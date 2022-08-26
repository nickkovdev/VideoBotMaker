using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using VideoLibrary;
using Microsoft.Extensions.Configuration;
using Google.Apis.YouTube.Samples;

namespace WorkerVideoMaker.YTAPI
{
    public class YouTubeAPIService : IYouTubeAPIService
    {
        private readonly IConfiguration _configuration;

        private readonly string[] Backgrounds = { "https://www.youtube.com/watch?v=Pt5_GSKIWQM", "https://www.youtube.com/watch?v=GTaXbH6iSFA" };

        private readonly string VideoContentFolder;

        public YouTubeAPIService(IConfiguration configuration)
        {
            VideoContentFolder = $@"{Directory.GetCurrentDirectory()}\VideoContent\in\";
            _configuration = configuration;
        }

        public void UploadCreatedVideo(string createdVideoFile)
        {
            UploadVideo.StartUpload(createdVideoFile);
        }

        public async Task DownloadBackgroundVideo()
        {
            try
            {
                var youTube = YouTube.Default;
                var video = await youTube.GetAllVideosAsync(Backgrounds[0]);
                var hightResVideo = video.FirstOrDefault(x => x.Resolution == 720);
                await File.WriteAllBytesAsync(VideoContentFolder + "background.mp4", await hightResVideo.GetBytesAsync());
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
    }
}
