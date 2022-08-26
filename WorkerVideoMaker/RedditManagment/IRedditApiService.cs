using OpenQA.Selenium;
using System.Threading.Tasks;
using WorkerVideoMaker.Models;

namespace WorkerVideoMaker.RedditAuth
{
    public interface IRedditApiService
    {
        public bool AuthReddit();
        public RedditPost GetRedditPostWithComments();
        public Task<CreatedVideoInfo> ProcessRedditPost(RedditPost redditPost);
    }
}
