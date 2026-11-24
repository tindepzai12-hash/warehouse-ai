using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace JarVis.Warehouse.Preview
{
    public enum CameraRestartPhase { Idle, Stopping, Starting, WaitingForFrame, Ready, Failed }
    public interface IPreviewCameraFeed
    {
        bool Enabled { get; set; }
        bool AutoFocus { get; set; }
        int FrameCount { get; }
    }
    // Only the camera subsystem is restarted. No ARSession, anchors or map owners are touched.
    public static class PreviewCameraRestartSequence
    {
        public static IEnumerator Run(IPreviewCameraFeed feed, Func<float> now, Action<CameraRestartPhase> phase)
        {
            if (feed == null || !feed.Enabled) { phase(CameraRestartPhase.Failed); yield break; }
            bool completed = false;
            try
            {
                phase(CameraRestartPhase.Stopping); feed.Enabled = false;
                yield return new WaitForSecondsRealtime(.2f);
                phase(CameraRestartPhase.Starting); feed.Enabled = true; feed.AutoFocus = true;
                int baseline = feed.FrameCount;
                phase(CameraRestartPhase.WaitingForFrame);
                float deadline = now() + 6;
                while (feed.FrameCount <= baseline && now() < deadline) yield return null;
                completed = feed.FrameCount > baseline;
            }
            finally
            {
                // Cancellation may happen while the camera is off; always restore its original enabled state.
                feed.Enabled = true;
            }
            phase(completed ? CameraRestartPhase.Ready : CameraRestartPhase.Failed);
        }
    }
    // Candidate behavior for the isolated review scene; not installed on the shipping AR scene.
    public sealed class PreviewCameraRestart : MonoBehaviour, IPreviewCameraFeed
    {
        public ARCameraManager cameraManager;
        public CameraRestartPhase Phase { get; private set; }
        public bool Busy => routine != null;
        public event Action<CameraRestartPhase> Changed;
        int frames;
        Coroutine routine;
        bool subscribed;
        public bool Enabled { get => cameraManager != null && cameraManager.enabled; set { if (cameraManager != null) cameraManager.enabled = value; } }
        public bool AutoFocus { get => cameraManager != null && cameraManager.autoFocusRequested; set { if (cameraManager != null) cameraManager.autoFocusRequested = value; } }
        public int FrameCount => frames;
        void SetPhase(CameraRestartPhase value) { Phase = value; Changed?.Invoke(value); }
        void Frame(ARCameraFrameEventArgs _) { frames++; }
        public bool Restart()
        {
            if (Busy) return false;
            if (cameraManager == null || !cameraManager.enabled) { SetPhase(CameraRestartPhase.Failed); return false; }
            cameraManager.frameReceived += Frame; subscribed = true;
            routine = StartCoroutine(Work()); return true;
        }
        IEnumerator Work()
        {
            yield return PreviewCameraRestartSequence.Run(this, () => Time.unscaledTime, SetPhase);
            Unsubscribe(); routine = null;
        }
        void Unsubscribe() { if (subscribed && cameraManager != null) cameraManager.frameReceived -= Frame; subscribed = false; }
        void OnDisable()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; if (cameraManager != null) cameraManager.enabled = true; SetPhase(CameraRestartPhase.Failed); }
            Unsubscribe();
        }
    }
}
