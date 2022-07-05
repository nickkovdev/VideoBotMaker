using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace WorkerVideoMaker.TTS
{
    public class AzureTextToSpeechService : IAzureTextToSpeechService
    {
        private readonly IConfiguration _configuration;
        
        public AzureTextToSpeechService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        static void OutputSpeechSynthesisResult(SpeechSynthesisResult speechSynthesisResult, string text)
        {
            switch (speechSynthesisResult.Reason)
            {
                case ResultReason.SynthesizingAudioCompleted:
                    Console.WriteLine($"Speech synthesized for text: [{text}]");
                    break;
                case ResultReason.Canceled:
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                    Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                        Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                    }
                    break;
                default:
                    break;
            }
        }

        public async Task<TimeSpan> CreateAudioFromTextAndReturnLength(string textToProcess, string pathToSave)
        {
            var speechConfig = SpeechConfig.FromSubscription(_configuration["AzureSubKey"], _configuration["AzureServiceRegion"]);
            speechConfig.SpeechSynthesisVoiceName = _configuration["SpeechSynthesisVoiceName"];

            using var audioConfig = AudioConfig.FromWavFileOutput(pathToSave);

            using var speechSynthesizer = new SpeechSynthesizer(speechConfig, audioConfig);
            var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(textToProcess);
            OutputSpeechSynthesisResult(speechSynthesisResult, textToProcess);

            return speechSynthesisResult.AudioDuration;
        }
    }
}
