using System;
using System.Collections.Generic;
using UnityEngine;

namespace JarVis.Warehouse.VisualMaps
{
    public sealed class VisualMapRouteGuide
    {
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> distances = new List<float>();
        public float Progress { get; private set; }
        public float Length => distances[distances.Count - 1];
        public float Remaining => Mathf.Max(0,Length - Progress);
        public Vector3 Closest { get; private set; }

        public VisualMapRouteGuide(VisualMapPoint[] source)
        {
            if (source == null || source.Length > 600) throw new ArgumentException("Invalid route");
            foreach (VisualMapPoint point in source)
            {
                if (point == null || !VisualMapModel.Finite(point.x) || !VisualMapModel.Finite(point.y) || !VisualMapModel.Finite(point.z)) throw new ArgumentException("Invalid point");
                Vector3 position = point.Position;
                if (points.Count > 0 && Vector3.Distance(points[points.Count - 1],position) < .02f) continue;
                distances.Add(points.Count == 0 ? 0 : distances[distances.Count - 1] + Vector3.Distance(points[points.Count - 1],position));
                points.Add(position);
            }
            if (points.Count < 2 || Length < 1) throw new ArgumentException("Route too short");
        }

        public float Project(Vector3 position,float movement)
        {
            float minimum = Mathf.Max(0,Progress - .8f),maximum = Mathf.Min(Length,Progress + Mathf.Max(.8f,movement * 1.8f + .35f));
            float best = float.PositiveInfinity,chosen = Progress;
            for (int index = 0; index + 1 < points.Count; index++)
            {
                if (distances[index + 1] < minimum || distances[index] > maximum) continue;
                Vector3 along = points[index + 1] - points[index];
                float fraction = Mathf.Clamp01(Vector3.Dot(position - points[index],along) / along.sqrMagnitude);
                float distance = Mathf.Clamp(Mathf.Lerp(distances[index],distances[index + 1],fraction),minimum,maximum);
                Vector3 sample = Sample(distance);
                float error = Vector3.SqrMagnitude(position - sample);
                if (error < best) { best = error; chosen = distance; }
            }
            Progress = chosen; Closest = Sample(chosen);
            return Vector3.Distance(position,Closest);
        }

        public Vector3 Sample(float distance)
        {
            distance = Mathf.Clamp(distance,0,Length);
            for (int index = 0; index + 1 < points.Count; index++)
                if (distance <= distances[index + 1]) return Vector3.Lerp(points[index],points[index + 1],(distance - distances[index]) / (distances[index + 1] - distances[index]));
            return points[points.Count - 1];
        }

        public Vector3 Aim(out float turn)
        {
            turn = 0;
            for (float distance = Progress + .35f; distance < Mathf.Min(Length - .35f,Progress + 1.8f); distance += .15f)
            {
                Vector3 before = Sample(distance) - Sample(distance - .5f),after = Sample(distance + .5f) - Sample(distance);
                float angle = Vector3.SignedAngle(before,after,Vector3.up);
                if (Mathf.Abs(angle) >= 40) { turn = angle; return Sample(distance); }
            }
            return Sample(Progress + 1.6f);
        }
    }
}
