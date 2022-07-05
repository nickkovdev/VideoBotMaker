using FFMpegCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerVideoMaker.Video
{
    public class VideoCreator : IVideoCreator
    {
        private string VideoContentFolder;
        private string inputFile;
        private string outputFile;
        private readonly IConfiguration _configuration;

        public VideoCreator(IConfiguration configuration)
        {
            VideoContentFolder = Directory.GetCurrentDirectory() + "\\VideoContent\\";
            _configuration = configuration;
        }
        
        public async Task ConfigureFFCore()
        {
            inputFile = GetFilesByExtension(VideoContentFolder, ".mp4", SearchOption.TopDirectoryOnly).FirstOrDefault();
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = _configuration["FFBinaryPath"], TemporaryFilesFolder = _configuration["FFTempPath"] });
            
            var videoMuted = FFMpeg.Mute(inputFile, VideoContentFolder);
            if (videoMuted)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _configuration["FFBinaryPath"] + "/ffmpeg.exe",
                    Arguments = $"-i {inputFile} -ss 00:30:00 -t 00:59:00 part1.mp4",
                    WorkingDirectory = _configuration["FFBinaryPath"],
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var process = new Process();
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
        }

        public Task DeleteFilesFromContentFolder()
        {
            DirectoryInfo di = new DirectoryInfo(VideoContentFolder);
            
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
            
            return Task.CompletedTask;
        }

        private static IEnumerable<string> GetFilesByExtension(string directoryPath, string extension, SearchOption searchOption)
        {
            return
                Directory.EnumerateFiles(directoryPath, "*" + extension, searchOption)
                    .Where(x => string.Equals(Path.GetExtension(x), extension, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
