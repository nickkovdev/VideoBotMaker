using System.Threading.Tasks;

namespace WorkerVideoMaker.YTAPI
{
    public interface IYouTubeAPIService
    {
        void UploadCreatedVideo(string createdVideoFile);
        Task DownloadBackgroundVideo();
    }
}