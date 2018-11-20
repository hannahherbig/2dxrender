import argparse
import ctypes
import json
import sys
import struct


def read_string(infile, length, encoding='shift-jis'):
    return infile.read(length).decode(encoding).strip('\0')

def write_string(outfile, input, length, fill='\0', encoding='shift-jis'):
    string_data = input[:length].encode(encoding)
    outfile.write(string_data)

    if len(input) < length:
        outfile.write("".join([fill] * (length - len(string_data))).encode(encoding))

# Cannonballers reader/writer
def reader_19(infile, song_count):
    song_entries = []

    for i in range(song_count):
        title = read_string(infile, 0x40)
        title_ascii = read_string(infile, 0x40)
        genre = read_string(infile, 0x40)
        artist = read_string(infile, 0x40)

        texture_title, texture_artist, texture_genre, texture_load, texture_list = struct.unpack("<IIIII", infile.read(20))
        font_idx, game_version = struct.unpack("<IH", infile.read(6))
        other_folder, bemani_folder, splittable_diff = struct.unpack("<HHH", infile.read(6))

        difficulties = [x for x in infile.read(8)]

        unk_sect1 = infile.read(0xa0)

        song_id, volume = struct.unpack("<II", infile.read(8))
        file_identifiers = [x for x in infile.read(8)]

        bga_delay = ctypes.c_short(struct.unpack("<H", infile.read(2))[0]).value
        unk_sect2 = infile.read(2)
        bga_filename = read_string(infile, 0x20)

        afp_flag = struct.unpack("<I", infile.read(4))[0]

        afp_data = []
        for x in range(10):
            afp_data.append(infile.read(0x20).hex())

        song_entries.append({
            'song_id': song_id,
            'title': title,
            'title_ascii': title_ascii,
            'genre': genre,
            'artist': artist,
            'texture_title': texture_title,
            'texture_artist': texture_artist,
            'texture_genre': texture_genre,
            'texture_load': texture_load,
            'texture_list': texture_list,
            'font_idx': font_idx,
            'game_version': game_version,
            'other_folder': other_folder,
            'bemani_folder': bemani_folder,
            'splittable_diff': splittable_diff,
            'difficulties': difficulties,
            'volume': volume,
            'file_identifiers': file_identifiers,
            'bga_filename': bga_filename,
            'bga_delay': bga_delay,
            'afp_flag': afp_flag,
            'afp_data': afp_data,
            'unk_sect1': unk_sect1.hex(),
            'unk_sect2': unk_sect2.hex(),
        })

    return song_entries


def writer_19(outfile, data):
    DATA_VERSION = 25
    MAX_ENTRIES = 26000
    CUR_STYLE_ENTRIES = MAX_ENTRIES - 1000

    # Write header
    outfile.write(b"IIDX")
    outfile.write(struct.pack("<IHHI", DATA_VERSION, len(data), MAX_ENTRIES, 0))

    # Write song index table
    exist_ids = {}
    for song_data in data:
        exist_ids[song_data['song_id']] = True

    cur_song = 0
    for i in range(MAX_ENTRIES):
        if i in exist_ids:
            outfile.write(struct.pack("<H", cur_song))
            cur_song += 1
        elif i >= CUR_STYLE_ENTRIES:
            outfile.write(struct.pack("<H", 0x0000))
        else:
            outfile.write(struct.pack("<H", 0xffff))

    # Write song entries
    for song_data in data:
        write_string(outfile, song_data['title'], 0x40)
        write_string(outfile, song_data['title_ascii'], 0x40)
        write_string(outfile, song_data['genre'], 0x40)
        write_string(outfile, song_data['artist'], 0x40)

        outfile.write(struct.pack("<IIIII", song_data['texture_title'], song_data['texture_artist'], song_data['texture_genre'], song_data['texture_load'], song_data['texture_list']))
        outfile.write(struct.pack("<IH", song_data['font_idx'], song_data['game_version']))
        outfile.write(struct.pack("<HHH", song_data['other_folder'], song_data['bemani_folder'], song_data['splittable_diff']))

        for difficulty in song_data['difficulties']:
            outfile.write(struct.pack("<B", difficulty))

        outfile.write(bytes.fromhex(song_data['unk_sect1']))

        outfile.write(struct.pack("<II", song_data['song_id'], song_data['volume']))

        for ident in song_data['file_identifiers']:
            outfile.write(struct.pack("<B", ident))

        outfile.write(struct.pack("<h", song_data['bga_delay']))
        outfile.write(bytes.fromhex(song_data['unk_sect2']))
        write_string(outfile, song_data['bga_filename'], 0x20)

        outfile.write(struct.pack("<I", song_data['afp_flag']))

        for afp_data in song_data['afp_data']:
            outfile.write(bytes.fromhex(afp_data))


read_handlers = {
    0x19: reader_19,
}

write_handlers = {
    0x19: writer_19,
}


