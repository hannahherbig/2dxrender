using CommandLine;

namespace _2dxrender
{
    class Options
    {
        [Option('i', "input-chart", Required = true, HelpText = "Input .1 chart file")]
        public string InputChart { get; set; }

        [Option('s', "input-audio", Required = true, HelpText = "Input .s3p/.2dx audio archive or audio folder")]
        public string InputAudio { get; set; }

        [Option('c', "chart", Default = 2, HelpText = "Chart ID to render")]
        public int ChartId { get; set; }

        [Option('n', "no-bgm", Default = false, HelpText = "Render without BGM")]
        public bool NoBgm { get; set; }

        [Option('r', "volume", Default = 0.85f, HelpText = "Render volume (1.0 = 100%)")]
        public float RenderVolume { get; set; }

        [Option('a', "assist-clap", Default = false, HelpText = "Enable assist clap sounds")]
        public bool AssistClap { get; set; }

        [Option('p', "assist-clap-sound", Default = "clap.wav", HelpText = "Assist clap sound file")]
        public string AssistClapSound { get; set; }

        [Option('k', "volume-clap", Default = 1.25f, HelpText = "Assist clap render volume (1.0 = 100%)")]
        public float AssistClapVolume { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output file")]
        public string OutputFile { get; set; }

        [Option('f', "output-format", Default = "mp3", HelpText = "Output file format (WAV or MP3)")]
        public string OutputFormat { get; set; }

        [Option("id3-album", Default = "", HelpText = "ID3 album tag")]
        public string Id3Album { get; set; }

        [Option("id3-album-artist", Default = "", HelpText = "ID3 album artist tag")]
        public string Id3AlbumArtist { get; set; }

        [Option("id3-artist", Default = "", HelpText = "ID3 artist tag")]
        public string Id3Artist { get; set; }

        [Option("id3-title", Default = "", HelpText = "ID3 title tag")]
        public string Id3Title { get; set; }

        [Option("id3-year", Default = "", HelpText = "ID3 year tag")]
        public string Id3Year{ get; set; }

        [Option("id3-genre", Default = "", HelpText = "ID3 genre tag")]
        public string Id3Genre { get; set; }

        [Option("id3-track", Default = "", HelpText = "ID3 track tag")]
        public string Id3Track { get; set; }
    }
}
