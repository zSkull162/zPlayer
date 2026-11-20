<p align="center">
  <img width="256" height="256" alt="zPlayerImage" src="https://github.com/user-attachments/assets/64bc901e-e720-45cf-bda6-3a3bb2d7c49b" />
</p>

<h1 align="center">zPlayer</h1>
<h2 align="center">A music player made for local files, and with manual editing in-mind</h2>

This is a little custom music player i made to move away from Youtube. It works by allowing you to import local music files, it will read any existing metadata, and save all changes to song or album info to a local sql database, in Appdata/Local/zPlayer.\
This program was made with Windows Presentation Foundation and .NET 9.0. Windows is and will be the only supported OS for the foreseeable future as it's what i use.

**Note: Again, all changes are made to data in a local database. This means editing song into does not change any of the file's actual tags. This will change when i make version 2 of this music player**\
For now, i recommend a program like [MP3Tag](https://www.mp3tag.de/en/) to edit the metadata before importing.


### Features
- Supports FLAC, MP3, OGG, and WAV
- Importing individual selected files, or whole folders
  - If you select "Import Folder" it does search recursively for audio files. So for example, you could select "Music Library" with a hierarchy like "Music Library/Artists/Daft Punk/Discovery/..." and it will find any music files inside Discovery, or any other folders in the Daft Punk directory
- Reads song metadata on import, such as:
  - Title
  - Artist
  - Album
  - Cover art
- Automatic and manual album creation
  - If imported songs have an album, one will be generated in the database, any any subsequent songs with the same album will be added
  - If you want to give songs an album because they didn't have one on import, you can manually create an album
- Custom album and song artwork
  - Songs will automatically use embedded artwork, but if you want to use a custom image, you may select one and it does not overwrite the embedded data
- Custom playlists (mixes)
  - Functionally similar to albums, the difference being that songs can only have one album, but can be in multiple mixes
- Playback queue with options for loop all, loop one, shuffle, and auto re-shuffle
- Listens for windows media inputs, such as Play/Pause, Skip, and Previous
- Custom color themes
- Discord rich presence
  - Customizable and toggleable. On by default

### Screenshots
<img width="1266" height="709" alt="zPlayer_Songs" src="https://github.com/user-attachments/assets/4ebf30a1-742d-438c-8e4b-2a6ebff3df27" />\
Songs list
\
\
<img width="410" height="604" alt="zPlayer_Songs_Edit" src="https://github.com/user-attachments/assets/5e58c9e7-662a-4025-a970-15cc68c7fa11" />\
Edit song menu
\
\
\
<img width="1266" height="709" alt="zPlayer_Albums" src="https://github.com/user-attachments/assets/15cb8b96-d8ee-46a8-aaa0-f6d7a44d994d" />\
Albums list
\
\
<img width="1266" height="709" alt="zPlayer_AlbumDetails" src="https://github.com/user-attachments/assets/0fd92b6e-f70c-4776-9c57-8a9be0c98c71" />\
Album details menu
\
\
<img width="1266" height="709" alt="zPlayer_MixDetails" src="https://github.com/user-attachments/assets/6ac354e5-5353-4ca3-acb9-cd40769447a5" />\
Mix details menu
\
\
\
<img width="1266" height="709" alt="zPlayer_PlaybackQueue" src="https://github.com/user-attachments/assets/16b75442-305b-49a5-a469-6869e721fa91" />\
Preview of the playback queue
\
\
\
<img width="1266" height="922" alt="zPlayer_Settings" src="https://github.com/user-attachments/assets/d65e5e90-51b8-420a-a747-af04cc422f19" />\
Settings menu
\
\
<img width="1073" height="395" alt="zPlayer_Settings_RPC" src="https://github.com/user-attachments/assets/563b0a8a-bb2c-47c3-8595-c9ad4e988e13" />\
Discord RPC settings
\
\
\
<img width="1266" height="713" alt="zPlayer_CustomTheme" src="https://github.com/user-attachments/assets/12d881e9-92d0-468e-b2cc-6867c3f1041f" />\
Example of custom theme colors
