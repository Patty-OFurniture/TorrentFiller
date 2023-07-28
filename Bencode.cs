using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Torrent
{
    public struct TorrentData
    {
        public string announce;
        public List<string> announce_list;
        public DateTime creation_date;
        public TorrentInfo info;
    }

    public struct TorrentFile
    {
        public int length;
        public string path;
    }

    public struct TorrentInfo
    {
        public string name;
        public int piece_length;
        public int is_private;
        public List<TorrentFile> files;
        public List<string> pieces; // 20-byte SHA1 hash
    }

    public struct FileHash
    {
        // [info] -- TorrentInfo.files[].TorrentFile.length
        public int FileLength;
        // [info] -- TorrentInfo.files[].TorrentFile.path
        public string Path;
        // offset into the file where the first PieceHash starts
        public int HashOffset;
        // [piece length] -- TorrentInfo.piece_length
        public int PieceLength;
        // [pieces] -- lowercase SHA-1
        public List<string> PieceHashes;
    }

    public class Bencode : IDisposable
    {
        private Stream stream;
        private string path;
        private int indent;

        public Bencode(string _path)
        {
            stream = null;
            path = _path;
            indent = 0;
        }

        // this throws away hashes that span files
        public List<FileHash> CreateFileHashList(TorrentData data)
        {
            List<FileHash> fileHashList = new List<FileHash>();

            if (data.info.files == null)
                return fileHashList;

            int fileIndex = 0;
            int fileOffset = 0;
            int fileRemainder = data.info.files[fileIndex].length;

            for (int pieceIndex = 0; pieceIndex < data.info.pieces.Count; pieceIndex++)
            {
                FileHash fileHash = new FileHash();
                fileHash.Path = data.info.files[fileIndex].path;
                fileHash.FileLength = data.info.files[fileIndex].length;
                fileHash.HashOffset = fileOffset;
                fileHash.PieceHashes = new List<string>();

                while (fileRemainder >= data.info.piece_length)
                {
                    fileOffset += data.info.piece_length;
                    fileRemainder -= data.info.piece_length;
                    fileHash.PieceHashes.Add(data.info.pieces[pieceIndex++]);
                }
                fileHashList.Add(fileHash);

                while (++fileIndex < data.info.files.Count)
                {
                    if (fileRemainder >= data.info.files[fileIndex].length)
                        fileRemainder -= data.info.files[fileIndex].length;
                    else
                        break; // goto might be harmless here
                }

                if (!(fileIndex < data.info.files.Count))
                    return fileHashList;

                // 10k remainder with 32k pieces means skip 22k before hashing
                fileOffset = data.info.piece_length - fileRemainder;
                fileRemainder = data.info.files[fileIndex].length - fileOffset;
            }

            return fileHashList;
        }

        private List<TorrentFile> CreateTorrentFileList(List<object> l)
        {
            var result = new List<TorrentFile>();

            if (l != null)
            {
                foreach(var i in l)
                {
                    if (i is IDictionary<string, object>)
                    {
                        IDictionary<string, object> d = (IDictionary<string, object>)i;
                        var t = new TorrentFile();
                        t.length = Convert.ToInt32(d["length"]);
                        t.path = d["path"].ToString();
                        result.Add(t);
                    }
                }
            }

            return result;
        }

        private List<string> CreateTorrentPieceInfo(byte[] pieces)
        {
            var pieceList = new List<string>();
            var sBuilder = new StringBuilder();

            for (int i = 0; i < pieces.Length; i++)
            {
                sBuilder.Append(pieces[i].ToString("x2"));
                if (sBuilder.Length == 40)
                {
                    // Console.WriteLine(new string(' ', indent) + sBuilder.ToString());
                    pieceList.Add(sBuilder.ToString());
                    sBuilder = new StringBuilder();
                }
            }
            return pieceList;
        }

        private TorrentInfo CreateTorrentInfo(IDictionary<string, object> d)
        {
            string key;
            var torrentInfo = new TorrentInfo();

            if (d != null)
            {
                key = "name";
                if (d.ContainsKey(key))
                    torrentInfo.name = d[key].ToString();

                key = "piece length";
                if (d.ContainsKey(key))
                    torrentInfo.piece_length = (int) d[key];

                key = "pieces";
                if (d.ContainsKey(key))
                    torrentInfo.pieces = CreateTorrentPieceInfo(d[key] as byte[]);

                key = "private";
                if (d.ContainsKey(key))
                    torrentInfo.is_private = (int)d[key];

                key = "files";
                if (d.ContainsKey(key))
                    torrentInfo.files = CreateTorrentFileList(d[key] as List<object>);
            }

            return torrentInfo;
        }

        public TorrentData CreateTorrentData(IDictionary<string, object> d)
        {
            string key;
            var torrentData = new TorrentData();

            if (d != null)
            {
                key = "announce";
                if (d.ContainsKey(key))
                    torrentData.announce = d[key].ToString();

                key = "announce-list";
                if (d.ContainsKey(key))
                {
                    torrentData.announce_list = new List<string>();
                    foreach(var s in d[key] as List<object>)
                    {
                        torrentData.announce_list.Add(s.ToString());
                    }
                    
                }

                key = "creation date";
                if (d.ContainsKey(key))
                    torrentData.creation_date = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(d[key])).DateTime;

                key = "info";
                if (d.ContainsKey(key))
                    torrentData.info = CreateTorrentInfo(d[key] as IDictionary<string, object>);
            }

            return torrentData;
        }

        public bool OpenTorrent()
        {
            bool status = false;

            try
            {
                stream = System.IO.File.OpenRead(path);
                status = true;
            }
            catch (Exception)
            {

            }

            return status;
        }

        private Dictionary<string, object> ReadDictionary()
        {
            var result = new Dictionary<string, object>();
            while (true)
            {
                object key = Read();
                if (key == null)
                    break;
                object value = Read();
                if (value == null)
                    break;

                result.Add(key.ToString(), value);
            }
            return result;
        }

        private object ReadList()
        {
            List<object> items = new List<object>();
            object item = Read();
            while (item != null)
            {
                if (item is string)
                    items.Add(item.ToString());
                else
                    items.Add(item);

                item = Read();
            }

            if (items.Count == 1)
                return items[0];
            else
                return items;
        }

        private int ReadInt()
        {
            StringBuilder sb = new StringBuilder();
            int c = stream.ReadByte();

            do
            {
                sb.Append((char)c);
                c = stream.ReadByte();
            }
            while (c != 'e');

            return Convert.ToInt32(sb.ToString());
        }

        private object ReadString(int c)
        {
            bool isAscii = true;

            StringBuilder sb = new StringBuilder();
            while (c != ':')
            {
                sb.Append((char)c);
                c = stream.ReadByte();
            }

            int stringLength = Convert.ToInt32(sb.ToString());
            byte[] bytes = new byte[stringLength];

            for (int i = 0; i <  stringLength; i++)
            {
                char ch = (char) stream.ReadByte();
                if (!char.IsLetterOrDigit(ch) && !char.IsPunctuation(ch) && !char.IsWhiteSpace(ch))
                    isAscii = false;

                bytes[i] = (byte) ch;
            }

            if (isAscii)
            {
                string result = System.Text.Encoding.UTF8.GetString(bytes);
                return result;
            }

            return bytes;
        }

        public object Read()
        {
            object result = null;

            while (true)
            {
                int c = stream.ReadByte();
                if (c < 0)
                    return null;

                Console.Write(new string(' ', indent));
                switch (c)
                {
                    case 'e':
                        indent -= 2;
                        Console.WriteLine("End");
                        break;
                    case 'd':
                        indent += 2;
                        Console.WriteLine("Dictionary");
                        result = ReadDictionary();
                        break;
                    case 'l':
                        indent += 2;
                        Console.WriteLine("List");
                        result = ReadList();
                        break;
                    case 'i':
                        Console.WriteLine(result = ReadInt());
                        break;
                    default:
                        Console.WriteLine(result = ReadString(c));
                        break;
                }
                return result;
            }
        }

        public void Dispose()
        {
            if (stream != null)
                stream.Dispose();
        }
    }
}
