using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WorkerVideoMaker.Video
{
    public interface IVideoCreator
    {
        public Task ConfigureFFCore();
        public Task DeleteFilesFromContentFolder();
    }
}
