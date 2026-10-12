using System;
using UnityEngine;

namespace JarVis.Warehouse.UI
{
    public sealed class VoicePlayback : MonoBehaviour
    {
        public VoiceMessage Current { get; private set; }
        public bool Playing => source != null && source.isPlaying;
        public float Position => source != null && source.clip != null ? source.time : 0;
        public event Action Starting;
        public event Action<string> Failed;
        AudioSource source;
        bool paused;
        void Awake() { source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; }
        public void Toggle(VoiceMessage message)
        {
            if (message == null || message.Disposed) return;
            if (Current == message && source.clip != null)
            {
                if (Playing) { source.Pause(); paused = true; }
                else { Starting?.Invoke(); source.UnPause(); paused = false; }
                return;
            }
            Stop();
            try { Starting?.Invoke(); source.clip = VoiceAudio.Load(message.AudioPath); Current = message; source.Play(); }
            catch (Exception) { Stop(); Failed?.Invoke("Không phát được bản ghi. Hãy ghi âm lại."); }
        }
        public void Stop()
        {
            if (source != null) { source.Stop(); if (source.clip != null) Destroy(source.clip); source.clip = null; }
            Current = null; paused = false;
        }
        void Update() { if (Current != null && (Current.Disposed || !paused && !Playing)) Stop(); }
        void OnDisable() { Stop(); }
        void OnDestroy() { Stop(); }
    }
}
