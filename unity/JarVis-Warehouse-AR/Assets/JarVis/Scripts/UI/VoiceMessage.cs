using System;
using System.IO;
using UnityEngine;

namespace JarVis.Warehouse.UI
{
    public enum VoiceMessageStatus { Sending, Processing, Completed, Failed }

    public sealed class VoiceMessage : IDisposable
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public string AudioPath { get; private set; }
        public float Duration { get; private set; }
        public float[] Waveform { get; private set; }
        public string Transcript { get; set; }
        public string Error { get; set; }
        public bool Retryable { get; set; }
        public VoiceMessageStatus Status { get; set; } = VoiceMessageStatus.Sending;
        public bool Disposed => AudioPath == null;

        public static VoiceMessage Create(byte[] wav)
        {
            VoiceAudio.Validate(wav);
            var message = new VoiceMessage();
            message.Duration = (wav.Length - 44) / (2f * BitConverter.ToInt32(wav, 24));
            message.Waveform = VoiceAudio.Envelope(wav, 64);
            string folder = Path.Combine(Application.temporaryCachePath, "JarVisVoice");
            Directory.CreateDirectory(folder);
            message.AudioPath = Path.Combine(folder, message.Id + ".wav");
            try { File.WriteAllBytes(message.AudioPath, wav); }
            catch { message.Dispose(); throw; }
            return message;
        }

        public void Dispose()
        {
            if (AudioPath == null) return;
            try { if (File.Exists(AudioPath)) File.Delete(AudioPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            AudioPath = null;
            Waveform = null;
        }
    }

    public static class VoiceAudio
    {
        public static void Validate(byte[] wav)
        {
            if (wav == null || wav.Length < 44 || wav.Length > 2048044) throw new InvalidDataException("Invalid WAV size");
            string Read(int offset, int length) => System.Text.Encoding.ASCII.GetString(wav, offset, length);
            int rate = BitConverter.ToInt32(wav, 24), size = wav.Length - 44;
            if (Read(0, 4) != "RIFF" || Read(8, 8) != "WAVEfmt " || Read(36, 4) != "data" ||
                BitConverter.ToInt32(wav, 16) != 16 || BitConverter.ToInt16(wav, 20) != 1 ||
                BitConverter.ToInt16(wav, 22) != 1 || BitConverter.ToInt16(wav, 34) != 16 ||
                BitConverter.ToInt16(wav, 32) != 2 || rate < 8000 || rate > 32000 ||
                BitConverter.ToInt32(wav, 28) != rate * 2 || BitConverter.ToInt32(wav, 40) != size ||
                BitConverter.ToInt32(wav, 4) != wav.Length - 8 || size % 2 != 0 ||
                size / (rate * 2f) < .25f || size / (rate * 2f) > 32) throw new InvalidDataException("Invalid mono PCM16 WAV");
        }

        public static float[] Envelope(byte[] wav, int bars)
        {
            var values = new float[bars];
            int frames = (wav.Length - 44) / 2;
            float maximum = 0;
            for (int index = 0; index < bars; index++)
            {
                int start = frames * index / bars, end = frames * (index + 1) / bars;
                double energy = 0;
                for (int frame = start; frame < end; frame++)
                {
                    float sample = BitConverter.ToInt16(wav, 44 + frame * 2) / 32768f;
                    energy += sample * sample;
                }
                values[index] = Mathf.Clamp01((float)Math.Sqrt(energy / Math.Max(1, end - start)) * 4);
                maximum = Mathf.Max(maximum, values[index]);
            }
            if (maximum > .001f) for (int index = 0; index < bars; index++) values[index] /= maximum;
            return values;
        }

        public static AudioClip Load(string path)
        {
            var wav = File.ReadAllBytes(path);
            Validate(wav);
            int frames = (wav.Length - 44) / 2;
            var pcm = new float[frames];
            for (int frame = 0; frame < frames; frame++) pcm[frame] = BitConverter.ToInt16(wav, 44 + frame * 2) / 32768f;
            var clip = AudioClip.Create("Voice playback", frames, 1, BitConverter.ToInt32(wav, 24), false);
            if (!clip.SetData(pcm, 0)) { UnityEngine.Object.Destroy(clip); throw new InvalidDataException("Cannot decode voice"); }
            return clip;
        }

        public static string TimeLabel(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
    }
}
