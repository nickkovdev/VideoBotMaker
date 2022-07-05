using OpenQA.Selenium;
using System.Threading.Tasks;
using WorkerVideoMaker.Models;

namespace WorkerVideoMaker.RedditAuth
{
    public interface IRedditApiService
    {
        public Task<bool> AuthReddit();
        public Task<RedditPost> GetRedditPostWithComments();
        public Task<bool> ProcessRedditPost(RedditPost redditPost);
    }
}
