using System.IO;
using System.Threading.Tasks;
using VideoLibrary;

namespace WorkerVideoMaker.YTAPI
{
    public class YouTubeAPIService
    {
        private string[] Backgrounds = 
            { "https://www.youtube.com/watch?v=Pt5_GSKIWQM" ,"https://www.youtube.com/watch?v=GTaXbH6iSFA" };

        private readonly string VideoContentFolder;

        public YouTubeAPIService()
        {
            VideoContentFolder = Directory.GetCurrentDirectory() + "\\VideoContent\\";
        }
        
        public async Task DownloadBackgroundVideo()
        {
            var youTube = YouTube.Default; 
            var video = await youTube.GetVideoAsync(Backgrounds[0]);
            await File.WriteAllBytesAsync(VideoContentFolder + "background.mp4", video.GetBytes());
        }
    }
}
