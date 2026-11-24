using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class RoomFeatureSampler : MonoBehaviour
    {
        private ARPointCloudManager manager;
        private WalkingRouteRecorder recorder;
        private double nextSample;
        private readonly HashSet<ulong> identifiers = new HashSet<ulong>();
        private readonly HashSet<int> cells = new HashSet<int>();

        public void Configure(ARPointCloudManager owner,WalkingRouteRecorder target)
        {
            manager = owner;
            recorder = target;
            manager.trackablesChanged.AddListener(Changed);
        }

        private void Changed(ARTrackablesChangedEventArgs<ARPointCloud> changes)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (recorder == null || recorder.arCamera == null || !recorder.PreparingEnvironment || !recorder.Tracking || now < nextSample || changes.added.Count + changes.updated.Count == 0) return;
            nextSample = now + 0.15;
            identifiers.Clear(); cells.Clear();
            int count = 0;
            foreach (ARPointCloud cloud in manager.trackables)
            {
                if (cloud.trackingState != TrackingState.Tracking || !cloud.positions.HasValue) continue;
                var positions = cloud.positions.Value;
                var confidence = cloud.confidenceValues;
                var ids = cloud.identifiers;
                for (int index = 0; index < positions.Length && count < 2000; index++)
                {
                    if (confidence.HasValue && index < confidence.Value.Length && (!(confidence.Value[index] >= 0.3f))) continue;
                    Vector3 view = recorder.arCamera.WorldToViewportPoint(cloud.transform.TransformPoint(positions[index]));
                    if (!Visible(view)) continue;
                    if (ids.HasValue && index < ids.Value.Length && !identifiers.Add(ids.Value[index])) continue;
                    count++;
                    cells.Add(Mathf.Min(2,(int)(view.x * 3)) + Mathf.Min(1,(int)(view.y * 2)) * 3);
                }
            }
            recorder.Environment.Features(count,cells.Count,now);
        }

        public static bool Visible(Vector3 view) => view.z >= 0.2f && view.z <= 6 && view.x >= 0 && view.x <= 1 && view.y >= 0 && view.y <= 1;
        private void OnDestroy() { if (manager != null) manager.trackablesChanged.RemoveListener(Changed); }
    }
}