def extract_file(input, output, in_memory=False):
    with open(input, "rb") as infile:
        if infile.read(4) != b"IIDX":
            print("Invalid", input)
            exit(-1)

        data_ver, available_entries, total_entries, unk4 = struct.unpack("<IHIH", infile.read(12))

        song_ids = {}
        for i in range(total_entries):
            song_id = struct.unpack("<H", infile.read(2))[0]

            if song_id != 0xffff and (len(song_ids) == 0 or song_id != 0):
                song_ids[i] = song_id

        if data_ver in read_handlers:
            output_data = read_handlers[data_ver](infile, available_entries)
            output_data = {
                'data_ver': data_ver,
                'data': output_data,
            }

            if in_memory:
                return output_data

            json.dump(output_data, open(output, "w", encoding="utf8"), indent=4, ensure_ascii=False)
        else:
            print("Couldn't find a handler for this data version")
            exit(-1)

    return []


def create_file(input, output, data_version):
    data = json.load(open(input, "r", encoding="utf8"))
    data_ver = data.get('data_ver', data_version)

    if not data_ver:
        print("Couldn't find data version")
        exit(-1)

    if data_ver in write_handlers:
        write_handlers[data_ver](open(output, "wb"), data['data'])
    else:
        print("Couldn't find a handler for this data version")
        exit(-1)


def convert_file(input, output, data_version):
    with open(input, "rb") as infile:
        if infile.read(4) != b"IIDX":
            print("Invalid", input)
            exit(-1)

        data_ver, available_entries, total_entries, unk4 = struct.unpack("<IHIH", infile.read(12))

        song_ids = {}
        for i in range(total_entries):
            song_id = struct.unpack("<H", infile.read(2))[0]

            if song_id != 0xffff and (len(song_ids) == 0 or song_id != 0):
                song_ids[i] = song_id

        if data_ver in read_handlers:
            output_data = read_handlers[data_ver](infile, available_entries)
            write_handlers[data_version](open(output, "wb"), output_data)
        else:
            print("Couldn't find a handler for this input data version")
            exit(-1)


def merge_files(input, basefile, output):
    with open(input, "rb") as infile:
        if infile.read(4) != b"IIDX":
            print("Invalid", input)
            exit(-1)

        data_ver, available_entries, total_entries, unk4 = struct.unpack("<IHIH", infile.read(12))

        song_ids = {}
        for i in range(total_entries):
            song_id = struct.unpack("<H", infile.read(2))[0]

            if song_id != 0xffff and (len(song_ids) == 0 or song_id != 0):
                song_ids[i] = song_id

        if data_ver in read_handlers:
            old_data = read_handlers[data_ver](infile, available_entries)
        else:
            print("Couldn't find a handler for this input data version")
            exit(-1)

    with open(basefile, "rb") as infile:
        if infile.read(4) != b"IIDX":
            print("Invalid", basefile)
            exit(-1)

        data_ver, available_entries, total_entries, unk4 = struct.unpack("<IHIH", infile.read(12))

        song_ids = {}
        for i in range(total_entries):
            song_id = struct.unpack("<H", infile.read(2))[0]

            if song_id != 0xffff and (len(song_ids) == 0 or song_id != 0):
                song_ids[i] = song_id

        if data_ver in read_handlers:
            new_data = read_handlers[data_ver](infile, available_entries)
        else:
            print("Couldn't find a handler for this input data version")
            exit(-1)

    # Create list of
    exist_ids_new = {}
    for song_data in new_data:
        exist_ids_new[song_data['song_id']] = True

    for song_data in old_data:
        if song_data['song_id'] not in exist_ids_new:
            new_data.append(song_data)

    write_handlers[data_ver](open(output, "wb"), new_data)



if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', help='Input file', required=True)
    parser.add_argument('--output', help='Output file', required=True)
    parser.add_argument('--extract', help='Extraction mode', default=False, action='store_true')
    parser.add_argument('--create', help='Creation mode', default=False, action='store_true')
    parser.add_argument('--convert', help='Conversion mode', default=False, action='store_true')
    parser.add_argument('--merge', help='Merge mode', default=False, action='store_true')
    parser.add_argument('--data-version', help='Force a data version (usedful for converts)', default=None, type=int)
    args = parser.parse_args()

    if args.create == False and args.extract == False and args.convert == False and args.merge == False:
        print("You must specify either --extract or --create or --convert or --merge")
        exit(-1)

    if args.convert == True:
        if args.data_version == None:
            print("You must specify a target --data-version with --convert")
            exit(-1)
        elif args.data_version not in write_handlers:
            print("Don't know how to handle specified data version")
            exit(-1)

    if args.extract:
        extract_file(args.input, args.output)

    elif args.create:
        create_file(args.input, args.output, args.data_version)

    elif args.convert:
        convert_file(args.input, args.output, args.data_version)

    elif args.merge:
        merge_files(args.input, args.output, args.output)