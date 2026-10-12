using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Navigation
{
    [Serializable]
    public class MapLocation
    {
        public string locationId;
        public string displayName;
        public float x;
        public float y;
        public float z;
        public string type;

        public Vector3 Position => new Vector3(x, y, z);
    }

    [Serializable]
    public sealed class Waypoint : MapLocation
    {
        public string[] neighbors = Array.Empty<string>();
    }

    [Serializable]
    public sealed class Route
    {
        public List<Waypoint> waypoints = new List<Waypoint>();
        public float totalDistance;
        public bool isMock = true;
        public bool approved;

        public bool IsValid => waypoints != null && waypoints.Count >= 2;
    }

    public sealed class WarehouseMapService
    {
        private readonly List<Waypoint> waypoints = new List<Waypoint>
        {
            CreateWaypoint("ENTRY-01", "Lối vào / Entry", 0f, 0f, 0f, "entry", "A-01-01"),
            CreateWaypoint("A-01-01", "Kệ A-01-01", 4f, 0f, 1f, "storage_bin", "ENTRY-01", "B-12-02"),
            CreateWaypoint("B-12-02", "Kệ B-12-02", 11f, 0f, 5f, "storage_bin", "A-01-01", "B-12-03", "PACK-01"),
            CreateWaypoint("B-12-03", "Kệ B-12-03", 18.4f, 0f, 7.2f, "storage_bin", "B-12-02", "B-13-03"),
            CreateWaypoint("B-13-03", "Kệ B-13-03", 19f, 0f, 12f, "storage_bin", "B-12-03", "PACK-01"),
            CreateWaypoint("PACK-01", "Đóng gói / Packing", 9f, 0f, 14f, "packing", "B-12-02", "B-13-03")
        };

        public WarehouseMapService()
        {
            waypoints.Add(CreateWaypoint("IN-A-03", "Receiving IN-A-03", 2f, 0f, 4f, "receiving", "ENTRY-01"));
            waypoints.Add(CreateWaypoint("C-07-02", "Storage C-07-02", 7f, 0f, 7f, "storage_bin", "A-01-01"));
        }

        public IReadOnlyList<Waypoint> GetWaypoints()
        {
            return waypoints;
        }

        public Route GetRoute(string startLocationId, string destinationLocationId)
        {
            return RoutePlanner.Calculate(waypoints, startLocationId, destinationLocationId);
        }

        private static Waypoint CreateWaypoint(
            string id,
            string displayName,
            float x,
            float y,
            float z,
            string type,
            params string[] neighbors
        )
        {
            return new Waypoint
            {
                locationId = id,
                displayName = displayName,
                x = x,
                y = y,
                z = z,
                type = type,
                neighbors = neighbors
            };
        }
    }

    public static class RoutePlanner
    {
        public static Route Calculate(
            IReadOnlyList<Waypoint> waypoints,
            string startLocationId,
            string destinationLocationId
        )
        {
            Route route = new Route();
            if (waypoints == null || waypoints.Count == 0)
                return route;

            Dictionary<string, Waypoint> lookup = waypoints
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.locationId))
                .GroupBy(value => value.locationId)
                .ToDictionary(group => group.Key, group => group.First());
            if (!lookup.TryGetValue(startLocationId, out Waypoint start)
                || !lookup.TryGetValue(destinationLocationId, out Waypoint destination))
            {
                return route;
            }

            Dictionary<string, float> distances = lookup.Keys.ToDictionary(key => key, _ => float.PositiveInfinity);
            Dictionary<string, string> previous = new Dictionary<string, string>();
            HashSet<string> unvisited = new HashSet<string>(lookup.Keys);
            distances[start.locationId] = 0f;

            while (unvisited.Count > 0)
            {
                string currentId = unvisited.OrderBy(id => distances[id]).First();
                if (float.IsPositiveInfinity(distances[currentId]))
                    break;

                unvisited.Remove(currentId);
                if (currentId == destination.locationId)
                    break;

                Waypoint current = lookup[currentId];
                IEnumerable<string> neighborIds = (current.neighbors ?? Array.Empty<string>())
                    .Concat(
                        lookup.Values
                            .Where(candidate => (candidate.neighbors ?? Array.Empty<string>()).Contains(currentId))
                            .Select(candidate => candidate.locationId)
                    )
                    .Distinct();

                foreach (string neighborId in neighborIds)
                {
                    if (!unvisited.Contains(neighborId) || !lookup.TryGetValue(neighborId, out Waypoint neighbor))
                        continue;

                    float candidate = distances[currentId] + Vector3.Distance(current.Position, neighbor.Position);
                    if (candidate >= distances[neighborId])
                        continue;

                    distances[neighborId] = candidate;
                    previous[neighborId] = currentId;
                }
            }

            if (start.locationId != destination.locationId && !previous.ContainsKey(destination.locationId))
                return route;

            List<Waypoint> path = new List<Waypoint> { destination };
            string cursor = destination.locationId;
            while (cursor != start.locationId)
            {
                if (!previous.TryGetValue(cursor, out cursor))
                    return new Route();
                path.Add(lookup[cursor]);
            }

            path.Reverse();
            route.waypoints = path;
            route.totalDistance = distances[destination.locationId];
            route.isMock = true;
            route.approved = false;
            return route;
        }
    }

    public enum IndoorPositionTechnology
    {
        QrMarker,
        ArFoundation,
        BluetoothBeacon,
        Uwb
    }

    public readonly struct IndoorPositionFix
    {
        public Vector3 Position { get; }
        public float AccuracyMeters { get; }
        public IndoorPositionTechnology Technology { get; }

        public IndoorPositionFix(Vector3 position, float accuracyMeters, IndoorPositionTechnology technology)
        {
            Position = position;
            AccuracyMeters = accuracyMeters;
            Technology = technology;
        }
    }

    public interface IIndoorPositionProvider
    {
        bool IsAvailable { get; }
        bool TryGetPosition(out IndoorPositionFix fix);
    }

    public sealed class DestinationSelector : MonoBehaviour
    {
        public string StartLocationId { get; private set; }
        public string DestinationLocationId { get; private set; }

        public void Select(string startLocationId, string destinationLocationId)
        {
            StartLocationId = startLocationId;
            DestinationLocationId = destinationLocationId;
        }
    }

    public sealed class NavigationArrowController : MonoBehaviour
    {
        [SerializeField] private Text directionLabel;
        [SerializeField] private Transform arArrowPlaceholder;

        public void ShowStep(Waypoint from, Waypoint to)
        {
            if (from == null || to == null)
                return;

            Vector3 direction = (to.Position - from.Position).normalized;
            if (directionLabel != null)
                directionLabel.text = $"{GetArrow(direction)}  {to.displayName}";
            if (arArrowPlaceholder != null && direction.sqrMagnitude > 0.01f)
            {
                arArrowPlaceholder.gameObject.SetActive(true);
                arArrowPlaceholder.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        public void Hide()
        {
            if (directionLabel != null)
                directionLabel.text = string.Empty;
            if (arArrowPlaceholder != null)
                arArrowPlaceholder.gameObject.SetActive(false);
        }

        private static string GetArrow(Vector3 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
                return direction.x >= 0f ? "→" : "←";
            return direction.z >= 0f ? "↑" : "↓";
        }
    }
}
