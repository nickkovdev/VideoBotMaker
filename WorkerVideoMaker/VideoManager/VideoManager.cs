using FFMpegCore;
using Microsoft.Extensions.Configuration;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace WorkerVideoMaker.Video
{
    public class VideoManager : IVideoManager
    {
        private readonly string InFolder;
        private readonly string StageFolder;
        private readonly string OutFolder;
        private readonly IConfiguration _configuration;

        public VideoManager(IConfiguration configuration)
        {
            InFolder = @$"{Directory.GetCurrentDirectory()}\VideoContent\in\";
            StageFolder = @$"{Directory.GetCurrentDirectory()}\VideoContent\staging\";
            OutFolder = @$"{Directory.GetCurrentDirectory()}\VideoContent\out\";
            _configuration = configuration;

            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = _configuration["FFBinaryPath"], TemporaryFilesFolder = _configuration["FFTempPath"] });
        }
        
        public string CreationProcess()
        {
            var resultVideo = "";
            try
            {
                var mutedVideoPath = MuteOriginalVideo();
                if (!string.IsNullOrEmpty(mutedVideoPath))
                {
                    var audioPaths = GetFilesByExtension(InFolder, ".wav", SearchOption.TopDirectoryOnly).ToList();
                    var audioFileDuration = GetTotalVideoDuration(audioPaths);
                    var cuttedVideoPath = CutVideo(mutedVideoPath, audioFileDuration);
                    
                    var webFormatVideo = CreateWebFormatVideo(cuttedVideoPath);

                    resultVideo = InsertImagesAndAudio(webFormatVideo, audioPaths);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }
            
            return resultVideo;
        }

        private string MuteOriginalVideo()
        {
            string inputFile = GetFilesByExtension(InFolder, ".mp4", SearchOption.TopDirectoryOnly).FirstOrDefault();
            string resultFilePath = InFolder + "muted.mp4";
            if (FFMpeg.Mute(inputFile, resultFilePath))
            {
                return resultFilePath;
            }
            return null;
        }

        private string CreateWebFormatVideo(string cuttedVideoPath)
        {
            var webFormatVideo = @$"{StageFolder}web.mp4";
            var startInfo = new ProcessStartInfo
            {
                FileName = _configuration["FFBinaryPath"] + "/ffmpeg.exe",
                Arguments = $"-i {Path.GetFileName(cuttedVideoPath)} -vf \"crop = ih * (9 / 16):ih\" -crf 10 -c:a copy {webFormatVideo}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = StageFolder,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            RunProcess(startInfo);

            return webFormatVideo;
        }
        
        private string InsertImagesAndAudio(string cuttedVideoPath, List<string> audioPaths)
        {
            var startInfo = new ProcessStartInfo();
            var cuttedVideoInfo = FFProbe.Analyse(cuttedVideoPath);
            
            double from = 0;
            double to = 0;
            var loopInput = "";
            audioPaths.Reverse();

            var inputArgs = new StringBuilder();
            var filterArgs = new StringBuilder();
            var additionalArgs = new StringBuilder();
            var prevDelay = 0;
            var alphabet = new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z" };

            int i = 1;
            foreach (var wavFile in audioPaths)
            {
                var loopOutput = "";

                var audioFileName = Path.GetFileNameWithoutExtension(wavFile);
                var contentId = audioFileName[(audioFileName.LastIndexOf('_') + 1)..];

                var imageInfo = Image.FromFile(Directory.GetFiles(InFolder).FirstOrDefault(x => x.Contains(contentId) && x.EndsWith(".png")));
                var audioInfo = new WaveFileReader(wavFile);

                var scaledImage = @$"{StageFolder}scaled_{contentId}.png";
                using (var newImage = ScaleImage(imageInfo, cuttedVideoInfo.PrimaryVideoStream.Width - 40, imageInfo.Height))
                {
                    var roundedImage = MakeRoundedCorners(newImage, 10);
                    roundedImage.Save(scaledImage, ImageFormat.Png);
                }

                to += audioInfo.TotalTime.TotalSeconds;
                loopOutput = $"stage_{contentId}.mp4";

                if (string.IsNullOrEmpty(loopInput)) loopInput = Path.GetFileName(cuttedVideoPath);

                startInfo = new ProcessStartInfo
                {
                    FileName = _configuration["FFBinaryPath"] + "/ffmpeg.exe",
                    Arguments = $"-i {loopInput} -i {scaledImage} -filter_complex \"[0:v][1:v] overlay =20:{(cuttedVideoInfo.PrimaryVideoStream.Height / 2) - 40}:enable = 'between(t,{from.ToString().Replace(',','.')},{to.ToString().Replace(',', '.')})'\" -pix_fmt yuv420p -c:a copy {loopOutput}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = StageFolder,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                RunProcess(startInfo);

                inputArgs.Append($"-i {wavFile} ");
                if (i != audioPaths.Count)
                {
                    var delay = ConvertSecondsToMilliseconds(audioInfo.TotalTime.TotalSeconds);
                    filterArgs = filterArgs.Append($"[{i}]adelay={delay + prevDelay}|{delay + prevDelay}[{alphabet[i]}];");
                    prevDelay += delay;
                    additionalArgs = additionalArgs.Append($"[{alphabet[i]}]");
                }

                File.Delete($@"{StageFolder + loopInput}");
                File.Delete($"{scaledImage}");

                from = to;
                loopInput = loopOutput;
                i++;
            }

            var mergedAudioFile = @$"{StageFolder}merged.wav";
            var audioMergeCommand = $"{inputArgs} -filter_complex \"{filterArgs}[0]{additionalArgs}amix=inputs={audioPaths.Count}:normalize=0\" {mergedAudioFile}";

            startInfo = new ProcessStartInfo
            {
                FileName = _configuration["FFBinaryPath"] + "/ffmpeg.exe",
                Arguments = audioMergeCommand,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = StageFolder,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            RunProcess(startInfo);

            var result = $"{OutFolder}result{prevDelay}.mp4";
            FFMpeg.ReplaceAudio(StageFolder + loopInput, mergedAudioFile, result);

            return result;
        }

        private string CutVideo(string mutedVideoPath, TimeSpan audioFileDuration)
        {
            var cuttedVideoPath = $@"{StageFolder}cutted.mp4";
            var startInfo = new ProcessStartInfo
            {
                FileName = _configuration["FFBinaryPath"] + "/ffmpeg.exe",
                Arguments = $"-ss 0 -i {mutedVideoPath} -t {Math.Ceiling(audioFileDuration.TotalSeconds)} -c copy {Path.GetFileName(cuttedVideoPath)}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = StageFolder,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            RunProcess(startInfo);

            return cuttedVideoPath;
        }

        private static TimeSpan GetTotalVideoDuration(List<string> filePaths)
        {
            var totalTime = TimeSpan.Zero;
            foreach (var file in filePaths)
            {
                WaveFileReader reader = new WaveFileReader(file);
                totalTime += reader.TotalTime;
            }
            return totalTime;
        }

        public static int ConvertSecondsToMilliseconds(double seconds)
        {
            return (int)TimeSpan.FromSeconds(seconds).TotalMilliseconds;
        }

        public void DeleteFilesFromContentFolder()
        {
            DirectoryInfo di2 = new DirectoryInfo(StageFolder);
            foreach (FileInfo file in di2.GetFiles())
            {
                file.Delete();
            }
            
            DirectoryInfo di = new DirectoryInfo(InFolder);
            foreach (FileInfo file in di.GetFiles().Where(x => !x.FullName.Contains("background.mp4")))
            {
                file.Delete();
            }
        }

        private static IEnumerable<string> GetFilesByExtension(string directoryPath, string extension, SearchOption searchOption)
        {
            return
                Directory.EnumerateFiles(directoryPath, "*" + extension, searchOption)
                    .Where(x => string.Equals(Path.GetExtension(x), extension, StringComparison.InvariantCultureIgnoreCase));
        }

        public static Image ScaleImage(Image image, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            var newImage = new Bitmap(newWidth, newHeight);

            using (var graphics = Graphics.FromImage(newImage))
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);

            return newImage;
        }

        private Bitmap MakeRoundedCorners(Image img, int radius)
        {
            Bitmap Bmp = new Bitmap(img, img.Width, img.Height);
            Graphics G = Graphics.FromImage(Bmp);
            Brush brush = new SolidBrush(Color.Red);

            for (int i = 0; i < 4; i++)
            {
                Point[] CornerUpLeft = new Point[3];

                CornerUpLeft[0].X = 0;
                CornerUpLeft[0].Y = 0;

                CornerUpLeft[1].X = radius;
                CornerUpLeft[1].Y = 0;

                CornerUpLeft[2].X = 0;
                CornerUpLeft[2].Y = radius;

                System.Drawing.Drawing2D.GraphicsPath pathCornerUpLeft =
                   new System.Drawing.Drawing2D.GraphicsPath();

                pathCornerUpLeft.AddArc(CornerUpLeft[0].X, CornerUpLeft[0].Y,
                    radius, radius, 180, 90);
                pathCornerUpLeft.AddLine(CornerUpLeft[0].X, CornerUpLeft[0].Y,
                    CornerUpLeft[1].X, CornerUpLeft[1].Y);
                pathCornerUpLeft.AddLine(CornerUpLeft[0].X, CornerUpLeft[0].Y,
                    CornerUpLeft[2].X, CornerUpLeft[2].Y);

                G.FillPath(brush, pathCornerUpLeft);
                pathCornerUpLeft.Dispose();

                Bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
            }

            brush.Dispose();
            G.Dispose();

            Color backColor = Bmp.GetPixel(0, 0);

            Bmp.MakeTransparent(backColor);

            return Bmp;
        }        

        private void RunProcess(ProcessStartInfo startInfo)
        {
            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (e, s) => { Console.WriteLine(s.Data); };
            process.ErrorDataReceived += (e, s) => { Console.WriteLine(s.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            process.Dispose();
        }
    }
}
