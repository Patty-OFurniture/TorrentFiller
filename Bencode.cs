using HashTester;
using HashTester.UI;
using System;
using System.Text;
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
        public ulong length;
        public string path;
    }

    public struct TorrentInfo
    {
        public string name;
        public ulong piece_length;
        public ulong is_private;
        public List<TorrentFile> files;
        public List<string> pieces; // 20-byte SHA1 hash
    }

    public struct FileHash
    {
        // [info] -- TorrentInfo.files[].TorrentFile.length
        public ulong FileLength;
        // [info] -- TorrentInfo.files[].TorrentFile.path
        public string Path;
        // offset into the file where the first PieceHash starts
        public ulong HashOffset;
        // [piece length] -- TorrentInfo.piece_length
        public ulong PieceLength;
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
        private bool verbose = false;

        readonly private static SHA1Managed sha1 = new();

        public Bencode(string _path, bool verbose)
        {
            stream = null;
            path = _path;
            indent = 0;
            this.verbose = verbose;
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
            List<FileHash> fileHashList = new ();

            if (data.info.files == null || data.info.files.Count < 1)
                return fileHashList;

            if(verbose)
                Console.WriteLine("Files:");

            Queue<TorrentFile> files = new ();
            for (int filesIndex = 0; filesIndex < data.info.files.Count; filesIndex++)
            {
                files.Enqueue(data.info.files[filesIndex]);
                if (verbose)
                    Console.WriteLine(data.info.files[filesIndex].length+ "\t" + data.info.files[filesIndex].path);
            }
            if (verbose)
                Console.WriteLine("");

            if (verbose)
                Console.WriteLine("Pieces:");

            Queue<string> pieceHashes = new ();
            for (int pieceIndex = 0; pieceIndex < data.info.pieces.Count; pieceIndex++)
            {
                pieceHashes.Enqueue(data.info.pieces[pieceIndex]);
                if (verbose)
                    Console.WriteLine(data.info.pieces[pieceIndex]);
            }
            if (verbose)
                Console.WriteLine("");

            var file = files.Dequeue();

            ulong bytesProcessed = 0;
            ulong fileProcessed = 0;
            long lastHashOffset = 0;

            // either a file spans pieces, or pieces span a file
            // files gets Dequeued at the end of the loop, so zero is fine
            while (files.Count >= 0 && pieceHashes.Count > 0)
            {
                // comsume files up to piece size
                if (file.length < data.info.piece_length)
                {
                    long pieceRemainder = (long) (data.info.piece_length - file.length);
                    // possible overflow warning
                    fileProcessed += file.length;
                    while (pieceRemainder > 0 && files.Count > 0)
                    {
                        file = files.Dequeue();
                        // save before it goes negative
                        lastHashOffset = pieceRemainder;
                        pieceRemainder = pieceRemainder - (long) file.length;
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
                    FileHash fileHash = new ();
                    fileHash.Path = file.path;
                    fileHash.FileLength = file.length;
                    fileHash.HashOffset = (ulong) Math.Abs(lastHashOffset);
                    fileHash.PieceHashes = new List<string>();

                    ulong fileRemainder = fileHash.FileLength - fileHash.HashOffset;
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
                        lastHashOffset = (int) data.info.piece_length - (int) fileRemainder;
                        fileProcessed += fileRemainder;
                        files.TryDequeue(out file);
                        pieceHashes.TryDequeue(out _);
                        bytesProcessed += data.info.piece_length;
                    }
                }
            }

            return fileHashList;
        }

        private static List<TorrentFile> CreateTorrentFileList(List<object> l)
        {
            var result = new List<TorrentFile>();

            if (l != null)
            {
                foreach(var i in l)
                {
                    if (i is IDictionary<string, object>)
                    {
                        IDictionary<string, object> d = i as IDictionary<string, object>;

                        if (!d.ContainsKey("path"))
                            throw new BencodeException("Invalid dictionary, path is missing");

                        if (!d.ContainsKey("length"))
                            throw new BencodeException("Invalid dictionary, length is missing");

                        var t = new TorrentFile();
                        t.length = Convert.ToUInt64(d["length"]);
                        if (d["path"] is IList<object>)
                        {
                            var pathList = d["path"] as IList<object>;
                            if (pathList != null)
                            {
                                for (int index = 0; index < pathList.Count; index++)
                                {
                                    if (pathList[index] is byte[])
                                    {
                                        pathList[index] = UTF8Encoding.UTF8.GetString(pathList[index] as byte[]);
                                    }

                                    if (pathList[index] is not string)
                                        throw new BencodeException($"Complex type {pathList[index].GetType().FullName} found in path");
                                }
                            }
                            //t.path = Path.Combine(params pathList.ToArray());
                            t.path = Path.Combine(pathList.Select(p => p.ToString()).ToArray());
                        }
                        else
                        {
                            t.path = (d["path"] as string) ?? "";
                        }

                        if (FSUtilities.IsAbsolutePath(t.path))
                            throw new BencodeException("Absolute path found");

                        t.path = FSUtilities.NormalizePath(t.path);

                        string? message = FSUtilities.IsValidPath(t.path);
                        if (message != null)
                            throw new BencodeException("Invalid path found: " + message);


                        result.Add(t);
                    }
                }
            }

            return result;
        }

        private static List<string> CreateTorrentPieceInfo(byte[] pieces)
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

        private static TorrentInfo CreateTorrentInfo(IDictionary<string, object> d)
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
                    torrentInfo.piece_length = Convert.ToUInt64(d[key]);

                key = "pieces";
                if (d.ContainsKey(key))
                    torrentInfo.pieces = CreateTorrentPieceInfo(d[key] as byte[]);

                key = "private";
                if (d.ContainsKey(key))
                    torrentInfo.is_private = Convert.ToUInt64(d[key]);

                key = "files";
                if (d.ContainsKey(key))
                    torrentInfo.files = CreateTorrentFileList(d[key] as List<object>);
                else
                { 
                    // temp hack - single files don't have a "files" info block
                    torrentInfo.files = new List<TorrentFile>();
                    TorrentFile file = new ();
                    file.path = torrentInfo.name;
                    key = "length";
                    if (d.ContainsKey(key))
                        file.length = (ulong)d[key];
                    torrentInfo.files.Add(file);
                }
            }

            return torrentInfo;
        }

        public static TorrentData CreateTorrentData(IDictionary<string, object> d)
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
                    var l = d[key] as List<object>;
                    if (l != null)
                    {
                        foreach (object o in l)
                        {
                            if (o != null)
                            {
                                if (o is string)
                                    torrentData.announce_list.Add(o as string);
                                else if (o is List<object>)
                                {
                                    foreach(var ls in (o as List<object>))
                                        torrentData.announce_list.Add(ls.ToString());
                                }
                                else
                                    System.Diagnostics.Debugger.Break();
                            }
                        }
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
            catch(System.IO.FileNotFoundException)
            {
                //if (verbose)
                    Console.WriteLine($"Cannot open [{path}]");
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

            if (stream == null) 
                return result;

            while (true)
            {
                object? key = Read();
                if (key == null)
                    break;

                if (key is IDictionary<string, object>)
                {
                    System.Diagnostics.Debugger.Break();
                    // TODO: ???
                    // is this a V2 file?
                    //var d = (IDictionary<string, object>)key;
                    //foreach (var k in d.Keys)
                    //    result.Add(k, d[k]);
                }
                else
                {
                    string sKey = key as string;

                    if (result.ContainsKey(sKey))
                        return result;

                    if (sKey == "info")
                        infohashStart = stream.Position;

                    object value = Read(sKey);
                    if (value == null)
                        break;

                    result.Add(sKey, value);

                    if (sKey == "info")
                        infohashEnd = stream.Position;
                }
            }
            return result;
        }

        private object ReadList()
        {
            List<object> items = new();
            object? item = Read();
            while (item != null)
            {
                if (item is string)
                    items.Add(item as string);
                else
                    items.Add(item);

                item = Read();
            }

            if (items.Count == 1)
                return items[0];
            else
                return items;
        }

        private ulong ReadInt()
        {
            if (stream == null) 
                return 0;

            StringBuilder sb = new();
            int c = stream.ReadByte();

            do
            {
                sb.Append((char)c);
                c = stream.ReadByte();
            }
            while (c != 'e');

            string temp = sb.ToString();
            if (temp == "-1")
                temp = "0";

            if (temp[0] == '-')
                throw new BencodeException("Negative values are allowed, but unexpected");

            return Convert.ToUInt64(temp);
        }

        private object? ReadBytes(int c)
        {
            if (stream == null)
                return null;

            StringBuilder sb = new ();
            while (c != ':')
            {
                sb.Append((char)c);
                c = stream.ReadByte();
            }

            ulong stringLength = Convert.ToUInt64(sb.ToString());
            byte[] bytes = new byte[stringLength];

            for (ulong i = 0; i < stringLength; i++)
            {
                char ch = (char)stream.ReadByte();
                bytes[i] = (byte)ch;
            }

            return bytes;
        }

        private object? ReadString(int c)
        {
            if (stream == null)
                return null;

            StringBuilder sb = new ();
            while (c != ':')
            {
                sb.Append((char)c);
                c = stream.ReadByte();
            }

            ulong stringLength = Convert.ToUInt64(sb.ToString());
            byte[] bytes = new byte[stringLength];

            for (ulong i = 0; i < stringLength; i++)
            {
                char ch = (char) stream.ReadByte();
                bytes[i] = (byte) ch;
            }

            string result = System.Text.Encoding.UTF8.GetString(bytes);
            return result;
        }

        public object? Read(string? key = null)
        {
            if (stream == null)
                return null;

            object? result = null;

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

            GC.SuppressFinalize(this);
        }
    }
}
