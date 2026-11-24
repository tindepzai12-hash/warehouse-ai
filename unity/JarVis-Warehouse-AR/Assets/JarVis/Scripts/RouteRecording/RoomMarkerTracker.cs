using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class RoomMarkerTracker : MonoBehaviour
    {
        private sealed class Sample
        {
            public Pose pose;
            public double updated,stableSince;
            public bool tracking;
        }
        private readonly Dictionary<string,Sample> samples = new Dictionary<string,Sample>();
        private ARTrackedImageManager manager;
        public string Error { get; private set; }

        public void Configure(ARTrackedImageManager owner)
        {
            manager = owner;
            XRReferenceImageLibrary library = Resources.Load<XRReferenceImageLibrary>("Routes/RoomMarkers");
            if (library == null) { Error = "Thiếu thư viện thẻ A/B. Không thể định vị."; return; }
            manager.referenceLibrary = library;
            manager.requestedMaxNumberOfMovingImages = 0;
            manager.trackablesChanged.AddListener(Changed);
        }

        private void Changed(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
        {
            foreach (ARTrackedImage image in changes.added) Observe(image);
            foreach (ARTrackedImage image in changes.updated) Observe(image);
            foreach (var removed in changes.removed)
                if (removed.Value != null) samples.Remove(removed.Value.referenceImage.name);
        }

        private void Observe(ARTrackedImage image)
        {
            string name = image.referenceImage.name;
            double now = Time.realtimeSinceStartupAsDouble;
            Pose pose = new Pose(image.transform.position,image.transform.rotation);
            bool tracking = image.trackingState == TrackingState.Tracking && Mathf.Abs(Vector3.Dot(pose.rotation * Vector3.up,Vector3.up)) < 0.35f;
            if (!samples.TryGetValue(name,out Sample sample)) samples[name] = sample = new Sample { stableSince = now };
            if (!tracking || !sample.tracking || now - sample.updated > 0.5 || Vector3.Distance(sample.pose.position,pose.position) > 0.04f || Quaternion.Angle(sample.pose.rotation,pose.rotation) > 4) sample.stableSince = now;
            sample.pose = pose;
            sample.updated = now;
            sample.tracking = tracking;
        }

        public bool TryGet(string qr,out Pose pose)
        {
            string name = qr == RoomRouteAlignment.StartQr ? "Room-A" : qr == RoomRouteAlignment.EndQr ? "Room-B" : "";
            double now = Time.realtimeSinceStartupAsDouble;
            pose = default;
            if (!samples.TryGetValue(name,out Sample sample) || !sample.tracking || now - sample.updated > 0.35 || now - sample.stableSince < 0.8) return false;
            pose = sample.pose;
            return true;
        }

        public float Progress(string qr)
        {
            string name = qr == RoomRouteAlignment.StartQr ? "Room-A" : qr == RoomRouteAlignment.EndQr ? "Room-B" : "";
            double now = Time.realtimeSinceStartupAsDouble;
            return samples.TryGetValue(name,out Sample sample) && sample.tracking && now - sample.updated <= 0.35 ? Mathf.Clamp01((float)((now - sample.stableSince) / 0.8)) : 0;
        }

        public void ResetSamples() => samples.Clear();
        private void OnDestroy()
        {
            if (manager != null) manager.trackablesChanged.RemoveListener(Changed);
        }
    }
}
