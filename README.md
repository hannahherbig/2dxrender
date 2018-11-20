# 2dxrender
Render IIDX charts into audio files

## 2dxrender.exe
```
cmd> 2dxrender.exe --help
2dxrender 1.0.0.0
Copyright c  2018

  -i, --input-chart          Required. Input .1 chart file

  -s, --input-audio          Required. Input .s3p/.2dx audio archive or audio folder

  -c, --chart                (Default: 2) Chart ID to render

  -n, --no-bgm               (Default: false) Render without BGM

  -r, --volume               (Default: 0.85) Render volume (1.0 = 100%)

  -a, --assist-clap          (Default: false) Enable assist clap sounds

  -p, --assist-clap-sound    (Default: clap.wav) Assist clap sound file

  -k, --volume-clap          (Default: 1.25) Assist clap render volume (1.0 = 100%)

  -o, --output               Required. Output file

  -f, --output-format        (Default: mp3) Output file format (WAV or MP3)

  --id3-album-art            (Default: ) ID3 album art

  --id3-album                (Default: ) ID3 album tag

  --id3-album-artist         (Default: ) ID3 album artist tag

  --id3-artist               (Default: ) ID3 artist tag

  --id3-title                (Default: ) ID3 title tag

  --id3-year                 (Default: ) ID3 year tag

  --id3-genre                (Default: ) ID3 genre tag

  --id3-track                (Default: ) ID3 track tag

  --help                     Display this help screen.

  --version                  Display version information.

```

- Currently only .1 charts from `beatmania IIDX 25 CANNON BALLERS` are supported
- You can specify a .2dx, .s3p, or folder for the input keysounds
- MP3 is restricted to CBR 320 at the moment due to a bug with NAudio.Lame
- Requires .NET 4.0

## Mass converter (render_songs.py)
```
cmd> python3 render_songs.py --help
usage: render_songs.py [-h] --songs-folder SONGS_FOLDER
                       [--output-folder OUTPUT_FOLDER]
                       [--music-database-file MUSIC_DATABASE_FILE]
                       [--charts CHARTS [CHARTS ...]] [--id3-album ID3_ALBUM]
                       [--id3-album-artist ID3_ALBUM_ARTIST]
                       [--id3-year ID3_YEAR] [--id3-album-art ID3_ALBUM_ART]
                       [--output-format OUTPUT_FORMAT]

optional arguments:
  -h, --help            show this help message and exit
  --songs-folder SONGS_FOLDER
                        Input songs folder
  --output-folder OUTPUT_FOLDER
                        Output folder
  --music-database-file MUSIC_DATABASE_FILE
                        Input music database file
  --charts CHARTS [CHARTS ...]
                        Charts to render for each song
  --id3-album ID3_ALBUM
                        ID3 album
  --id3-album-artist ID3_ALBUM_ARTIST
                        ID3 album artist
  --id3-year ID3_YEAR   ID3 year
  --id3-album-art ID3_ALBUM_ART
                        ID3 album art
  --output-format OUTPUT_FORMAT
                        Output format: WAV or MP3
```

- Automatically tag things if you point it at your music_data.bin file using `--music-database-file`
- Requires at least Python 3.4 (tested with 3.6, but 3.4 should be fine. Lower may or may not work)
- Valid charts IDs:
```
0: "SP NORMAL",
1: "SP HYPER",
2: "SP ANOTHER",
3: "SP BEGINNER",
6: "DP NORMAL",
7: "DP HYPER",
8: "DP ANOTHER",
```
