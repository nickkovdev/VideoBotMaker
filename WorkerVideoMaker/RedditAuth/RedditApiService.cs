using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support;
using Reddit;
using Reddit.Controllers;
using RedditAuth.AuthTokenRetriever;
using RedditAuth.AuthTokenRetriever.EventArgs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorkerVideoMaker.Models;
using WorkerVideoMaker.RedditAuth.Helper;
using WorkerVideoMaker.TTS;
using WorkerVideoMaker.YTAPI;

namespace WorkerVideoMaker.RedditAuth
{
    public class RedditApiService : IRedditApiService
    {
        private readonly IConfiguration _configuration;
        private IAzureTextToSpeechService _textToSpeechService;
        private static Random rng = new Random();

        private string RefreshToken;
        private string AccessToken;

        private string VideoContentFolder;
        private bool CookiesAccepted = false;
        private TimeSpan VideoTotalLength;

        public RedditApiService(IConfiguration configuration, IAzureTextToSpeechService textToSpeechService)
        {
            _configuration = configuration;
            _textToSpeechService = textToSpeechService;
            VideoContentFolder = Directory.GetCurrentDirectory() + "\\VideoContent\\";
        }

        public async Task<bool> AuthReddit()
        {
            bool successfulAuth = false;
            Console.WriteLine("Reddit.NET OAuth Trying to get Access and Refresh Token");
            AuthTokenRetrieverLib authTokenRetrieverLib = new AuthTokenRetrieverLib(_configuration["RedditClientID"], int.Parse(_configuration["AppPort"]));

            authTokenRetrieverLib.AuthSuccess += C_AuthSuccess;

            authTokenRetrieverLib.AwaitCallback();

            SeleniumAutoAuth(authTokenRetrieverLib.AuthURL());

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

        public async Task<RedditPost> GetRedditPostWithComments()
        {
            if (RefreshToken == null) return null;
            var rClient = new RedditClient(_configuration["RedditClientID"], RefreshToken);
            var postHelper = new PostsHelper(rClient);

            var postsList = postHelper.GetPostsFromSubreddit("AskReddit");
            var bestPost = postsList.OrderBy(x => rng.Next()).ToList().FirstOrDefault();
            postsList.Clear();

            var commentHelper = new CommentHelper(rClient, bestPost);
            bestPost.CommentsList = commentHelper.GetComments();
            return bestPost;
        }

        public async Task<bool> ProcessRedditPost(RedditPost redditPost)
        {
            bool done = false;
            try
            {
                await CreateTitleScreenshot(redditPost);

                var filteredComments = redditPost.CommentsList
                                                .Where(y => !string.IsNullOrEmpty(y.Body) && y.Body.Length <= 50 && !y.Body.Contains("deleted") && y.Removed.Equals(false))
                                                .OrderByDescending(x => x.UpVotes)
                                                .ToList();

                await CreateCommentScreenshot(filteredComments);
                done = true;
            }
            catch (Exception ex)
            {

            }
            return done;
        }

        #region Selenium methods 
        private async Task CreateTitleScreenshot(RedditPost redditPost)
        {
            string filename;
            IWebDriver driver = InitializeWebDriver();

            Console.WriteLine($"Prepearing to find a post element and take a screenshot");
            Console.WriteLine($"Redirecting to post page {redditPost.Url}");
            driver.Navigate().GoToUrl(redditPost.Url);
            IWebElement postQuestionDiv = driver.FindElement(By.CssSelector("[data-test-id='post-content']"));
            
            filename = VideoContentFolder + $"Question_{redditPost.PostId}.jpg";
            SaveElementScreenshot(driver, postQuestionDiv, filename);
            Console.WriteLine($"Image of post question with filename {filename} was saved");

            driver.Dispose();

            VideoTotalLength += await _textToSpeechService.CreateAudioFromTextAndReturnLength(redditPost.Title, VideoContentFolder + $"Title_Audio_{redditPost.PostId}.wav");
            return;
        }

        private async Task CreateCommentScreenshot(List<Comment> postComments)
        {
            string filename;
            try
            {
                foreach (var comment in postComments)
                {
                    IWebDriver driver = InitializeWebDriver();
                    
                    Console.WriteLine($"Prepearing to find comment element and take a screenshot");
                    driver.Navigate().GoToUrl("https://www.reddit.com" + comment.Permalink);
                    Console.WriteLine($"Redirecting to comment permalink {comment.Permalink}");

                    driver.Manage().Window.Maximize();
                    IWebElement commentElement = TryFindTextElement(driver, comment);
                    if (commentElement == null)
                    {
                        Console.WriteLine($"Could not find comment element with text {comment.Body}");
                        return;
                    }

                    AcceptCookies(driver);
                    
                    filename = VideoContentFolder + $"Comment_{comment.Id}.jpg";
                    SaveElementScreenshot(driver, commentElement, filename);
                    Console.WriteLine($"Image of post question with filename {filename} was saved");

                    driver.Dispose();

                    VideoTotalLength += await _textToSpeechService.CreateAudioFromTextAndReturnLength(comment.Body, VideoContentFolder + $"Comment_Audio_{comment.Id}.wav");
                    if (VideoTotalLength.CompareTo(TimeSpan.FromSeconds(59)) > 0 || VideoTotalLength.CompareTo(TimeSpan.FromSeconds(59)) == 0)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during comments images creation");
                Console.WriteLine($"{ex}");
            }
            return;
        }

        private void AcceptCookies(IWebDriver driver)
        {
            try
            {
                var cookies = driver.FindElement(By.XPath("//button[contains(., 'Accept all')]"));
                cookies.Submit();
                CookiesAccepted = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cookies wasn't found {ex.Message} No need to panic, it means they already accepted");
            }
        }

        private IWebElement TryFindTextElement(IWebDriver driver, Comment commentToFind)
        {
            var commentElements = driver.FindElements(By.CssSelector("[class^='Comment']"));
            foreach (var comment in commentElements)
            {
                if (comment.Text.Contains(commentToFind.Body))
                {
                    return comment;
                }
            }
            return commentElements.FirstOrDefault();
        }

        private static void SaveElementScreenshot(IWebDriver driver, IWebElement element, string filename)
        {
            Screenshot sc = ((ITakesScreenshot)driver).GetScreenshot();
            Bitmap img = Image.FromStream(new MemoryStream(sc.AsByteArray)) as Bitmap;
            Bitmap elemScreenshot = img.Clone(new Rectangle(element.Location, element.Size), img.PixelFormat);
            elemScreenshot.Save(filename, ImageFormat.Jpeg);
            img.Dispose();
            elemScreenshot.Dispose();
        }

        private void SeleniumAutoAuth(string url)
        {
            IWebDriver driver = InitializeWebDriver();
            Console.WriteLine($"Chrome Driver is initialized, navigating to {url}");
            driver.Navigate().GoToUrl(url);
            try
            {
                IWebElement usernameInput = driver.FindElement(By.Name("username"));
                usernameInput.SendKeys(_configuration["RedditLogin"]);
                IWebElement passwordInput = driver.FindElement(By.Name("password"));
                passwordInput.SendKeys(_configuration["RedditPassword"]);
                IWebElement loginButton = driver.FindElement(By.XPath("//button[@type='submit']"));
                loginButton.Submit();

                Console.WriteLine($"Logged in as {_configuration["RedditLogin"]}");
                Thread.Sleep(2500);

                IWebElement acceptButton = driver.FindElement(By.Name("authorize"));
                Actions actions = new Actions(driver);
                actions
                    .MoveToElement(acceptButton)
                    .Perform();
                acceptButton.Click();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Got Exception during selenium auto auth {ex}");
            }
            finally
            {
                Console.WriteLine("Driver disposed");
                driver.Dispose();
            }
        }

        private IWebDriver InitializeWebDriver()
        {
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("no-sandbox");
            options.AddArguments("--disable-notifications");

            var service = ChromeDriverService.CreateDefaultService();
            service.SuppressInitialDiagnosticInformation = true;

            IWebDriver driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(120));
            return driver;
        }

        #endregion
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
