// See https://aka.ms/new-console-template for more information
using System.Text;
using XSystem.Security.Cryptography;

internal class Program
{
    private static int pieceLength = 1024 * 32; // default, will be set from .torrent
    private static string searchRoot = "";

    // get the list of files to search once
    private static string[] fileNames = { "" };
    private static List<FileInfo> files = new List<FileInfo>();

    private static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Please pass an argument for the base path. Torrent contents and .torrent files will be searched from there");
            return;
        }

        searchRoot = args[0];

        if (!Directory.Exists(searchRoot))
        {
            Console.WriteLine($"Could not find: {searchRoot}");
            return;
        }

        fileNames = Directory.GetFiles(searchRoot, "*", SearchOption.AllDirectories);

        foreach (var file in fileNames)
        {
            var fileInfo = new FileInfo(file);
            files.Add(fileInfo);
        }

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
                    //if (v.Path == "System.Collections.Generic.List`1[System.Object]")
                        //System.Diagnostics.Debugger.Break();

                    //foreach (var z in v.PieceHashes)
                    //{
                    //    Console.WriteLine(z);
                    //}

                    var fileName = FindFile(searchRoot, pieceLength, v);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        Console.WriteLine($"Found: {fileName}");
                        string destination = Path.Combine(t.info.name, v.Path);
                        
                        if (!Directory.Exists(t.info.name))
                            Directory.CreateDirectory(t.info.name);

                        if (!File.Exists(destination))
                            File.Copy(fileName, destination, false);
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

    static string FindFile(string root, int pieceLength, Torrent.FileHash fileHash)
    {
        string result = "";
        bool hashMatched = false;

        foreach (var fileInfo in files)
        {
            if (fileInfo.Length == fileHash.FileLength)
            {
                var buffer = new byte[pieceLength];
                int pieceIndex = 0;

                using (var stream = File.OpenRead(fileInfo.FullName))
                using (var reader = new BinaryReader(stream))
                {
                    try
                    {
                        if (fileHash.HashOffset > 0 && stream.Length >= fileHash.HashOffset)
                            reader.Read(buffer, 0, fileHash.HashOffset);
                    } 
                    catch(Exception e)
                    {
                        // not sure what triggers this
                        // added stream.Length >= fileHash.HashOffset test above, but how does it get that way?
                        Console.WriteLine(e);
                    }

                    while (pieceLength == reader.Read(buffer, 0, pieceLength))
                    {
                        // just skip the rest of this file
                        if (pieceIndex >= fileHash.PieceHashes.Count)
                            break;

                        var hash = Hash(buffer);
                        Console.WriteLine(hash);
                        Console.WriteLine(fileHash.PieceHashes[pieceIndex]);
                        Console.WriteLine("");
                        // any piece matched is a potential fill
                        // could break here, but reporting all hashes for dev purposes
                        if (hash == fileHash.PieceHashes[pieceIndex++])
                            hashMatched = true;
                    }
                }

                if (hashMatched)
                    result = fileInfo.FullName;
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
