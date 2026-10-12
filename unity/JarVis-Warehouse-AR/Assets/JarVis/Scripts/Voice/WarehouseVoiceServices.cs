using System;

namespace JarVis.Warehouse.Voice
{
    public interface IMicrophoneInputService
    {
        bool IsAvailable { get; }
        void BeginCapture(Action<string> completed, Action<string> failed);
        void StopCapture();
    }

    public interface ITextToSpeechService
    {
        bool IsAvailable { get; }
        void Speak(string text, Action<string> failed);
        void Stop();
    }

    public sealed class PlaceholderMicrophoneInputService : IMicrophoneInputService
    {
        public bool IsAvailable => false;

        public void BeginCapture(Action<string> completed, Action<string> failed)
        {
            failed?.Invoke("voice_input_unavailable");
        }

        public void StopCapture()
        {
        }
    }

    public sealed class PlaceholderTextToSpeechService : ITextToSpeechService
    {
        public bool IsAvailable => false;

        public void Speak(string text, Action<string> failed)
        {
            failed?.Invoke("text_to_speech_unavailable");
        }

        public void Stop()
        {
        }
    }
}
