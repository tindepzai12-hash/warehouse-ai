using System;
using System.IO;
using UnityEngine;

namespace JarVis.Warehouse.UI
{
    public enum VoiceRecorderState { Idle, Recording, Stopped, Finalizing }

    public sealed class VoiceRecorder : MonoBehaviour
    {
        public const int MaxSeconds = 30;
        public VoiceRecorderState State { get; private set; }
        public float Duration => clip == null ? 0 : frames / (float)clip.frequency;
        public float[] Levels { get; } = new float[48];
        public event Action<string> Failed;
        AudioClip clip;
        string device;
        int frames;
        float started, nextPoll;
        float[] window;

        public bool Begin()
        {
            if (State != VoiceRecorderState.Idle) return false;
            try
            {
                if (Microphone.devices.Length == 0) throw new InvalidOperationException("Microphone unavailable");
                device = Microphone.devices[0];
                if (Microphone.IsRecording(device)) throw new InvalidOperationException("Microphone is busy");
                clip = Microphone.Start(device, false, MaxSeconds, 16000);
                if (clip == null || clip.frequency < 8000 || clip.frequency > 32000) throw new InvalidOperationException("Microphone format unavailable");
                window = new float[1024 * clip.channels];
                frames = 0; started = Time.realtimeSinceStartup; nextPoll = 0;
                Array.Clear(Levels, 0, Levels.Length);
                State = VoiceRecorderState.Recording;
                return true;
            }
            catch (Exception) { Cancel(); Failed?.Invoke("Không mở được micro. Kiểm tra quyền ghi âm."); return false; }
        }

        void Update()
        {
            if (State != VoiceRecorderState.Recording || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .1f;
            CapturePosition();
            if (frames >= 1024 && clip.GetData(window, frames - 1024))
            {
                double energy = 0;
                for (int index = 0; index < window.Length; index++) energy += window[index] * window[index];
                Array.Copy(Levels, 1, Levels, 0, Levels.Length - 1);
                Levels[Levels.Length - 1] = Mathf.Clamp01((float)Math.Sqrt(energy / window.Length) * 4);
            }
            if (Duration >= MaxSeconds || !Microphone.IsRecording(device) && frames > 0) Stop();
            else if (Time.realtimeSinceStartup - started > 3 && frames == 0)
            { Cancel(); Failed?.Invoke("Micro không nhận được âm thanh. Kiểm tra thiết bị và quyền micro."); }
        }

        void CapturePosition()
        {
            int position = Microphone.GetPosition(device);
            if (position > 0) frames = Mathf.Max(frames, position);
            else if (!Microphone.IsRecording(device) && Time.realtimeSinceStartup - started >= MaxSeconds - .2f) frames = clip.samples;
        }

        public void Stop()
        {
            if (State != VoiceRecorderState.Recording) return;
            CapturePosition();
            Microphone.End(device);
            State = VoiceRecorderState.Stopped;
        }

        public VoiceMessage FinalizeMessage()
        {
            if (State != VoiceRecorderState.Recording && State != VoiceRecorderState.Stopped) return null;
            Stop();
            State = VoiceRecorderState.Finalizing;
            try
            {
                if (Duration < .25f) throw new InvalidDataException("Too short");
                var wav = ImportedChatMedia.EncodeWav(clip, frames);
                if (!ImportedChatMedia.HasAudibleSamples(wav)) throw new InvalidDataException("No audible samples");
                return VoiceMessage.Create(wav);
            }
            catch (Exception) { Failed?.Invoke("Bản ghi quá ngắn hoặc không có tiếng. Hãy thử lại."); return null; }
            finally { Cancel(); }
        }

        public void Cancel()
        {
            if (clip != null) { if (Microphone.IsRecording(device)) Microphone.End(device); Destroy(clip); clip = null; }
            State = VoiceRecorderState.Idle;
            window = null;
            frames = 0;
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { Cancel(); }
    }
}
