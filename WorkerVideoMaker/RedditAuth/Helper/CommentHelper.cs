using Reddit;
using Reddit.Controllers;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WorkerVideoMaker.Models;

namespace WorkerVideoMaker.RedditAuth.Helper
{
    public class CommentHelper
    {
        private RedditClient Reddit;
        private Post Post;
        private int NumComments = 0;
        private HashSet<string> CommentIds;

        public CommentHelper(RedditClient redditClient, RedditPost post)
        {
            Reddit = redditClient;
            Post = FromPermalink(post.Permalink).About();
            CommentIds = new HashSet<string>();
        }

        public List<Comment> GetComments()
        {
            return Post.Comments.GetTop(limit: 500);
        }

        private void IterateComments(IList<Comment> comments, int depth = 0)
        {
            foreach (Comment comment in comments)
            {
                ShowComment(comment, depth);
                IterateComments(comment.Replies, (depth + 1));
                IterateComments(GetMoreChildren(comment), depth);
            }
        }

        private IList<Comment> GetMoreChildren(Comment comment)
        {
            List<Comment> res = new List<Comment>();
            if (comment.More == null)
            {
                return res;
            }

            foreach (Reddit.Things.More more in comment.More)
            {
                foreach (string id in more.Children)
                {
                    if (!CommentIds.Contains(id))
                    {
                        res.Add(Post.Comment("t1_" + id).About());
                    }
                }
            }

            return res;
        }

        private void ShowComment(Comment comment, int depth = 0)
        {
            if (comment == null || string.IsNullOrWhiteSpace(comment.Author))
            {
                return;
            }

            NumComments++;
            if (!CommentIds.Contains(comment.Id))
            {
                CommentIds.Add(comment.Id);
            }

            if (depth.Equals(0))
            {
                Console.WriteLine("---------------------");
            }
            else
            {
                for (int i = 1; i <= depth; i++)
                {
                    Console.Write("> ");
                }
            }

            Console.WriteLine("[" + comment.Author + "] " + comment.Body);
        }
        
        private Post FromPermalink(string permalink)
        {
            // Get the ID from the permalink, then preface it with "t3_" to convert it to a Reddit fullname.  --Kris
            Match match = Regex.Match(permalink, @"\/comments\/([a-z0-9]+)\/");

            string postFullname = "t3_" + (match != null && match.Groups != null && match.Groups.Count >= 2
                ? match.Groups[1].Value
                : "");
            if (postFullname.Equals("t3_"))
            {
                throw new Exception("Unable to extract ID from permalink.");
            }

            // Retrieve the post and return the result.  --Kris
            return Reddit.Post( postFullname).About();
        }
    }
}
