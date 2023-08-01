# TorrentFiller
Find files for torrents so you can seed them

Selects all .torrent files in a folder, finds files with matching size *and piece hash*, copies them to a separate folder.

Fully functional,except that it probably will not find files that are smaller than the piece size.  It needs at least one piece hash to match.  That would require finding the previous and/or next files first, then hashing parts of each file.  More complicated than I want to deal with right now, especially since I'm more concerned with larger files.

In response to:

https://github.com/qbittorrent/qBittorrent/issues/6520
