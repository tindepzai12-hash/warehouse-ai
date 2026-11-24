using UnityEngine;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class RoomRouteDeviation
    {
        public bool Caution { get; private set; }
        public bool Confirmed { get; private set; }
        private double outsideSince = -1,insideSince = -1;

        public void Reset()
        {
            Caution = Confirmed = false;
            outsideSince = insideSince = -1;
        }

        public void Observe(float distance,double now)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || double.IsNaN(now) || double.IsInfinity(now))
            { Caution = Confirmed = true; return; }
            if (distance > 0.9f)
            {
                Caution = true;
                insideSince = -1;
                if (outsideSince < 0) outsideSince = now;
                if (distance >= 1.8f || now - outsideSince >= 0.6) Confirmed = true;
            }
            else
            {
                outsideSince = -1;
                if (!Caution) return;
                if (distance <= 0.55f)
                {
                    if (insideSince < 0) insideSince = now;
                    if (now - insideSince >= 0.35) Reset();
                }
                else insideSince = -1;
            }
        }
    }
}
