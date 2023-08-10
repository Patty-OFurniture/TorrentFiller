// See https://aka.ms/new-console-template for more information
using System.IO;
using System.Text;
using XSystem.Security.Cryptography;

internal class Program
{
    private static string searchRoot = "";

    // get the list of files to search once
    private static IEnumerable<FileInfo> fileList = new List<FileInfo>();
    private static ILookup<int, FileInfo> files = null;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length < 1)
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
        else
        {
            searchRoot = args[0];
        }

        if (!Directory.Exists(searchRoot))
        {
            Console.WriteLine($"Could not find: {searchRoot}");
            return;
        }

        Console.WriteLine("Finding torrents...");
#if !DeepTorrent
        var torrentFiles = GetFiles(searchRoot, "*.torrent");
#else
        var dir = new DirectoryInfo(searchRoot);
        var torrentFiles = dir.GetFiles("*.torrent");
#endif
        Console.WriteLine($"Found {torrentFiles.Count()} torrents...");

        Console.WriteLine("");

        Console.WriteLine("Finding files...");
        fileList = GetFiles(searchRoot, "*");
        Console.WriteLine($"Found {fileList.Count()} files...");

        Console.WriteLine("");

        files = fileList.ToLookup(f => (int) f.Length, f => f);

        fileList = new List<FileInfo>(); // probably unnecessary

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
                var h = reader.CreateFileHashList(t);
                var infoHash = reader.GetInfoHash();

                Console.WriteLine($"{Environment.NewLine}InfoHash: {infoHash} Piece Size: {t.info.piece_length} Pieces: {t.info.pieces.Count}");

                // files may not be sorted in a logical order, display them in hash order
                foreach (var f in t.info.files)
                {
                    Console.WriteLine($"{f.length} {f.path}");
                }
                Console.WriteLine("");

                int pieces = 0;

                foreach (var v in h)
                {
                    pieces += v.PieceHashes.Count;

                    Console.WriteLine($"{Environment.NewLine}{v.Path} {v.FileLength} o {v.HashOffset} c {v.PieceHashes.Count}");
                    //if (v.Path == "System.Collections.Generic.List`1[System.Object]")
                        //System.Diagnostics.Debugger.Break();

                    //foreach (var z in v.PieceHashes)
                    //{
                    //    Console.WriteLine(z);
                    //}

                    var fileName = FindFile(searchRoot, t.info.piece_length, v);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        Console.WriteLine($"Found: {fileName}");
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
                    }
                }
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
        bool hashMatched = false;

        if (files.Contains(fileHash.FileLength))
        {
            // HACK: copy when size matches, and there's only 1 file that size
            if (files[fileHash.FileLength].Count() == 1
                && pieceLength > fileHash.FileLength)
            {
                return files[fileHash.FileLength].First().FullName;
            }

            foreach (var fileInfo in files[fileHash.FileLength])
            {
                // files under piece size
                // TODO: second pass, hash in order of info block
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
                        try
                        {
                            if (fileHash.HashOffset > 0 && stream.Length >= fileHash.HashOffset)
                                reader.Read(buffer, 0, fileHash.HashOffset);
                        }
                        catch (Exception e)
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

                            var hash = Torrent.Bencode.Hash(buffer);
                            Console.WriteLine(hash);
                            Console.WriteLine(fileHash.PieceHashes[pieceIndex]);
                            Console.WriteLine("");
                            // any piece matched is a potential fill
                            // could break here, but reporting all hashes for dev purposes
                            if (hash == fileHash.PieceHashes[pieceIndex++])
                                hashMatched = true;
                        }
                    }
                } 
                catch(Exception e)
                {
                    // sharing violation, probably
                    Console.WriteLine(e);
                }

                if (hashMatched)
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
                //                files = Directory.GetFiles(path, wildcard);
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
