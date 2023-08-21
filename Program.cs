// See https://aka.ms/new-console-template for more information
using System;
using System.IO;
using System.Text;
using XSystem.Security.Cryptography;

internal class Program
{
    private static string searchRoot = "";
    private static bool tryUniqueSize = false;
    private static bool deepTorrent = false;

    // get the list of files to search once
    private static IEnumerable<FileInfo> fileList = new List<FileInfo>();
    private static ILookup<int, FileInfo>? files = null;
    private static IList<string> InfoHashes = new List<string>();

    [STAThread]
    private static void Main(string[] args)
    {
#if false
        string[] hashes =
        {
        };

        string fileName = @"";
        int pieceLength = 32 * 1024;
        using (var stream = File.OpenRead(fileName))
        using (var reader = new BinaryReader(stream))
        {

            if (stream.Length >= pieceLength && stream.Length <= (1024*1024*4)) // 4MB
            {
                var buffer = new byte[stream.Length];
                reader.Read(buffer, 0, (int) stream.Length);
                for (int i = 0; 1 < stream.Length - pieceLength; i++)
                {
                    stream.Position = i;
                    var hash = Torrent.Bencode.Hash(buffer, i, pieceLength);
                    Console.WriteLine($"{i}\t{hash}");
                    if (hashes.Contains(hash))
                    {
                        Console.WriteLine($"{hash}\t{stream.Position - pieceLength}");
                        return;
                    }
                }
            }
            else
            {
                var buffer = new byte[pieceLength];
                for (int i = 0; 1 < stream.Length - pieceLength; i++)
                {
                    stream.Position = i;
                    while (pieceLength == reader.Read(buffer, 0, pieceLength))
                    {
                        var hash = Torrent.Bencode.Hash(buffer);
                        Console.WriteLine($"{i}\t{hash}");
                        if (hashes.Contains(hash))
                        {
                            Console.WriteLine($"{hash}\t{stream.Position - pieceLength}");
                            return;
                        }
                    }
                }
            }
        }
        return;
#endif

        foreach (var arg in args)
        {
            if (arg.StartsWith("-"))
            {
                if (arg.ToLower() == "-u" || arg.ToLower() == "-tryuniquesize")
                    tryUniqueSize = true;
                if (arg.ToLower() == "-d" || arg.ToLower() == "-deeptorrent")
                    deepTorrent = true;
                if (arg.ToLower() == "-s" || arg.ToLower() == "-nodeeptorrent")
                    deepTorrent = false;
            }
            else
                searchRoot = arg;
        }

        if (args.Length < 1 || searchRoot == "")
        {
            var folder = HashTester.UI.RequestFolder();
            if (folder != null)
            {
                searchRoot = folder;
            }
            else
            {
                Console.WriteLine("Please pass an argument for the base path. Torrent contents and .torrent files will be searched from there");
                return;
            }
        } 

        if (!Directory.Exists(searchRoot))
        {
            Console.WriteLine($"Could not find: {searchRoot}");
            return;
        }

        Console.WriteLine("Finding torrents...");

        IEnumerable<FileInfo> torrentFiles;

        if (deepTorrent)
        {
            torrentFiles = GetFiles(searchRoot, "*.torrent");
        }
        else
        {
            var dir = new DirectoryInfo(searchRoot);
            torrentFiles = dir.GetFiles("*.torrent");
        }
        Console.WriteLine($"Found {torrentFiles.Count()} torrent{(torrentFiles.Count() > 1 ? "s" : "")}...");

        Console.WriteLine("");

        Console.WriteLine("Finding files...");
        fileList = GetFiles(searchRoot, "*");
        Console.WriteLine($"Found {fileList.Count()} file{(fileList.Count() > 1 ? "s" : "")}...");

        Console.WriteLine("");

        files = fileList.ToLookup(f => (int) f.Length, f => f);

        fileList = new List<FileInfo>(); // release memory, probably unnecessary

        foreach (var torrentFile in torrentFiles)
        {
            Console.WriteLine(torrentFile.Name);
            ParseTorrent(torrentFile.FullName);
            Console.WriteLine("");
        }

        return;
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
                var infoHash = reader.GetInfoHash();

                Console.WriteLine($"{Environment.NewLine}InfoHash: {infoHash} Piece Size: {t.info.piece_length} Pieces: {t.info.pieces.Count}");
                if (InfoHashes.Contains(infoHash))
                {
                    Console.WriteLine("Duplicate InfoHash, skipping");
                    return false;
                }
                InfoHashes.Add(infoHash);

                Console.WriteLine("");

                int pieces = 0;

                var h = reader.CreateFileHashList(t);

                foreach (var v in h)
                {
                    pieces += v.PieceHashes.Count;

                    Console.WriteLine($"{Environment.NewLine}{v.Path} {v.FileLength} o {v.HashOffset} c {v.PieceHashes.Count}");

                    var fileName = FindFile(searchRoot, t.info.piece_length, v);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        Console.WriteLine($"Found: {v.Path} as {fileName}");
                        string destination = Path.Combine(t.info.name, v.Path);

                        // CreateDirectory() Creates all directories and subdirectories
                        // in the specified path unless they already exist.
                        // Exists() is redundant, but coded this way for a breakpoint on CreateDirectory()
                        // but the filename may have part of the path
                        string? destinationPath = Path.GetDirectoryName(destination);
                        if (destinationPath != null)
                        {
                            if (!Directory.Exists(destinationPath))
                                Directory.CreateDirectory(destinationPath);
                        }

                        if (!File.Exists(destination))
                            File.Copy(fileName, destination, false);
                        else
                            Console.WriteLine($"File exists: {destination}");
                    }
                }

