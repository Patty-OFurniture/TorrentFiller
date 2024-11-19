# TorrentFiller
Find files for torrents.  Who wants more features, like filling in missing hash pieces when found?  Adding small files to the hash checks?  I DO.

I mean, we could go super crazy here.  But not that many people use torrents with single files randomly selected from wallpaper sites these days.

Selects all .torrent files in a folder, finds files with matching size *and piece hash*, copies them to a separate folder.

Fully functional.  It needs at least one piece hash to match.  If the file size is less than the piece size, that file cannot be matched.

Is it safe?  Well,

1) It does not download any files
2) It does not overwrite any files
3) It does not delete any files
4) I'm assuming if you are using this, you have opened the torrent in an actual torrent application.  If you are paranoid, export from there first.
5) Fixes from trying to parse libtorrent-raster are done.

Windows version in HashTester.zip prompts for the folder with the .torrent and files, and copies them to the local path.  Couldn't be simpler, and it's *fast*.

In response to:

https://github.com/qbittorrent/qBittorrent/issues/6520
