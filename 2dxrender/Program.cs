using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio;
using NAudio.WindowsMediaFormat;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Lame;
using System.IO;

using CommandLine;

using Newtonsoft.Json;

namespace _2dxrender
{
    class Program
    {
        enum ChartCommands
        {
            KeyP1 = 0,
            KeyP2 = 1,
            LoadSampleP1 = 2,
            LoadSampleP2 = 3,
            End = 6,
            BgmNote = 7,
        }

        class KeyPosition
        {
            public int offset;
            public int keysoundId;
            public int key;
            public int player;

            public KeyPosition(int offset, int keysoundId, int key, int player)
            {
                this.offset = offset;
                this.keysoundId = keysoundId;
                this.key = key;
                this.player = player;
            }
        }

        static Options options = new Options();
        static int assistClapIdx = -1;

        private static string GetTempFileName()
        {
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        static List<string> getAudioSamplesFromS3p(BinaryReader reader)
        {
            var samples = new List<string>();

            var fileCount = reader.ReadUInt32();

            var tempPath = GetTempFileName();

            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            for (var i = 0; i < fileCount; i++)
            {
                reader.BaseStream.Seek(i * 8 + 8, SeekOrigin.Begin);

                var offset = reader.ReadUInt32();
                var size = reader.ReadInt32();

                reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "S3V0")
                {
                    Console.WriteLine("Not a valid S3V audio file");
                    Environment.Exit(-1);
                }

                var dataOffset = reader.ReadUInt32();
                var dataSize = reader.ReadInt32();

                reader.BaseStream.Seek(offset + dataOffset, SeekOrigin.Begin);

                var audioBytes = reader.ReadBytes(dataSize);
                var tempFilename = Path.Combine(tempPath, String.Format("{0:d4}.wma", i + 1));

                samples.Add(tempFilename);

                File.WriteAllBytes(tempFilename, audioBytes);
            }

            return samples;
        }

        static List<string> getAudioSamplesFrom2dx(BinaryReader reader)
        {
            var samples = new List<string>();

            reader.BaseStream.Seek(0x14, SeekOrigin.Begin);

            var fileCount = reader.ReadUInt32();

            reader.BaseStream.Seek(0x48, SeekOrigin.Begin);

            var tempPath = Path.GetTempPath();
            for (var i = 0; i < fileCount; i++)
            {
                reader.BaseStream.Seek(0x48 + i * 4, SeekOrigin.Begin);

                var offset = reader.ReadUInt32();

                reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "2DX9")
                {
                    Console.WriteLine("Not a valid 2DX audio file @ {0:x8}", reader.BaseStream.Position - 4);
                    Environment.Exit(-1);
                }

                var dataOffset = reader.ReadUInt32();
                var dataSize = reader.ReadInt32();

                reader.BaseStream.Seek(offset + dataOffset, SeekOrigin.Begin);

                var audioBytes = reader.ReadBytes(dataSize);
                var tempFilename = Path.Combine(tempPath, String.Format("{0:d4}.wav", i + 1));

                samples.Add(tempFilename);

                File.WriteAllBytes(tempFilename, audioBytes);
            }

            return samples;
        }

