using Reddit.Controllers;
using System.Collections.Generic;

namespace WorkerVideoMaker.Models
{
    public class RedditPost
    {
        public string PostId { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string PostContent { get; set; }
        public bool SelfPost { get; set; }
        public string Permalink { get; set; }
        public int Upvotes { get; set; }
        public List<Comment> CommentsList { get; set; }
    }
}
