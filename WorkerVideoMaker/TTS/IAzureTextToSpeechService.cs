using System;
using System.Threading.Tasks;

namespace WorkerVideoMaker.TTS
{
    public interface IAzureTextToSpeechService
    {
        public Task<TimeSpan> CreateAudioFromTextAndReturnLength(string textToProcess, string pathToSave);
    }
}