        static List<string> getAudioSamples(string inputFilename)
        {
            var samples = new List<string>();

            FileAttributes attr = File.GetAttributes(inputFilename);

            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                samples = Directory.GetFiles(inputFilename).OrderBy(x => x).ToList();
            }
            else
            {
                using (var reader = new BinaryReader(File.Open(inputFilename, FileMode.Open)))
                {
                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) == "S3P0")
                    {
                        samples = getAudioSamplesFromS3p(reader);
                    }
                    else
                    {
                        try
                        {
                            // Try parsing as .2dx
                            samples = getAudioSamplesFrom2dx(reader);
                        }
                        catch
                        {
                            Console.WriteLine("Couldn't find parser for input audio file: {0}", inputFilename);
                        }
                    }
                }
            }

            if (options.AssistClap)
            {
                if (File.Exists(options.AssistClapSound))
                {
                    var assistClapTempFilename = GetTempFileName();
                    assistClapIdx = samples.Count;
                    samples.Add(assistClapTempFilename);
                    File.Copy(options.AssistClapSound, assistClapTempFilename);
                }
                else
                {
                    Console.WriteLine("Couldn't find clap file \"{0}\", skipping...", options.AssistClapSound);
                    options.AssistClap = false;
                }
            }

            return samples;
        }

        private static int findSampleById(List<string> samples, int value, string prefix)
        {
            for (int idx = 0; idx < samples.Count; idx++)
            {
                var sample = samples[idx];

                if (sample == Path.Combine(prefix, String.Format("{0:d4}.wav", value)) ||
                    sample == Path.Combine(prefix, String.Format("{0:d4}.wma", value)))
                {
                    return idx;
                }
            }

            return -1;
        }

        private static List<KeyPosition> parseJsonChartData(string filename, List<string> samples)
        {
            var sounds = new List<KeyPosition>();
            var loaded_samples = new List<Dictionary<short, int>>(){
                new Dictionary<short, int>(),
                new Dictionary<short, int>()
            };

            var json = File.ReadAllText(filename);
            dynamic _chartData = JsonConvert.DeserializeObject(json);
            dynamic chartData = _chartData["charts"][0];

            var soundFolder = Path.GetDirectoryName(samples[0]);

            foreach (var events in chartData["events"])
            {
                foreach (var _ev in events)
                {
                    for (int eventidx = 0; eventidx < _ev.Count; eventidx++)
                    {
                        var ev = _ev[eventidx];

                        int offset = ev["offset"];

                        if ((ev["event"] == "note_p1" || ev["event"] == "note_p2") && offset != 0)
                        {
                            var playerId = ev["event"] == "note_p1" ? 0 : 1;
                            int value = ev["value"];
                            short param = ev["slot"];

                            if (value != 0 && param == 7)
                            {
                                sounds.Add(new KeyPosition(offset + value, loaded_samples[playerId][param], param, playerId));

                                if (options.AssistClap)
                                {
                                    sounds.Add(new KeyPosition(offset + value, assistClapIdx, -1, 0));
                                }
                            }

                            if (options.AssistClap && param == 7)
                            {
                                sounds.Add(new KeyPosition(offset, assistClapIdx, -1, 0));
                            }

                            sounds.Add(new KeyPosition(offset, loaded_samples[playerId][param], param, playerId));
                        }
                        else if (ev["event"] == "sample_p1" || ev["event"] == "sample_p2")
                        {
                            var playerId = ev["event"] == "sample_p1" ? 0 : 1;
                            int value = ev["sound_id"];
                            short param = ev["slot"];

                            loaded_samples[playerId][param] = findSampleById(samples, value, soundFolder);

                            // To simulate the engine loading a new sample before it's played, loop through and update everything
                            // for that specific key from the current offset onward
                            for (int i = 0; i < sounds.Count; i++)
                            {
                                if (sounds[i].player == playerId && sounds[i].key == param && sounds[i].offset >= offset)
                                {
                                    sounds[i].keysoundId = loaded_samples[playerId][param];
                                }
                            }
                        }
                        else if (ev["event"] == "auto")
                        {
                            int value = ev["sound_id"];
                            int sample_idx = findSampleById(samples, value, soundFolder);

                            if (sample_idx == 0 && options.NoBgm)
                            {
                                continue;
                            }

                            sounds.Add(new KeyPosition(offset, sample_idx, -1, 0));
                        }
                        else if (ev["event"] == "end")
                        {
                            int value = ev["player"];
                            sounds.Add(new KeyPosition(offset, -1, -1, value));
                        }
                    }
                }
            }

            return sounds;
        }

        private static List<KeyPosition> parseChartData(BinaryReader reader, List<string> samples)
        {
            var sounds = new List<KeyPosition>();
            var loaded_samples = new List<Dictionary<short, int>>(){
                new Dictionary<short, int>(),
                new Dictionary<short, int>()
            };

            while (true)
            {
                var offset = reader.ReadInt32();
                var command = reader.ReadByte();
                var param = reader.ReadByte();
                var value = reader.ReadInt16();

                if (offset == 0x7fffffff)
                {
                    break;
                }

                switch (command)
                {
                    case (byte)ChartCommands.KeyP1:
                    case (byte)ChartCommands.KeyP2:
                        {
                            var playerId = command - (byte)ChartCommands.KeyP1;

                            if (value != 0 && param == 7)
                            {
                                sounds.Add(new KeyPosition(offset + value, loaded_samples[playerId][param], param, playerId));

                                if (options.AssistClap)
                                {
                                    sounds.Add(new KeyPosition(offset + value, assistClapIdx, -1, 0));
                                }
                            }

                            if (options.AssistClap && param == 7)
                            {
                                sounds.Add(new KeyPosition(offset, assistClapIdx, -1, 0));
                            }

                            sounds.Add(new KeyPosition(offset, loaded_samples[playerId][param], param, playerId));
                        }
                        break;

                    case (byte)ChartCommands.LoadSampleP1:
                    case (byte)ChartCommands.LoadSampleP2:
                        {
                            var playerId = command - (byte)ChartCommands.LoadSampleP1;

                            loaded_samples[playerId][param] = value - 1;

                            // To simulate the engine loading a new sample before it's played, loop through and update everything
                            // for that specific key from the current offset onward
                            for (int i = 0; i < sounds.Count; i++)
                            {
                                if (sounds[i].player == playerId && sounds[i].key == param && sounds[i].offset >= offset)
                                {
                                    sounds[i].keysoundId = loaded_samples[playerId][param];
                                }
                            }
                        }
                        break;

                    case (byte)ChartCommands.BgmNote:
                        {
                            if (value - 1 == 0 && options.NoBgm)
                            {
                                continue;
                            }

                            sounds.Add(new KeyPosition(offset, value - 1, -1, 0));
                        }
                        break;

                    case (byte)ChartCommands.End:
                        {
                            sounds.Add(new KeyPosition(offset, -1, -1, param));
                        }
                        break;
                }
            }

            return sounds;
        }

        static void parseChart(string filename, int chartId, string outputFilename, List<string> samples)
        {
            if (chartId < 0 || chartId > 0x60 / 8)
            {
                Console.WriteLine("Invalid chart ID");
                goto cleanup;
            }

            if (filename.EndsWith(".json"))
            {
                var sounds = parseJsonChartData(filename, samples);
                mixFinalAudio(outputFilename, sounds, samples);
                return;
            }

            using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open)))
            {
                reader.BaseStream.Seek(chartId * 8, SeekOrigin.Begin);

                var offset = reader.ReadUInt32();
                var filesize = reader.ReadInt32();

                reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                var sounds = parseChartData(reader, samples);
                mixFinalAudio(outputFilename, sounds, samples);
            }

            cleanup:
            foreach (var sampleFilename in samples)
            {
                File.Delete(sampleFilename);
            }
        }

        private static void mixFinalAudio(string outputFilename, List<KeyPosition> sounds, List<string> samples)
        {
            var mixedSamples = new List<OffsetSampleProvider>();

            // Find P1 and P2 ends
            int[] playerEnd = new int[2];
            foreach (var sound in sounds)
            {
                if (sound.keysoundId == -1 && sound.key == -1)
                {
                    playerEnd[sound.player] = sound.offset;
                }
            }

            foreach (var sound in sounds)
            {
                if (sound.keysoundId == -1)
                    continue;

                var audioFile = new AudioFileReader(samples[sound.keysoundId]);
                var volSample = new VolumeSampleProvider(audioFile);

                if (volSample.WaveFormat.Channels == 1)
                {
                    volSample = new VolumeSampleProvider(volSample.ToStereo());
                }

                if (volSample.WaveFormat.SampleRate != 44100)
                {
                    // Causes pop sound at end of audio
                    volSample = new VolumeSampleProvider(
                        new WaveToSampleProvider(
                            new MediaFoundationResampler(
                               new SampleToWaveProvider(volSample),
                               WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)
                            ) {
                                ResamplerQuality = 60
                            }
                        )
                    );
                }

                if (options.AssistClap && sound.keysoundId == assistClapIdx)
                {
                    volSample.Volume = options.AssistClapVolume;
                }
                else
                {
                    volSample.Volume = options.RenderVolume;
                }

                var sample = new OffsetSampleProvider(volSample);
                sample.DelayBy = TimeSpan.FromMilliseconds(sound.offset);

                if (sound.player >= 0 && sound.player <= 1 && sound.offset + audioFile.TotalTime.TotalMilliseconds > playerEnd[sound.player])
                {
                    sample.Take = TimeSpan.FromMilliseconds(playerEnd[sound.player] - sound.offset);
                }

                mixedSamples.Add(sample);
            }

            var mixers = new List<MixingSampleProvider>();
            for (int i = 0; i < mixedSamples.Count; i += 128)
            {
                mixers.Add(new MixingSampleProvider(mixedSamples.Skip(i).Take(128).ToArray()));
            }

            var mixer = new MixingSampleProvider(mixers);

            if (options.OutputFormat.ToLower() == "wav")
            {
                WaveFileWriter.CreateWaveFile16(outputFilename, mixer);
            }
            else if (options.OutputFormat.ToLower() == "mp3")
            {
                var tempFilename = GetTempFileName();

                WaveFileWriter.CreateWaveFile16(tempFilename, mixer);

                ID3TagData id3 = new ID3TagData();
                id3.Album = options.Id3Album;
                id3.AlbumArtist = options.Id3AlbumArtist;
                id3.Title = options.Id3Title;
                id3.Artist = options.Id3Artist;
                id3.Genre = options.Id3Genre;
                id3.Track = options.Id3Track;
                id3.Year = options.Id3Year;

                using (var reader = new AudioFileReader(tempFilename))
                using (var writer = new LameMP3FileWriter(outputFilename, reader.WaveFormat, 320, id3))
                {
                    reader.CopyTo(writer);
                }

                File.Delete(tempFilename);
            }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(opts => options = opts).WithNotParsed(errors => Environment.Exit(1));

            var samples = getAudioSamples(options.InputAudio);
            parseChart(options.InputChart, options.ChartId, options.OutputFile, samples);
        }
    }
}
