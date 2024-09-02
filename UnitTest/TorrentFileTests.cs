using HashTester;
using System.Diagnostics;
using Torrent;

namespace UnitTest
{
    [TestClass]
    public class TorrentFileTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var files = Directory.GetFiles("..\\..\\..\\libtorrent-RC_2_0", "*.torrent");

            foreach (var file in files)
            {
                Console.WriteLine(file);
                TestTorrentFile(file);
            }
        }

        private static void TestTorrentFile(string torrentFile)
        {
            try
            {
                var reader = new Torrent.Bencode(torrentFile, true);
                if (reader.OpenTorrent())
                {
                    var o = reader.Read();
                    var d = o as IDictionary<string, object>;

                    if (d == null)
                        throw new BencodeException("Initial dictionary not loaded");

                    var t = Bencode.CreateTorrentData(d);
                    var infoHash = reader.GetInfoHash();

                    ulong piecesize = t.info.piece_length;
                    string pieceString = "bytes";

                    if (piecesize < 1)
                    {
                        Console.WriteLine($"Corrupted file, piece size is invalid");
                        return;
                    }

                    if (piecesize == (piecesize / 1024 * 1024))
                    {
                        piecesize = piecesize / 1024;
                        pieceString = "kB";
                        if (piecesize == (piecesize / 1024 * 1024))
                        {
                            piecesize = piecesize / 1024;
                            pieceString = "mB";
                        }
                    }

                    if (t.info.pieces == null)
                        throw new BencodeException("Torrent pieces corrupt or not found");

                    Console.WriteLine($"{Environment.NewLine}InfoHash: {infoHash} Piece Size: {piecesize} {pieceString} Pieces: {t.info.pieces.Count}");
                    Console.WriteLine("");

                    var h = reader.CreateFileHashList(t);
                }
            }
            catch (BencodeException e)
            {
                Console.Error.WriteLine(e);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }

            return;
        }
    }

}