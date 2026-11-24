namespace JarVis.Warehouse.Lab
{
    public static class LabRouteStability
    {
        public static bool NeedsReplan(bool hasRoute,bool blockedEdgesChanged,bool remainingPathBlocked)
            =>hasRoute?remainingPathBlocked:blockedEdgesChanged;
    }
}
