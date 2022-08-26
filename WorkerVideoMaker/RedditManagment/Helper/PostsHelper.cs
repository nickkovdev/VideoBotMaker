using Reddit;
using Reddit.Controllers;
using System.Linq;
using System.Collections.Generic;
using WorkerVideoMaker.Models;

namespace WorkerVideoMaker.RedditAuth.Helper
{
    public class PostsHelper
    {
		private RedditClient Reddit { get; set; }

		public PostsHelper(RedditClient redditClient)
		{
			Reddit = redditClient;
		}

		// Display the title of each post followed by the link URL (if it's a link post) or the Body (if it's a self post)
		public List<RedditPost> GetPostsFromSubreddit(string subreddit)
		{
			List<RedditPost> postsList = new List<RedditPost>();
            var subredditPosts = Reddit.Subreddit(subreddit).Posts.GetHot(limit: 100).Where(x => x.NSFW.Equals(false) && x.Title.Length < 100 && x.UpVotes > 100);
            var controversialPosts = Reddit.Subreddit(subreddit).Posts.GetControversial(limit: 100).Where(x => x.NSFW.Equals(false) && x.Title.Length < 100 && x.UpVotes > 100);
            var bestPosts = Reddit.Subreddit(subreddit).Posts.GetBest(limit: 100).Where(x => x.NSFW.Equals(false) && x.Title.Length < 100 && x.UpVotes > 100);
            var allPosts = subredditPosts.Concat(controversialPosts).Concat(bestPosts);
            foreach (Post post in allPosts)
			{
				var postToAdd = new RedditPost
				{
                    PostId = post.Id,
                    Title = post.Title,
                    Url = post.Listing.URL,
                    Permalink = post.Listing.Permalink,
                    SelfPost = post.Listing.IsSelf,
                    Upvotes = post.UpVotes,
                };
                if (postToAdd.SelfPost && !string.IsNullOrEmpty(((SelfPost)post).Listing.SelfText))
                { 
                    postToAdd.PostContent = ((SelfPost)post).Listing.SelfText;
                }
                postsList.Add(postToAdd);
			}
            return postsList;
        }
	}
}
