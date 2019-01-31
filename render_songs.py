import argparse
import queue
import threading
import shutil
import glob
import os
import sys
import subprocess

import musicdata_tool

def get_sanitized_filename(filename, invalid_chars='<>:;\"\\/|?*'):
    for c in invalid_chars:
        filename = filename.replace(c, "_")

    return filename

def read_charts(mid, input_chart, input_sounds, metadata, output_folder="", chart_ids=[0, 1, 2, 7, 8, 9], output_format="mp3"):
    chart_labels = {
        0: "SP NORMAL",
        1: "SP HYPER",
        2: "SP ANOTHER",
        3: "SP BEGINNER",
        6: "DP NORMAL",
        7: "DP HYPER",
        8: "DP ANOTHER",
    }

    if not os.path.exists(output_folder):
        os.makedirs(output_folder)

    for i in range(0, 12):
        if i not in chart_ids or i not in chart_labels:
            continue

        id3_str = ""
        if metadata:
            id3_parts = []

            if 'artist' in metadata and metadata['artist']:
                id3_parts.append('--id3-artist "{}"'.format(metadata['artist']))

            if 'title' in metadata and metadata['title']:
                id3_parts.append('--id3-title "{}"'.format(metadata['title']))

            if 'genre' in metadata and metadata['genre']:
                id3_parts.append('--id3-genre "{}"'.format(metadata['genre']))

            if 'album' in metadata and metadata['album']:
                id3_parts.append('--id3-album "{}"'.format(metadata['album']))

            if 'album_artist' in metadata and metadata['album_artist']:
                id3_parts.append('--id3-album-artist "{}"'.format(metadata['album_artist']))

            if 'year' in metadata and metadata['year']:
                id3_parts.append('--id3-year "{}"'.format(metadata['year']))

            if 'mid' in metadata and metadata['mid']:
                id3_parts.append('--id3-track "{}"'.format(metadata['mid']))

            id3_str = " ".join(id3_parts)

            if output_format.lower() == "mp3":
                output_filename = os.path.join(output_folder, get_sanitized_filename("[%04d] %s - %s (%s).mp3" % (metadata.get('mid', ''), metadata.get('artist', ''), metadata.get('title', ''), chart_labels[i])))

            else:
                output_filename = os.path.join(output_folder, get_sanitized_filename("[%04d] %s - %s (%s).wav" % (metadata.get('mid', ''), metadata.get('artist', ''), metadata.get('title', ''), chart_labels[i])))

        else:
            if output_format.lower() == "mp3":
                output_filename = "%d.mp3" % mid

            else:
                output_filename = "%d.wav" % mid

        print("Rendering", input_chart)
        subprocess.call("2dxrender.exe -i \"%s\" -s \"%s\" -o \"%s\" -c %d %s" % (input_chart, input_sounds, output_filename, i, id3_str), shell=True)
        print("Saved", output_filename)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--songs-folder', help='Input songs folder', required=True)
    parser.add_argument('--output-folder', default="renders", help='Output folder')
    parser.add_argument('--music-database-file', default='music_data.bin', help='Input music database file')
    parser.add_argument('--charts', nargs='+', default=[2], help='Charts to render for each song', type=int)
    parser.add_argument('--id3-album', help='ID3 album', default="", type=str)
    parser.add_argument('--id3-album-artist', help='ID3 album artist', default="", type=str)
    parser.add_argument('--id3-year', help='ID3 year', default="", type=str)
    parser.add_argument('--output-format', help='Output format: WAV or MP3', default="mp3", type=str)
    parser.add_argument('--threads', help='Max number of threads to use for rendering songs', default=4, type=int)
    args = parser.parse_args()

    song_info = {}
    if os.path.exists(args.music_database_file):
        iidx_songs = musicdata_tool.extract_file(args.music_database_file, None, True)

        for song in iidx_songs.get('data', []):
            mid = song['song_id']

            song_info[mid] = {
                'artist': song['artist'],
                'title': song['title'],
                'genre': song['genre'],
                'album': args.id3_album,
                'album_artist': args.id3_album_artist,
                'year': args.id3_year,
                'mid': mid,
            }

    def worker():
        while True:
            mid = q.get()

            if mid is None:
                break

            try:
                read_charts(mid, os.path.join("songs", "{}".format(mid), "{}.1".format(mid)), os.path.join("songs", "{}".format(mid), "{}.s3p".format(mid)), song_info.get(mid, None), args.output_folder, args.charts, args.output_format)
            except:
                pass

            q.task_done()

    q = queue.Queue()
    threads = []
    for i in range(args.threads):
        t = threading.Thread(target=worker)
        t.start()
        threads.append(t)

    for root, dirnames, filenames in os.walk('songs'):
        for foldername in dirnames:
            try:
                mid = int(os.path.basename(foldername))
                q.put(mid)
            except:
                pass

    # block until all tasks are done
    q.join()

    # stop workers
    for i in range(args.threads):
        q.put(None)
    for t in threads:
        t.join()
