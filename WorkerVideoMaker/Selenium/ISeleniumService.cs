using OpenQA.Selenium;
using WorkerVideoMaker.Models;

namespace WorkerVideoMaker.Selenium
{
    public interface ISeleniumService
    {
        void RedditAutoAuth(string url);
        void YoutubeUpload(CreatedVideoInfo videoInfo);
        void AcceptCookies(IWebDriver driver);
        IWebElement TryFindTextElement(IWebDriver driver, string commentContent);
        void SaveElementScreenshot(IWebDriver driver, IWebElement element, string filename);
        IWebDriver InitializeWebDriver();
    }
}
