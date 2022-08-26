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
        private readonly string[] _azureTTSOptions = new string[] 
        { "en-US-JennyNeural", "en-US-GuyNeural", "en-US-AmberNeural",
          "en-US-AnaNeural", "en-US-AriaNeural", "en-US-AshleyNeural", 
          "en-US-BrandonNeural", "en-US-ChristopherNeural", 
          "en-US-CoraNeural", "en-US-ElizabethNeural", "en-US-EricNeural",
          "en-US-JacobNeural", "en-US-MichelleNeural", "en-US-MonicaNeural",
          "en-US-SaraNeural", "en-US-AIGenerate1Neural", "en-US-DavisNeural", 
          "en-US-JaneNeural", "en-US-JasonNeural", "en-US-NancyNeural", 
          "en-US-RogerNeural", "en-US-TonyNeural"};

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
            Random rd = new Random();
            var speechConfig = SpeechConfig.FromSubscription(_configuration["AzureSubKey"], _configuration["AzureServiceRegion"]);
            speechConfig.SpeechSynthesisVoiceName = _azureTTSOptions[rd.Next(0, _azureTTSOptions.Length)];

            using var audioConfig = AudioConfig.FromWavFileOutput(pathToSave);

            using var speechSynthesizer = new SpeechSynthesizer(speechConfig, audioConfig);
            var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(textToProcess);
            OutputSpeechSynthesisResult(speechSynthesisResult, textToProcess);

            return speechSynthesisResult.AudioDuration;
        }
    }
}
