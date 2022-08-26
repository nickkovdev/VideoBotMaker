using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerVideoMaker.RedditAuth;
using WorkerVideoMaker.Selenium;
using WorkerVideoMaker.TTS;
using WorkerVideoMaker.Video;
using WorkerVideoMaker.YTAPI;

namespace WorkerVideoMaker
{
    public class Program
    {
        private readonly IConfiguration Configuration;

        public Program(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddSingleton<IRedditApiService, RedditApiService>();
                    services.AddSingleton<IAzureTextToSpeechService, AzureTextToSpeechService>();
                    services.AddSingleton<IVideoManager, VideoManager>();
                    services.AddSingleton<IYouTubeAPIService, YouTubeAPIService>();
                    services.AddSingleton<ISeleniumService, SeleniumService>();
                });
    }
}
