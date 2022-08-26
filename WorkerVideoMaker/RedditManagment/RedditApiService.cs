using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using Reddit;
using Reddit.Controllers;
using RedditAuth.AuthTokenRetriever;
using RedditAuth.AuthTokenRetriever.EventArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorkerVideoMaker.Models;
using WorkerVideoMaker.RedditAuth.Helper;
using WorkerVideoMaker.Selenium;
using WorkerVideoMaker.TTS;

namespace WorkerVideoMaker.RedditAuth
{
    public class RedditApiService : IRedditApiService
    {
        private readonly IConfiguration _configuration;
        private readonly IAzureTextToSpeechService _textToSpeechService;
        private readonly ISeleniumService _seleniumService;

        private static readonly Random rng = new Random();

        private string RefreshToken;
        private string AccessToken;

        private string VideoContentFolder;
        private TimeSpan VideoTotalLength;

        public RedditApiService(IConfiguration configuration, IAzureTextToSpeechService textToSpeechService, ISeleniumService seleniumService)
        {
            _configuration = configuration;
            _textToSpeechService = textToSpeechService;
            _seleniumService = seleniumService;
            VideoContentFolder = $"{Directory.GetCurrentDirectory()}\\VideoContent\\in\\";
        }

        public bool AuthReddit()
        {
            bool successfulAuth = false;
            Console.WriteLine("Reddit.NET OAuth Trying to get Access and Refresh Token");
            AuthTokenRetrieverLib authTokenRetrieverLib = new AuthTokenRetrieverLib(_configuration["RedditClientID"], int.Parse(_configuration["AppPort"]));

            authTokenRetrieverLib.AuthSuccess += C_AuthSuccess;

            authTokenRetrieverLib.AwaitCallback();

            _seleniumService.RedditAutoAuth(authTokenRetrieverLib.AuthURL());

            Thread.Sleep(3000);

            if (authTokenRetrieverLib.RefreshToken != null && authTokenRetrieverLib.AccessToken != null)
            {
                RefreshToken = authTokenRetrieverLib.RefreshToken;
                AccessToken = authTokenRetrieverLib.AccessToken;
                authTokenRetrieverLib.StopListening();
                successfulAuth = true;
            }
            return successfulAuth;
        }

        public RedditPost GetRedditPostWithComments()
        {
            if (RefreshToken == null) return null;
            var rClient = new RedditClient(_configuration["RedditClientID"], RefreshToken);
            var postHelper = new PostsHelper(rClient);

            var postsList = postHelper.GetPostsFromSubreddit("AskReddit");
            var bestPost = postsList.OrderBy(x => rng.Next(0, postsList.Count)).ToList().FirstOrDefault();

            var commentHelper = new CommentHelper(rClient, bestPost);
            bestPost.CommentsList = commentHelper.GetComments();
            return bestPost;
        }

        public async Task<CreatedVideoInfo> ProcessRedditPost(RedditPost redditPost)
        {
            var videoInfo = new CreatedVideoInfo(redditPost.Title);
            try
            {
                await CreateTitleScreenshot(redditPost);

                var filteredComments = redditPost.CommentsList
                                                 .Where(y => !string.IsNullOrEmpty(y.Body) && !y.Body.Contains("deleted") && !y.Body.Contains("http") && y.Removed.Equals(false))
                                                 .OrderByDescending(x => x.UpVotes)
                                                 .ToList();

                await CreateCommentScreenshot(filteredComments);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return videoInfo;
        }

        private async Task CreateTitleScreenshot(RedditPost redditPost)
        {
            string filename;
            IWebDriver driver = _seleniumService.InitializeWebDriver();

            Console.WriteLine($"Prepearing to find a post element and take a screenshot");
            Console.WriteLine($"Redirecting to post page {redditPost.Url}");
            driver.Navigate().GoToUrl(redditPost.Url);
            IWebElement postQuestionDiv = driver.FindElement(By.CssSelector("[data-test-id='post-content']"));

            filename = VideoContentFolder + $"Question_{redditPost.PostId}.png";
            _seleniumService.SaveElementScreenshot(driver, postQuestionDiv, filename);
            Console.WriteLine($"Image of post question with filename {filename} was saved");

            driver.Dispose();

            VideoTotalLength += await _textToSpeechService.CreateAudioFromTextAndReturnLength(redditPost.Title, VideoContentFolder + $"Title_Audio_{redditPost.PostId}.wav");
            return;
        }

        private async Task CreateCommentScreenshot(List<Comment> postComments)
        {
            string filename;
            foreach (var comment in postComments)
            {
                try
                {
                    IWebDriver driver = _seleniumService.InitializeWebDriver();

                    Console.WriteLine($"Prepearing to find comment element and take a screenshot");
                    driver.Navigate().GoToUrl("https://www.reddit.com" + comment.Permalink);
                    Console.WriteLine($"Redirecting to comment permalink {comment.Permalink}");

                    driver.Manage().Window.FullScreen();

                    IWebElement commentElement = _seleniumService.TryFindTextElement(driver, comment.Body);
                    if (commentElement == null)
                    {
                        Console.WriteLine($"Could not find comment element with text {comment.Body}");
                        continue;
                    }

                    _seleniumService.AcceptCookies(driver);

                    filename = VideoContentFolder + $"Comment_{comment.Id}.png";
                    _seleniumService.SaveElementScreenshot(driver, commentElement, filename);
                    //TakeScreenshot(driver, commentElement).Save(filename, ImageFormat.Png);
                    Console.WriteLine($"Image of post question with filename {filename} was saved");

                    driver.Dispose();

                    VideoTotalLength += await _textToSpeechService.CreateAudioFromTextAndReturnLength(comment.Body, VideoContentFolder + $"Comment_Audio_{comment.Id}.wav");
                    if (VideoTotalLength.CompareTo(TimeSpan.FromSeconds(50)) > 0 || VideoTotalLength.CompareTo(TimeSpan.FromSeconds(50)) == 0)
                    {
                        break;
                    }
                }
                catch (OutOfMemoryException ex)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during comments images creation");
                    Console.WriteLine($"{ex}");
                }
            }
            return;
        }

        private static void C_AuthSuccess(object sender, AuthSuccessEventArgs e)
        {
            Console.WriteLine("Token retrieval successful!");

            Console.WriteLine();

            Console.WriteLine("Access Token: " + e.AccessToken);
            Console.WriteLine("Refresh Token: " + e.RefreshToken);

            Console.WriteLine();
        }
    }
}
