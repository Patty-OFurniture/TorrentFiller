using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using XSystem.Security.Cryptography;

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
        private Stream? stream;
        private string path;
        private int indent;

        private long infohashStart = 0;
        private long infohashEnd = 0;

        private static SHA1Managed sha1 = new SHA1Managed();

        public Bencode(string _path)
        {
            stream = null;
            path = _path;
            indent = 0;
        }

        public static string Hash(byte[] input, int offset, int count)
        {
            var hash = sha1.ComputeHash(input, offset, count);
            var sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        public static string Hash(byte[] input)
        {
            var hash = sha1.ComputeHash(input);
            var sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        public string? GetInfoHash()
        {
            string? result = null;
            if (infohashStart > 0 && infohashEnd > 0 && stream != null)
            {
                stream.Position = infohashStart;
                byte[] bytes = new byte[infohashEnd - infohashStart];
                stream.Read(bytes, 0, (int) (infohashEnd - infohashStart));
                result = Hash(bytes);
            }
            return result;
        }

        // this throws away hashes that span files
        public List<FileHash> CreateFileHashList(TorrentData data)
        {
            List<FileHash> fileHashList = new List<FileHash>();

            if (data.info.files == null || data.info.files.Count < 1)
                return fileHashList;

            Console.WriteLine("Files:");
            Queue<TorrentFile> files = new Queue<TorrentFile>();
            for (int filesIndex = 0; filesIndex < data.info.files.Count; filesIndex++)
            {
                files.Enqueue(data.info.files[filesIndex]);
                Console.WriteLine(data.info.files[filesIndex].length+ "\t" + data.info.files[filesIndex].path);
            }
            Console.WriteLine("");

            Console.WriteLine("Pieces:");
            Queue<string> pieceHashes = new Queue<string>();
            for (int pieceIndex = 0; pieceIndex < data.info.pieces.Count; pieceIndex++)
            {
                pieceHashes.Enqueue(data.info.pieces[pieceIndex]);
                Console.WriteLine(data.info.pieces[pieceIndex]);
            }
            Console.WriteLine("");

            var file = files.Dequeue();

            long bytesProcessed = 0;
            long fileProcessed = 0;
            int lastHashOffset = 0;

            // either a file spans pieces, or pieces span a file
            // files gets Dequeued at the end of the loop, so zero is fine
            while (files.Count >= 0 && pieceHashes.Count > 0)
            {
                // comsume files up to piece size
                if (file.length < data.info.piece_length)
                {
                    int pieceRemainder = data.info.piece_length - file.length;
                    fileProcessed += file.length;
                    while (pieceRemainder > 0 && files.Count > 0)
                    {
                        file = files.Dequeue();
                        // save before it goes negative
                        lastHashOffset = pieceRemainder;
                        pieceRemainder -= file.length;
                        fileProcessed += file.length;
                    };

                    // spanning hashes get thrown away
                    pieceHashes.Dequeue();
                    bytesProcessed += data.info.piece_length;

                    // start hashing this many bytes into the file
                    // file has this many bytes left over for the spanning hash
                    //long fileRemainder = file.length % data.info.piece_length;
                    // piece needs this much of the next file
                }
                else
                {
                    FileHash fileHash = new FileHash();
                    fileHash.Path = file.path;
                    fileHash.FileLength = file.length;
                    fileHash.HashOffset = lastHashOffset;
                    fileHash.PieceHashes = new List<string>();

                    long fileRemainder = fileHash.FileLength - fileHash.HashOffset;
                    while (fileRemainder >= data.info.piece_length)
                    {
                        fileProcessed += data.info.piece_length;
                        fileRemainder -= data.info.piece_length;
                        if (pieceHashes.TryDequeue(out var hash))
                        {
                            //Console.WriteLine(hash);
                            fileHash.PieceHashes.Add(hash);
                            //fileHash.PieceHashes.Add(pieceHashes.Dequeue());
                        }
                        bytesProcessed += data.info.piece_length;
                    }

                    fileHashList.Add(fileHash);

                    // 10k remainder with 32k pieces means skip 22k before hashing
                    // and throw away the file-spanning hash
                    if (fileRemainder > 0)
                    {
                        //fileRemainder = file.length - (int)(bytesProcessed % data.info.piece_length);
                        lastHashOffset = (int) (data.info.piece_length - fileRemainder);
                        fileProcessed += fileRemainder;
                        files.TryDequeue(out file);
                        pieceHashes.TryDequeue(out _);
                        bytesProcessed += data.info.piece_length;
                    }
                }
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
                        if (d["path"] is IList<object>)
                        {
                            var pathList = d["path"] as IList<object>;
                            for(int index = 0; index < pathList.Count; index++)
                            {
                                if (pathList[index] is byte[])
                                {
                                    pathList[index] = UTF8Encoding.UTF8.GetString(pathList[index] as byte[]);
                                }
                            }
                            //t.path = Path.Combine(params pathList.ToArray());
                            t.path = Path.Combine (pathList.Select(p => p.ToString()).ToArray());
                        }
                        else
                        {
                            t.path = d["path"].ToString();
                        }

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
                else
                { 
                    // temp hack - single files don't have a "files" info block
                    torrentInfo.files = new List<TorrentFile>();
                    TorrentFile file = new TorrentFile();
                    file.path = torrentInfo.name;
                    key = "length";
                    if (d.ContainsKey(key))
                        file.length = (int)d[key];
                    torrentInfo.files.Add(file);
                }
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
            catch (Exception e)
            {
                Console.Write(e);
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

                if (key.ToString() == "info")
                    infohashStart = stream.Position;

                object value = Read(key.ToString());
                if (value == null)
                    break;

                result.Add(key.ToString(), value);

                if (key.ToString() == "info")
                    infohashEnd = stream.Position;
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

        private object ReadBytes(int c)
        {
            StringBuilder sb = new StringBuilder();
            while (c != ':')
            {
                sb.Append((char)c);
                c = stream.ReadByte();
            }

            int stringLength = Convert.ToInt32(sb.ToString());
            byte[] bytes = new byte[stringLength];

            for (int i = 0; i < stringLength; i++)
            {
                char ch = (char)stream.ReadByte();
                bytes[i] = (byte)ch;
            }

            return bytes;
        }

        private object ReadString(int c)
        {
            StringBuilder sb = new StringBuilder();
            while (c != ':')
            {
                sb.Append((char)c);
                c = stream.ReadByte();
            }

            int stringLength = Convert.ToInt32(sb.ToString());
            byte[] bytes = new byte[stringLength];

            for (int i = 0; i < stringLength; i++)
            {
                char ch = (char) stream.ReadByte();
                bytes[i] = (byte) ch;
            }

            string result = System.Text.Encoding.UTF8.GetString(bytes);
            return result;
        }

        public object Read(string? key = null)
        {
            object result = null;

            while (true)
            {
                int c = stream.ReadByte();
                if (c < 0)
                    return null;

                //Console.Write(new string(' ', indent));
                switch (c)
                {
                    case 'e':
                        indent -= 2;
                        //Console.WriteLine("End");
                        break;
                    case 'd':
                        indent += 2;
                        //Console.WriteLine("Dictionary");
                        result = ReadDictionary();
                        break;
                    case 'l':
                        indent += 2;
                        //Console.WriteLine("List");
                        result = ReadList();
                        break;
                    case 'i':
                        result = ReadInt();
                        //Console.WriteLine(result);
                        break;
                    default:
                        if (key == "pieces")
                            result = ReadBytes(c);
                        else
                            result = ReadString(c);
                        //Console.WriteLine(result);
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
