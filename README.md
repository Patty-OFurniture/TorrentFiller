# TorrentFiller
Find files for torrents so you can seed them

Selects all .torrent files in a folder, finds files with matching size *and piece hash*, copies them to a separate folder.

Fully functional.  It needs at least one piece hash to match, unless the file size is less than the piece size.

Windows version in HashTester.zip prompts for the folder with the .torrent and files, and copies them to the local path.  Couldn't be simpler, and it's *fast*.

In response to:

https://github.com/qbittorrent/qBittorrent/issues/6520
