// See https://aka.ms/new-console-template for more information
using System.Text;
using XSystem.Security.Cryptography;

internal class Program
{
    private static int pieceLength = 1024 * 32; // default, will be set from .torrent
    private static string searchRoot = "";

    private static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Please pass an argument for the base path. Torrent contents and .torrent files will be searched from there");
            return;
        }

        searchRoot = args[0];

        var torrentFiles = Directory.GetFiles(searchRoot, "*.torrent", SearchOption.AllDirectories);
        foreach (var torrentFile in torrentFiles)
        {
            ParseTorrent(torrentFile);
        }

        // unit test
        //int fileSize = 52624;
        //int offset = 6081;
        //var fileName = FindFile(searchRoot, pieceLength, offset, fileSize);
        //Console.WriteLine(fileName);
    }

    static bool ParseTorrent(string torrentFile)
    {
        bool result = false;
        try
        {
            var reader = new Torrent.Bencode(torrentFile);
            if (reader.OpenTorrent())
            {
                var d = reader.Read() as IDictionary<string, object>;
                var t = reader.CreateTorrentData(d);
                var h = reader.CreateFileHashList(t);

                pieceLength = t.info.piece_length;

                foreach (var v in h)
                {
                    Console.WriteLine($"{Environment.NewLine}{v.Path} {v.FileLength} {v.HashOffset}");
                    foreach (var z in v.PieceHashes)
                    {
                        Console.WriteLine(z);
                    }

                    var fileName = FindFile(searchRoot, pieceLength, v.HashOffset, v.FileLength);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        Console.WriteLine($"Found: {fileName}");
                        string destination = Path.Combine(t.info.name, v.Path);
                        
                        if (!Directory.Exists(t.info.name))
                            Directory.CreateDirectory(t.info.name);

                        System.IO.File.Copy(fileName, destination, false);
                    }
                }
            }
            result = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return result;
    }

    static string FindFile(string root, int pieceLength, int offset, int fileSize)
    {
        string result = "";

        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            if (fileInfo.Length == fileSize)
            {
                var buffer = new byte[pieceLength];

                using (var stream = File.OpenRead(file))
                using (var reader = new BinaryReader(stream))
                {
                    if (offset > 0)
                        reader.Read(buffer, 0, offset);

                    while (pieceLength == reader.Read(buffer, 0, pieceLength))
                    {
                        var hash = Hash(buffer);
                        Console.WriteLine(hash);
                    }
                }

                result = file;
            }
        }
        return result;
    }

    static string Hash(byte[] input)
    {
        using (SHA1Managed sha1 = new SHA1Managed())
        {
            // convert char[] from StreamReader to byte[]
            var hash = sha1.ComputeHash(input);
            var sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
