using HashTester;
using HashTester.UI;
using Torrent;

public class Program
{
    public const uint OneK = 1024;

    internal class MatchOptions
    {
        public static string searchRoot = "";
        public static bool tryUniqueSize = false;
        public static bool deepTorrent = false;
        public static string torrentSearch = "*.torrent";
        public static bool verbose = false;
    }

    // get the list of files to search once
    private static IEnumerable<FileInfo> fileList = new List<FileInfo>();
    private static ILookup<ulong, FileInfo>? filesLookup = null;
    private static IList<string> InfoHashes = new List<string>();

    [STAThread]
    private static void Main(string[] args)
    {

        #region TestCode
#if false
        MatchOptions.searchRoot = ""; 
        var targetRoot = "";

        int i = MatchOptions.searchRoot.Length;
        foreach (var file in GetFiles(MatchOptions.searchRoot, "*"))
        {
            var target = file.FullName.Substring(i);
            if (!File.Exists(targetRoot + target))
            {
                System.Diagnostics.Debug.WriteLine(file.FullName);
                // EnsureDirectoryExists(targetRoot + target);
                // File.Copy(file.FullName, targetRoot + target, false);
            }
        }
        return;

        string[] hashes =
        {
        };

        string fileName = @"";
        int pieceLength = 32 * OneK;
        using (var stream = File.OpenRead(fileName))
        using (var reader = new BinaryReader(stream))
        {

            if (stream.Length >= pieceLength && stream.Length <= (OneK*OneK*4)) // 4MB
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
        #endregion TestCode

        foreach (var arg in args)
        {
            string larg = arg.ToLower();
            if (arg.StartsWith("-"))
            {
                if (larg == "-u" || larg == "-tryuniquesize")
                    MatchOptions.tryUniqueSize = true;
                if (larg == "-d" || larg == "-deeptorrent")
                    MatchOptions.deepTorrent = true;
                if (larg == "-s" || larg == "-nodeeptorrent")
                    MatchOptions.deepTorrent = false;
                if (larg == "-v" || larg == "-verbose")
                    MatchOptions.verbose = true;
            }
            else
                MatchOptions.searchRoot = arg;
        }

        if (args.Length < 1 || MatchOptions.searchRoot == "")
        {
            var folder = UI.RequestFolder();
            if (folder != null)
            {
                MatchOptions.searchRoot = folder;
            }
            else
            {
                Console.Error.WriteLine("Please pass an argument for the base path. Torrent contents and .torrent files will be searched from there");
                return;
            }
        } 

        if (!Directory.Exists(MatchOptions.searchRoot))
        {
            Console.Error.WriteLine("Could not find: {MatchOptions.searchRoot}");
            return;
        }

        Console.WriteLine($"searchRoot    : {MatchOptions.searchRoot}");
        Console.WriteLine($"tryUniqueSize : {MatchOptions.tryUniqueSize}");
        Console.WriteLine($"deepTorrent   : {MatchOptions.deepTorrent}");
        Console.WriteLine($"deepTorrent   : {MatchOptions.verbose}");

        Console.WriteLine("Finding torrents...");

        IEnumerable<FileInfo> torrentFiles;

        if (MatchOptions.deepTorrent)
        {
            torrentFiles = GetFiles(MatchOptions.searchRoot, MatchOptions.torrentSearch);
        }
        else
        {
            var dir = new DirectoryInfo(MatchOptions.searchRoot);
            torrentFiles = dir.GetFiles(MatchOptions.torrentSearch);
        }

        Console.WriteLine($"Found {torrentFiles.Count()} torrent{(torrentFiles.Count() > 1 ? "s" : "")}...");

        Console.WriteLine("");

        Console.WriteLine("Finding files...");
        fileList = GetFiles(MatchOptions.searchRoot, "*");
        Console.WriteLine($"Found {fileList.Count()} file{(fileList.Count() > 1 ? "s" : "")}...");

        Console.WriteLine("");

        filesLookup = fileList.ToLookup(f => (ulong) f.Length, f => f);
        if (filesLookup == null)
            return;

        fileList = new List<FileInfo>(); // release memory, probably unnecessary

        // ui hacks for progress visibility
#if true
        int progress = 0;
        var textProgressBar = new TextProgressBar(20);
        foreach (var torrentFile in torrentFiles)
        {
            if (!Console.IsOutputRedirected)
            {
                textProgressBar.WriteProgressBar(100 * progress++ / torrentFiles.Count());
                Console.Write(" ");
            }

            Console.WriteLine(torrentFile.Name);

            ParseTorrent(torrentFile.FullName, MatchOptions.verbose);
        }

        if (!Console.IsOutputRedirected)
        {
            textProgressBar.WriteProgressBar(100);
            Console.WriteLine();
        }

#else
        Console.Clear();
        using (var progressBar = new HashTester.UI.ProgressBar())
        {
            string header = new string('*', Console.WindowWidth);

            double progress = 0;
            foreach (var torrentFile in torrentFiles)
            {
                if (Console.IsOutputRedirected)
                {
                    Console.WriteLine(torrentFile.Name);
                    ParseTorrent(torrentFile.FullName, MatchOptions.verbose);
                }
                else
                    progressBar.Report(progress++ / torrentFiles.Count());
            }
        }
#endif
        Console.WriteLine("Done");

        return;
    }

    static void EnsureDirectoryExists(string destination)
    {
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
    }

    static bool ParseTorrent(string torrentFile, bool verbose)
    {
        bool result = false;
        try
        {
            var reader = new Torrent.Bencode(torrentFile, verbose);
            if (reader.OpenTorrent())
            {
                var d = reader.Read() as IDictionary<string, object>;

                if (d == null)
                    throw new BencodeException("Initial dictionary not loaded");

                var t = Bencode.CreateTorrentData(d);
                var infoHash = reader.GetInfoHash();

                if (infoHash == null)
                {
                    if (MatchOptions.verbose)
                    {
                        Console.Error.WriteLine("InfoHash not found, skipping");
                    }
                    return false;
                }

                if (t.info.piece_length < 1)
                {
                    Console.WriteLine($"Corrupted file, piece size is invalid");
                    return false;
                }

                string pieceSize = FSUtilities.FormatSize(t.info.piece_length, OneK);

                if (MatchOptions.verbose)
                {
                    Console.WriteLine($"{Environment.NewLine}InfoHash: {infoHash} Piece Size: {pieceSize} Pieces: {t.info.pieces.Count}");
                    Console.WriteLine("");
                }

                if (InfoHashes.Contains(infoHash))
                {
                    if (MatchOptions.verbose)
                    {
                        Console.Error.WriteLine($"Duplicate InfoHash {infoHash}, skipping");
                        // System.IO.File.Delete(torrentFile);
                    }
                    return false;
                }

                InfoHashes.Add(infoHash);

                int pieces = 0;

                var h = reader.CreateFileHashList(t);

                foreach (var v in h)
                {
                    pieces += v.PieceHashes.Count;

                    if (MatchOptions.verbose)
                        Console.WriteLine($"{Environment.NewLine}{v.Path} {v.FileLength} o {v.HashOffset} c {v.PieceHashes.Count}");

                    var fileName = FindFile(t.info.piece_length, v);

                    if (!string.IsNullOrEmpty(fileName))
                    {
                        if (MatchOptions.verbose)
                            Console.WriteLine($"Found: {v.Path} as {fileName}");

                        string destination = Path.Combine(t.info.name, v.Path);

                        bool overwrite = false;

                        try
                        {
                            if (overwrite)
                            {
                                File.Copy(fileName, destination, overwrite);
                            }
                            else if (File.Exists(destination))
                            {
                                if (MatchOptions.verbose)
                                    Console.WriteLine($"File exists: {destination}");
                            }
                            else
                            {
                                EnsureDirectoryExists(destination);
                                File.Copy(fileName, destination, false);
                            }
                        }
                        catch(Exception e)
                        {
                            Console.Error.WriteLine($"Error: {e}");
                        }
                    }

                    continue;
                }

                // TODO: second pass, for files that can't be hashed on their own
                // step 1, save the filenames and hashes instead of throwing them away
                // step 2, try each file if the file size fits,
                // //      and there is a previous/next file as needed

                if (filesLookup == null || filesLookup.Count < 1)
                {
                    Console.Error.WriteLine($"{nameof(filesLookup)} contains nothing to find");
                    return false;
                }

                // because CreateFileHashList() throws away files that can't be hashed
                if (MatchOptions.tryUniqueSize)
                {
                    foreach (var f in t.info.files)
                    {
                        // piece_length * 2 should be hashable, this is for smaller files
                        if (f.length >= (t.info.piece_length * 2))
                            continue;

                        var candidates = filesLookup[f.length].ToList();

                        if (candidates.Count < 1)
                            continue;

                        // relative path
                        string destination = Path.Combine(t.info.name, f.path);

                        // if it was found by hash, or was previously found, skip it
                        if (File.Exists(destination))
                        {
                            Console.Error.WriteLine($"File exists: {destination}");
                            continue;
                        }

#if false
                        // filter out files that seem to be in torrent structure
                        candidates = candidates
                            .Where(c => !c.FullName.Contains(destination, StringComparison.InvariantCultureIgnoreCase))
                            .ToList();
#endif
                        // ignore duplicate files
                        if (candidates.Count > 1)
                        {
                            var sha1 = new List<string>();
                            for (int i = 0; i < candidates.Count; i++)
                            {
                                var candidate = candidates[i];

                                using var stream = File.OpenRead(candidate.FullName);
                                byte[] bytes = new byte[candidate.Length];
                                stream.Read(bytes, 0, (int)candidate.Length);
                                var s = Bencode.Hash(bytes);
                                if (sha1.Contains(s))
                                    candidates[i] = null;
                                else
                                    sha1.Add(s);
                            }
                            candidates = candidates.Where(c => c != null).ToList();
                        }

                        try
                        {
                            if (candidates.Count == 1)
                            {
                                EnsureDirectoryExists(destination);
                                File.Copy(candidates[0].FullName, destination, false);
                            }
                            else
                            {
                                // Count == 0 will be handled here
                                foreach (var candidate in candidates)
                                {
                                    // TODO: how to handle
                                    EnsureDirectoryExists(destination);
                                    File.Copy(candidate.FullName, destination, false);
                                    break;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine($"Error: {e}");
                        }
                    }
                }

                if (MatchOptions.verbose)
                    Console.WriteLine($"Piece hashes: {pieces}");
            }
            result = true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }

        return result;
    }

    static string FindFile(ulong pieceLength, Torrent.FileHash fileHash)
    {
        string result = "";
        int hashMatched = 0;

        if (filesLookup.Contains(fileHash.FileLength))
        {
            foreach (var fileInfo in filesLookup[fileHash.FileLength])
            {
                // files under piece size
                // TODO: second pass, hash in order of info block
                if (MatchOptions.verbose)
                    Console.WriteLine($"{fileHash.FileLength}\t{fileInfo.FullName}");

                if (fileHash.HashOffset + pieceLength > fileHash.FileLength)
                {
                    continue;
                }

                var buffer = new byte[pieceLength];
                int pieceIndex = 0;

                try
                {
                    using var stream = File.OpenRead(fileInfo.FullName);
                    using var reader = new BinaryReader(stream);

                    // TODO: why is this a signed int?
                    reader.Read(buffer, 0, (int)fileHash.HashOffset);
                    while (pieceLength == (ulong)reader.Read(buffer, 0, (int)pieceLength))
                    {
                        // just skip the rest of this file
                        if (pieceIndex >= fileHash.PieceHashes.Count)
                            break;

                        var hash = Torrent.Bencode.Hash(buffer);
                        if (MatchOptions.verbose)
                        {
                            Console.WriteLine(hash);
                            Console.WriteLine(fileHash.PieceHashes[pieceIndex]);
                            Console.WriteLine("");
                        }

                        // any piece matched is a potential fill
                        if (hash == fileHash.PieceHashes[pieceIndex])
                            hashMatched++;

                        pieceIndex++;
                    }
                }
                catch (System.IO.FileNotFoundException)
                {
                    Console.WriteLine($"Cannot open [{fileInfo.FullName}]");
                }
                catch (Exception e)
                {
                    // sharing violation, probably
                    Console.Error.WriteLine(e);
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
        Queue<string> queue = new ();
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

            FileInfo[] files = Array.Empty<FileInfo>();
            try
            {
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
