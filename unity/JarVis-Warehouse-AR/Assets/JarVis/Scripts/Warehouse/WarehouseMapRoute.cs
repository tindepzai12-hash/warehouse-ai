using System;
using System.Collections.Generic;
using System.Linq;
using JarVis.Warehouse.Backend;

namespace JarVis.Warehouse.Warehouse
{
    public static class WarehouseMapRoute
    {
        public enum Failure { None, NoMap, VersionChanged, InvalidMarker, UnknownDestination, DifferentFloor, NoRoute }

        public static bool TryParseMarkerCode(string code, out string markerId)
        {
            markerId = null;
            if (string.IsNullOrEmpty(code) || !code.StartsWith("JWM:MAIN:", StringComparison.Ordinal)) return false;
            string candidate = code.Substring("JWM:MAIN:".Length);
            if (candidate.Length == 0 || candidate.Length > 64 || candidate.Any(character =>
                !char.IsLetterOrDigit(character) && character != '.' && character != '_' && character != '-')) return false;
            markerId = candidate;
            return true;
        }

        public static Failure Find(OmsMobileApi.WarehouseMapReply map, int expectedVersion, string markerId,
            string locationCode, out List<OmsMobileApi.WarehouseMapNode> route)
        {
            route = new List<OmsMobileApi.WarehouseMapNode>();
            if (map?.document == null || map.version < 1 || map.document.schemaVersion != 1 ||
                map.document.warehouseId != "MAIN") return Failure.NoMap;
            if (expectedVersion > 0 && map.version != expectedVersion) return Failure.VersionChanged;
            var document = map.document;
            var marker = document.markers?.FirstOrDefault(item => item.id == markerId);
            if (marker == null) return Failure.InvalidMarker;
            var destination = document.locations?.FirstOrDefault(item =>
                string.Equals(item.code, locationCode, StringComparison.OrdinalIgnoreCase));
            if (destination == null) return Failure.UnknownDestination;
            if (marker.floorId != destination.floorId) return Failure.DifferentFloor;
            var nodes = (document.nodes ?? Array.Empty<OmsMobileApi.WarehouseMapNode>())
                .Where(item => item.floorId == marker.floorId).ToDictionary(item => item.id);
            if (!nodes.ContainsKey(marker.nodeId) || !nodes.ContainsKey(destination.accessNodeId)) return Failure.NoRoute;
            var distances = new Dictionary<string, float> { [marker.nodeId] = 0 };
            var previous = new Dictionary<string, string>();
            var visited = new HashSet<string>();
            while (true)
            {
                string current = null;
                float best = float.PositiveInfinity;
                foreach (var entry in distances)
                    if (!visited.Contains(entry.Key) && entry.Value < best) { current = entry.Key; best = entry.Value; }
                if (current == null) return Failure.NoRoute;
                if (current == destination.accessNodeId) break;
                visited.Add(current);
                foreach (var edge in document.edges ?? Array.Empty<OmsMobileApi.WarehouseMapEdge>())
                {
                    if (edge.blocked) continue;
                    string next = edge.from == current ? edge.to : !edge.oneWay && edge.to == current ? edge.from : null;
                    if (next == null || !nodes.ContainsKey(next) || visited.Contains(next)) continue;
                    var start = nodes[current];
                    var end = nodes[next];
                    float deltaX = start.x - end.x, deltaZ = start.z - end.z;
                    float candidate = best + (float)Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
                    if (distances.TryGetValue(next, out float known) && candidate >= known) continue;
                    distances[next] = candidate;
                    previous[next] = current;
                }
            }
            string nodeId = destination.accessNodeId;
            route.Add(nodes[nodeId]);
            while (nodeId != marker.nodeId)
            {
                if (!previous.TryGetValue(nodeId, out nodeId)) { route.Clear(); return Failure.NoRoute; }
                route.Add(nodes[nodeId]);
            }
            route.Reverse();
            return Failure.None;
        }
    }
}
