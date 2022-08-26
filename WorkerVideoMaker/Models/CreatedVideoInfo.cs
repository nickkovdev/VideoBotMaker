
namespace WorkerVideoMaker.Models
{
    public class CreatedVideoInfo
    {
        public CreatedVideoInfo(string title)
        {
            HashTags = new string[] { "#shorts", "#funny", "#reddit", "#story", "#challenge", "#question", "#ask" };
            Title = title;
            if (title.Length < 100)
            {
                for (int i = 0; 100 > Title.Length; i++)
                {
                    Title += $" {HashTags[i]}";
                }
                Title = Trunc(Title, 100);
            }
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public string[] HashTags { get; set; }
        public string Path { get; set; }

        string Trunc(string s, int len) => s?.Length > len ? s.Substring(0, len) : s;
    }
}
