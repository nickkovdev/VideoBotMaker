using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reddit;
using RedditAuth.AuthTokenRetriever;
using RedditAuth.AuthTokenRetriever.EventArgs;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;


namespace WorkerVideoMaker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        public const string BROWSER_PATH = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        private int port = 8080;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void GetRedditCredentials()
        {
            Console.WriteLine("Reddit.NET OAuth Trying to get Access and Refresh Token");
            AuthTokenRetrieverLib authTokenRetrieverLib = new AuthTokenRetrieverLib(_configuration["RedditClientID"], port);

            authTokenRetrieverLib.AuthSuccess += C_AuthSuccess;

            authTokenRetrieverLib.AwaitCallback();

            OpenBrowser(authTokenRetrieverLib.AuthURL());

            var rClient = new RedditClient(_configuration["RedditClientID"], "446809302741-n0JlBM4bo0FKUor2Z9SlLd6a8GBCEw");

            // Display the name and cake day of the authenticated user.
            Console.WriteLine("Username: " + rClient.Account.Me.Name);
            Console.WriteLine("Cake Day: " + rClient.Account.Me.Created.ToString("D"));

            var askReddit = rClient.Subreddit("AskReddit").About();

            authTokenRetrieverLib.StopListening();
        }

        public static void OpenBrowser(string authUrl = "about:blank")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    ProcessStartInfo processStartInfo = new ProcessStartInfo(authUrl);
                    Process.Start(processStartInfo);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    ProcessStartInfo processStartInfo = new ProcessStartInfo(BROWSER_PATH)
                    {
                        Arguments = authUrl
                    };
                    Process.Start(processStartInfo);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", authUrl);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", authUrl);
            }
        }

        public static void C_AuthSuccess(object sender, AuthSuccessEventArgs e)
        {
            Console.Clear();

            Console.WriteLine("Token retrieval successful!");

            Console.WriteLine();

            Console.WriteLine("Access Token: " + e.AccessToken);
            Console.WriteLine("Refresh Token: " + e.RefreshToken);

            Console.WriteLine();

            Console.WriteLine("Press any key to exit....");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                GetRedditCredentials();
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