                // TODO: second pass, for files that can't be hashed on their own
                // step 1, save the filenames and hashes instead of throwing them away
                // step 2, try each file if the file size fits,
                // //      and there is a previous/next file as needed

                Console.WriteLine($"Piece hashes: {pieces}");
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
        int hashMatched = 0;

        if (files.Contains(fileHash.FileLength))
        {
            // HACK: copy when size matches, and there's only 1 file that size
            if (tryUniqueSize)
            {
                if (files[fileHash.FileLength].Count() == 1)
                {
                    var file = files[fileHash.FileLength].First();
                    return file.FullName;
                }
            }

            foreach (var fileInfo in files[fileHash.FileLength])
            {
                // files under piece size
                // TODO: second pass, hash in order of info block
                Console.WriteLine($"{fileHash.FileLength}\t{fileInfo.FullName}");
                if (fileHash.HashOffset + pieceLength > fileInfo.Length)
                {
                    continue;
                }

                var buffer = new byte[pieceLength];
                int pieceIndex = 0;

                try
                {
                    using (var stream = File.OpenRead(fileInfo.FullName))
                    using (var reader = new BinaryReader(stream))
                    {
                        while (pieceLength == reader.Read(buffer, 0, pieceLength))
                        {
                            // just skip the rest of this file
                            if (pieceIndex >= fileHash.PieceHashes.Count)
                                break;

                            var hash = Torrent.Bencode.Hash(buffer);
                            Console.WriteLine(hash);
                            Console.WriteLine(fileHash.PieceHashes[pieceIndex]);
                            Console.WriteLine("");
                            // any piece matched is a potential fill
                            if (hash == fileHash.PieceHashes[pieceIndex])
                                hashMatched++;

                            pieceIndex++;
                        }
                    }
                } 
                catch(Exception e)
                {
                    // sharing violation, probably
                    Console.WriteLine(e);
                }

                if (hashMatched > 0)
                    result = fileInfo.FullName;
            }
        }
        return result;
    }

    // https://stackoverflow.com/questions/929276/how-to-recursively-list-all-the-files-in-a-directory-in-c
    public static IEnumerable<FileInfo> GetFiles(string path, string wildcard)
    {
        Queue<string> queue = new Queue<string>();
        queue.Enqueue(path);
        while (queue.Count > 0)
        {
            path = queue.Dequeue();
            try
            {
                foreach (string subDir in Directory.GetDirectories(path))
                {
                    queue.Enqueue(subDir);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            FileInfo[] files = null;
            try
            {
                // files = Directory.GetFiles(path, wildcard);
                var dir = new DirectoryInfo(path);
                files = dir.GetFiles(wildcard);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            if (files != null)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    yield return files[i];
                }
            }
        }
    }
}
