using System;
using System.Collections.Generic;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using System.Drawing;
using System.Drawing.Imaging;
using OpenQA.Selenium;
using Microsoft.Extensions.Configuration;
using System.Threading;
using System.IO;
using System.Linq;
using AutoItX3Lib;
using WorkerVideoMaker.Models;

namespace WorkerVideoMaker.Selenium
{
    public class SeleniumService : ISeleniumService
    {
        private readonly IConfiguration _configuration;

        private bool CookiesAccepted = false;

        public SeleniumService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public void RedditAutoAuth(string url)
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
        
        public void YoutubeUpload(CreatedVideoInfo videoInfo)
        {
            IWebDriver driver = InitializeWebDriverWithProfile();
            driver.Navigate().GoToUrl("https://studio.youtube.com/");
            
            IWebElement uploadButton = driver.FindElement(By.Id("upload-icon"));
            uploadButton.Click();

            IWebElement uploadVideoButton = driver.FindElement(By.Id("select-files-button"));
            uploadVideoButton.Click();

            AutoItX3 autoIT = new AutoItX3();
            autoIT.WinActivate("Open");
            autoIT.Send(videoInfo.Path);
            Thread.Sleep(1000);
            autoIT.Send("{ENTER}");
            Thread.Sleep(9000);
            
            IWebElement titleInput = driver.FindElement(By.XPath("//div[@aria-label='Add a title that describes your video (type @ to mention a channel)']"));
            titleInput.Clear();
            titleInput.SendKeys(videoInfo.Title);

            IWebElement descriptionInput = driver.FindElement(By.XPath("//div[@aria-label='Tell viewers about your video (type @ to mention a channel)']"));
            for (int i = 0; i < videoInfo.HashTags.Length; i++)
            {
                descriptionInput.SendKeys(videoInfo.HashTags[i] + " ");
            }
            
            IWebElement radioBtns = driver.FindElements(By.Id("radioContainer")).FirstOrDefault();
            Actions actions = new Actions(driver);
            actions
                .MoveToElement(radioBtns)
                .Perform();

            radioBtns.Click();

            IWebElement nextBtn = driver.FindElement(By.Id("next-button"));
            nextBtn.Click();
            nextBtn.Click();
            nextBtn.Click();

            IWebElement publicRadioBtn = driver.FindElements(By.Id("radioContainer")).ElementAt(4);
            publicRadioBtn.Click();

            IWebElement doneBtn = driver.FindElement(By.Id("done-button"));
            doneBtn.Click();

            Thread.Sleep(10000);
            
            driver.Dispose();
        }

        public void AcceptCookies(IWebDriver driver)
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

        public IWebElement TryFindTextElement(IWebDriver driver, string commentContent)
        {
            var commentElements = driver.FindElements(By.CssSelector("[class^='Comment']"));
            foreach (var comment in commentElements)
            {
                if (comment.Text.Contains(commentContent))
                {
                    return comment;
                }
            }
            return commentElements.FirstOrDefault();
        }

        public void SaveElementScreenshot(IWebDriver driver, IWebElement element, string filename)
        {
            Screenshot sc = ((ITakesScreenshot)driver).GetScreenshot();
            Bitmap img = Image.FromStream(new MemoryStream(sc.AsByteArray)) as Bitmap;
            Bitmap elemScreenshot = img.Clone(new Rectangle(element.Location, element.Size), img.PixelFormat);
            elemScreenshot.Save(filename, ImageFormat.Png);
            img.Dispose();
            elemScreenshot.Dispose();
        }

        public static Bitmap TakeScreenshot(IWebDriver driver, IWebElement element)
        {
            IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
            var dict = (Dictionary<string, object>)js.ExecuteScript(@"
                arguments[0].scrollIntoView(true);
                var r = arguments[0].getBoundingClientRect(), scrollX = 0, scrollY = 0;
                for(var e = arguments[0]; e; e=e.parentNode) {
                  scrollX += e.scrollLeft || 0;
                  scrollY += e.scrollTop || 0;
                }
                return {left: r.left|0, top: r.top|0, width: r.width|0, height: r.height|0
                       , scrollX: scrollX, scrollY: scrollY, innerHeight: window.innerHeight}; "
                , element);

            var rect = new Rectangle(
                Convert.ToInt32(dict["left"]),
                Convert.ToInt32(dict["top"]),
                Convert.ToInt32(dict["width"]),
                Convert.ToInt32(dict["height"]));

            byte[] bytes = ((ITakesScreenshot)driver).GetScreenshot().AsByteArray;
            using Bitmap bitmap = (Bitmap)Image.FromStream(new MemoryStream(bytes, 0, bytes.Length, false, true), false, false);

            if (bitmap.Height > Convert.ToInt32(dict["innerHeight"]))
                rect.Offset(Convert.ToInt32(dict["scrollX"]), Convert.ToInt32(dict["scrollY"]));

            rect.Intersect(new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            if (rect.Height == 0 || rect.Width == 0)
                throw new WebDriverException("WebElement is outside of the screenshot.");

            return bitmap.Clone(rect, bitmap.PixelFormat);
        }

        public IWebDriver InitializeWebDriverWithProfile()
        {
            ChromeOptions options = new ChromeOptions()
            {
                BinaryLocation = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            };

            options.AddArgument("test-type");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("no-sandbox");
            options.AddArgument("disable-infobars");
            options.AddArguments("--disable-notifications");
            options.AddArgument("--start-maximized");

            options.AddArgument(@"user-data-dir=C:\Users\nikita.kovalov\AppData\Local\Google\Chrome\User Data");

            var service = ChromeDriverService.CreateDefaultService();
            service.SuppressInitialDiagnosticInformation = true;

            IWebDriver driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(120));
            return driver;
        }


        public IWebDriver InitializeWebDriver()
        {
            ChromeOptions options = new ChromeOptions()
            {
                BinaryLocation = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            };

            options.AddArgument("test-type");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("no-sandbox");
            options.AddArgument("disable-infobars");
            options.AddArguments("--disable-notifications");
            options.AddArgument("--start-maximized");
            
            var service = ChromeDriverService.CreateDefaultService();
            service.SuppressInitialDiagnosticInformation = true;

            IWebDriver driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(120));
            return driver;
        }
    }
}
