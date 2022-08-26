using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using WorkerVideoMaker.RedditAuth;
using WorkerVideoMaker.Selenium;
using WorkerVideoMaker.Video;
using WorkerVideoMaker.YTAPI;

namespace WorkerVideoMaker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private IRedditApiService _redditApiService;
        private IVideoManager _videoManager;
        private IYouTubeAPIService _youTubeAPIService;
        private ISeleniumService _seleniumService;

        public Worker(ILogger<Worker> logger, IRedditApiService redditApiService, IVideoManager videoManager, IYouTubeAPIService youTubeAPIService, ISeleniumService seleniumService)
        {
            _logger = logger;
            _redditApiService = redditApiService;
            _videoManager = videoManager;
            _youTubeAPIService = youTubeAPIService;
            _seleniumService = seleniumService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var authDone = false;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _videoManager.DeleteFilesFromContentFolder();
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    if (!authDone)
                    {
                        authDone = _redditApiService.AuthReddit();
                    }

                    _logger.LogInformation("Reddit Auth Done");
                    if (authDone)
                    {
                        _logger.LogInformation("Trying to recieve Reddit post");
                        var postToProcess = _redditApiService.GetRedditPostWithComments();
                        _logger.LogInformation($"Reddit post recieved - {postToProcess.Title}");

                        _logger.LogInformation($"Time to generate files from reddit post");
                        var videoInfo = await _redditApiService.ProcessRedditPost(postToProcess);
                        _logger.LogInformation($"Data generated");
                        
                        _logger.LogInformation($"Time to download background video");
                        //await _youTubeAPIService.DownloadBackgroundVideo();
                        _logger.LogInformation($"Video downloaded and saved to Content folder");

                        _logger.LogInformation($"Time to create a video");
                        var resultVideoPath = _videoManager.CreationProcess();

                        videoInfo.Path = resultVideoPath;

                        _logger.LogInformation($"Video creation finished, lets try to upload it");
                        //_youTubeAPIService.UploadCreatedVideo(resultVIdeoPath);
                        _seleniumService.YoutubeUpload(videoInfo);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Worker");
                }
                finally
                {
                    _videoManager.DeleteFilesFromContentFolder();
                }
            }
        }
    }
}
