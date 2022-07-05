using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using WorkerVideoMaker.RedditAuth;
using WorkerVideoMaker.TTS;
using WorkerVideoMaker.Video;
using WorkerVideoMaker.YTAPI;

namespace WorkerVideoMaker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private IRedditApiService _redditApiService;
        private IVideoCreator _videoCreator;

        public Worker(ILogger<Worker> logger, IRedditApiService redditApiService, IVideoCreator videoCreator)
        {
            _logger = logger;
            _redditApiService = redditApiService;
            _videoCreator = videoCreator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    var authDone = await _redditApiService.AuthReddit();
                    _logger.LogInformation("Reddit Auth Done");
                    if (authDone)
                    {
                        _logger.LogInformation("Trying to recieve Reddit post");
                        var postToProcess = await _redditApiService.GetRedditPostWithComments();
                        _logger.LogInformation($"Reddit post recieved - {postToProcess.Title}");

                        _logger.LogInformation($"Time to generate files from reddit post");
                        var dataGenerationDone = await _redditApiService.ProcessRedditPost(postToProcess);
                        if (dataGenerationDone)
                        {
                            _logger.LogInformation($"Data generated");
                            _logger.LogInformation($"Time to download background video");
                            await new YouTubeAPIService().DownloadBackgroundVideo();
                            _logger.LogInformation($"Video downloaded and saved to Content folder");

                            _logger.LogInformation($"Time to create a video");
                            await _videoCreator.ConfigureFFCore();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Worker");
                }
                finally
                {
                    await _videoCreator.DeleteFilesFromContentFolder();
                }
                await Task.Delay(100000, stoppingToken);
            }
        }
    }
}
