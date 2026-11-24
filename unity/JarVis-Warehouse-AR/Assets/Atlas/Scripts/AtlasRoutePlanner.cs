using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtlasAR
{
    public sealed class AtlasRouteResult
    {
        public bool valid;
        public string reason;
        public Vector3[] points;
        public float length;
    }

    /// <summary>
    /// Plans in map-space XZ metres for a circular body. Query heights are projected
    /// onto the saved room floor; callers must first convert the camera to map space.
    /// Geometry is copied at construction. Recreate the planner when the map changes.
    /// </summary>
    public sealed class AtlasRoutePlanner
    {
        public const float BodyClearance = .25f;
        public const float GridSpacing = .10f;
        public const float MaximumStandingDistance = 1.5f;

        // A small extra margin makes rounding conservative; clearance is never reduced.
        const float Radius = BodyClearance + .00001f;
        const float RadiusSquared = Radius * Radius;
        static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        static readonly int[] Dz = { 0, 1, 1, 1, 0, -1, -1, -1 };

        readonly Polygon room;
        readonly Polygon[] obstacles;
        readonly float floorY, originX, originZ;
        readonly int width, depth;
        readonly Vector3[] nodes;
        readonly bool[] walkable;
        readonly byte[] links;
        readonly int[] components;
        readonly Dictionary<string, int> stands = new Dictionary<string, int>(StringComparer.Ordinal);

        public AtlasRoutePlanner(AtlasMap map)
        {
            AtlasMap.Validate(map);
            floorY = map.room[0].y;
            room = new Polygon(map.room);
            obstacles = new Polygon[map.obstacles.Length];
            for (int i = 0; i < obstacles.Length; i++)
                obstacles[i] = new Polygon(map.obstacles[i].points);

            originX = Mathf.Floor(room.min.x / GridSpacing) * GridSpacing;
            originZ = Mathf.Floor(room.min.y / GridSpacing) * GridSpacing;
            double columns = Math.Ceiling((room.max.x - originX) / GridSpacing) + 1;
            double rows = Math.Ceiling((room.max.y - originZ) / GridSpacing) + 1;
            if (columns * rows > 1000000)
                throw new InvalidOperationException("Map exceeds the route grid capacity.");
            width = (int)columns;
            depth = (int)rows;
            nodes = new Vector3[width * depth];
            walkable = new bool[nodes.Length];
            links = new byte[nodes.Length];
            components = new int[nodes.Length];
            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                int n = z * width + x;
                nodes[n] = new Vector3(originX + x * GridSpacing, floorY, originZ + z * GridSpacing);
                walkable[n] = CanWalk(nodes[n]);
                components[n] = -1;
            }

            // Store undirected edges once. Free endpoints alone do not make a safe
            // diagonal: the entire swept body must clear walls and obstacle corners.
            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                int n = z * width + x;
                if (!walkable[n]) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + Dx[d], nz = z + Dz[d];
                    if (nx < 0 || nx >= width || nz < 0 || nz >= depth) continue;
                    int other = nz * width + nx;
                    if (!walkable[other] || !SegmentClear(nodes[n], nodes[other])) continue;
                    links[n] |= (byte)(1 << d);
                    links[other] |= (byte)(1 << (d + 4));
                }
            }

            int largestComponent = LabelComponents();
            foreach (var marker in map.markers)
                stands.Add(marker.id, FindStand(marker.position, largestComponent));
        }

        /// <summary>
        /// Returns a deterministic free floor point within 1.5 m horizontally of the
        /// marker. Throws if the target is unknown or has no safe standing point.
        /// </summary>
        public Vector3 StandingPoint(string target)
        {
            int n;
            if (target == null || !stands.TryGetValue(target, out n) || n < 0)
                throw new InvalidOperationException("No safe standing point for target: " + target);
            return nodes[n];
        }

        /// <summary>True only if the complete body fits inside the room and outside all obstacles.</summary>
        public bool CanWalk(Vector3 point)
        {
            if (!AtlasMap.Finite(point)) return false;
            Vector2 p = Xz(point);
            if (!room.Contains(p) || room.DistanceSquared(p) <= RadiusSquared) return false;
            foreach (var obstacle in obstacles)
            {
                if (!obstacle.Near(p, p, Radius)) continue;
                if (obstacle.Contains(p) || obstacle.DistanceSquared(p) <= RadiusSquared) return false;
            }
            return true;
        }

        /// <summary>Continuously checks the swept body, including the endpoints. Height is ignored.</summary>
        public bool SegmentClear(Vector3 from, Vector3 to)
        {
            if (!CanWalk(from) || !CanWalk(to)) return false;
            Vector2 a = Xz(from), b = Xz(to);
            if (room.SegmentDistanceSquared(a, b) <= RadiusSquared) return false;
            foreach (var obstacle in obstacles)
                if (obstacle.Near(a, b, Radius) && obstacle.SegmentDistanceSquared(a, b) <= RadiusSquared)
                    return false;
            return true;
        }

        /// <summary>
        /// Returns floor points beginning at the exact supplied camera XZ, never a
        /// snapped or relocated start. Failure has a reason, empty points and zero length.
        /// </summary>
        public AtlasRouteResult Plan(Vector3 current, string target)
        {
            if (!AtlasMap.Finite(current)) return Failure("INVALID_START");
            int goal;
            if (target == null || !stands.TryGetValue(target, out goal)) return Failure("UNKNOWN_TARGET");
            if (goal < 0) return Failure("NO_STANDING_POINT");
            Vector3 start = new Vector3(current.x, floorY, current.z);
            if (!CanWalk(start)) return Failure("START_BLOCKED");
            if (SegmentClear(start, nodes[goal])) return Success(new List<Vector3> { start, nodes[goal] });

            var connectors = StartConnections(start);
            if (connectors.Count == 0) return Failure("START_NOT_CONNECTED");
            bool connected = false;
            foreach (int n in connectors)
                if (components[n] == components[goal]) { connected = true; break; }
            if (!connected) return Failure("NO_PATH");

            var path = Search(start, goal, connectors);
            if (path == null) return Failure("NO_PATH");
            path = Smooth(path);
            // Validate the public result as well as every graph and smoothing edge.
            for (int i = 1; i < path.Count; i++)
                if (!SegmentClear(path[i - 1], path[i])) return Failure("PATH_VALIDATION_FAILED");
            return Success(path);
        }

        int LabelComponents()
        {
            var queue = new Queue<int>();
            int component = 0, largest = -1, largestSize = 0;
            for (int seed = 0; seed < nodes.Length; seed++)
            {
                if (!walkable[seed] || components[seed] >= 0) continue;
                int size = 0;
                components[seed] = component;
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    int n = queue.Dequeue();
                    size++;
                    for (int d = 0; d < 8; d++)
                    {
                        if ((links[n] & (1 << d)) == 0) continue;
                        int other = n + Dz[d] * width + Dx[d];
                        if (components[other] >= 0) continue;
                        components[other] = component;
                        queue.Enqueue(other);
                    }
                }
                if (size > largestSize) { largest = component; largestSize = size; }
                component++;
            }
            return largest;
        }

        int FindStand(Vector3 marker, int mainComponent)
        {
            int best = -1, bestMain = -1;
            float distance = float.PositiveInfinity, mainDistance = float.PositiveInfinity;
            for (int n = 0; n < nodes.Length; n++)
            {
                if (!walkable[n]) continue;
                float squared = (Xz(nodes[n]) - Xz(marker)).sqrMagnitude;
                if (squared > MaximumStandingDistance * MaximumStandingDistance) continue;
                if (squared < distance) { best = n; distance = squared; }
                if (components[n] == mainComponent && squared < mainDistance)
                { bestMain = n; mainDistance = squared; }
            }
            // Prefer accessible floor over tiny pockets between a table and a wall.
            // This depends only on saved geometry, never on the caller's start.
            return bestMain >= 0 ? bestMain : best;
        }

        List<int> StartConnections(Vector3 start)
        {
            var result = new List<int>();
            int cx = Mathf.RoundToInt((start.x - originX) / GridSpacing);
            int cz = Mathf.RoundToInt((start.z - originZ) / GridSpacing);
            for (int z = Math.Max(0, cz - 2); z <= Math.Min(depth - 1, cz + 2); z++)
            for (int x = Math.Max(0, cx - 2); x <= Math.Min(width - 1, cx + 2); x++)
            {
                int n = z * width + x;
                if (walkable[n] && SegmentClear(start, nodes[n])) result.Add(n);
            }
            return result;
        }

        List<Vector3> Search(Vector3 start, int goal, List<int> connectors)
        {
            int startNode = nodes.Length, count = startNode + 1;
            var positions = new Vector3[count];
            Array.Copy(nodes, positions, nodes.Length);
            positions[startNode] = start;
            var forward = new SearchSide(count);
            var backward = new SearchSide(count);
            var potential = new float[count];
            for (int n = 0; n < count; n++)
                potential[n] = .5f * (Vector3.Distance(positions[n], positions[goal]) - Vector3.Distance(positions[n], start));
            forward.Seed(startNode, potential[startNode]);
            backward.Seed(goal, -potential[goal]);
            float best = float.PositiveInfinity;
            int meeting = -1;

            // Balanced Euclidean potentials give nonnegative reduced edge costs.
            // The sum of the two minimum keys is a lower bound on any better route.
            while (forward.Ready() && backward.Ready())
            {
                if (forward.open.Peek().priority + backward.open.Peek().priority >= best) break;
                bool fromStart = forward.open.Peek().priority <= backward.open.Peek().priority;
                SearchSide side = fromStart ? forward : backward;
                SearchSide other = fromStart ? backward : forward;
                int n = side.open.Pop().node;
                side.closed[n] = true;
                ConsiderMeeting(n, side, other, ref best, ref meeting);
                if (n == startNode)
                {
                    foreach (int next in connectors)
                        Relax(n, next, fromStart, positions, potential, side, other, ref best, ref meeting);
                }
                else
                {
                    for (int d = 0; d < 8; d++)
                        if ((links[n] & (1 << d)) != 0)
                            Relax(n, n + Dz[d] * width + Dx[d], fromStart, positions, potential, side, other, ref best, ref meeting);
                    if (connectors.Contains(n))
                        Relax(n, startNode, fromStart, positions, potential, side, other, ref best, ref meeting);
                }
            }
            if (meeting < 0) return null;
            var path = new List<Vector3>();
            for (int n = meeting; n >= 0; n = forward.parent[n]) path.Add(positions[n]);
            path.Reverse();
            for (int n = backward.parent[meeting]; n >= 0; n = backward.parent[n]) path.Add(positions[n]);
            return path;
        }

        static void ConsiderMeeting(int n, SearchSide side, SearchSide other, ref float best, ref int meeting)
        {
            float total = side.cost[n] + other.cost[n];
            if (total < best) { best = total; meeting = n; }
        }

        static void Relax(int n, int next, bool fromStart, Vector3[] positions, float[] potential,
            SearchSide side, SearchSide other, ref float best, ref int meeting)
        {
            if (side.closed[next]) return;
            float cost = side.cost[n] + Vector3.Distance(positions[n], positions[next]);
            if (cost >= side.cost[next]) return;
            side.cost[next] = cost;
            side.parent[next] = n;
            side.open.Push(new Entry(next, cost, cost + (fromStart ? potential[next] : -potential[next])));
            ConsiderMeeting(next, side, other, ref best, ref meeting);
        }

        List<Vector3> Smooth(List<Vector3> path)
        {
            var result = new List<Vector3> { path[0] };
            int current = 0;
            while (current < path.Count - 1)
            {
                int next = path.Count - 1;
                while (next > current + 1 && !SegmentClear(path[current], path[next])) next--;
                result.Add(path[next]);
                current = next;
            }
            return result;
        }

        static AtlasRouteResult Failure(string reason)
        {
            return new AtlasRouteResult { valid = false, reason = reason, points = Array.Empty<Vector3>(), length = 0 };
        }

        static AtlasRouteResult Success(List<Vector3> path)
        {
            // A camera already at the stand has a one-point, zero-length route.
            if (path.Count == 2 && (path[0] - path[1]).sqrMagnitude == 0) path.RemoveAt(1);
            float length = 0;
            for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            return new AtlasRouteResult { valid = true, reason = string.Empty, points = path.ToArray(), length = length };
        }

        static Vector2 Xz(Vector3 p) { return new Vector2(p.x, p.z); }

        sealed class SearchSide
        {
            public readonly float[] cost;
            public readonly int[] parent;
            public readonly bool[] closed;
            public readonly MinHeap open = new MinHeap();
            public SearchSide(int count)
            {
                cost = new float[count]; parent = new int[count]; closed = new bool[count];
                for (int i = 0; i < count; i++) { cost[i] = float.PositiveInfinity; parent[i] = -1; }
            }
            public void Seed(int n, float priority) { cost[n] = 0; open.Push(new Entry(n, 0, priority)); }
            public bool Ready()
            {
                while (open.Count > 0 && (closed[open.Peek().node] || open.Peek().cost != cost[open.Peek().node])) open.Pop();
                return open.Count > 0;
            }
        }

        struct Entry
        {
            public int node;
            public float cost, priority;
            public Entry(int node, float cost, float priority) { this.node = node; this.cost = cost; this.priority = priority; }
        }

        sealed class MinHeap
        {
            readonly List<Entry> entries = new List<Entry>();
            public int Count { get { return entries.Count; } }
            public Entry Peek() { return entries[0]; }
            static bool Before(Entry a, Entry b) { return a.priority < b.priority || (a.priority == b.priority && a.node < b.node); }
            public void Push(Entry item)
            {
                int at = entries.Count;
                entries.Add(item);
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (!Before(item, entries[parent])) break;
                    entries[at] = entries[parent]; at = parent;
                }
                entries[at] = item;
            }
            public Entry Pop()
            {
                Entry first = entries[0], last = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (entries.Count == 0) return first;
                int at = 0;
                while (at * 2 + 1 < entries.Count)
                {
                    int child = at * 2 + 1;
                    if (child + 1 < entries.Count && Before(entries[child + 1], entries[child])) child++;
                    if (!Before(entries[child], last)) break;
                    entries[at] = entries[child]; at = child;
                }
                entries[at] = last;
                return first;
            }
        }

        sealed class Polygon
        {
            readonly Vector2[] vertices;
            public readonly Vector2 min, max;
            public Polygon(Vector3[] points)
            {
                vertices = new Vector2[points.Length];
                Vector2 lo = Xz(points[0]), hi = lo;
                double area = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    vertices[i] = Xz(points[i]);
                    lo = Vector2.Min(lo, vertices[i]); hi = Vector2.Max(hi, vertices[i]);
                    area += Cross(Xz(points[i]), Xz(points[(i + 1) % points.Length]));
                }
                if (Math.Abs(area) < .000001) throw new InvalidOperationException("Degenerate route polygon.");
                min = lo; max = hi;
            }
            public bool Near(Vector2 a, Vector2 b, float radius)
            {
                return Mathf.Max(a.x, b.x) >= min.x - radius && Mathf.Min(a.x, b.x) <= max.x + radius
                    && Mathf.Max(a.y, b.y) >= min.y - radius && Mathf.Min(a.y, b.y) <= max.y + radius;
            }
            public bool Contains(Vector2 p)
            {
                bool inside = false;
                for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                {
                    Vector2 a = vertices[i], b = vertices[j];
                    if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
                return inside;
            }
            public float DistanceSquared(Vector2 p)
            {
                float best = float.PositiveInfinity;
                for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                    best = Mathf.Min(best, PointSegmentSquared(p, vertices[j], vertices[i]));
                return best;
            }
            public float SegmentDistanceSquared(Vector2 a, Vector2 b)
            {
                float best = float.PositiveInfinity;
                for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                {
                    Vector2 c = vertices[j], d = vertices[i];
                    if (Intersects(a, b, c, d)) return 0;
                    float endpoints = Mathf.Min(PointSegmentSquared(a, c, d), PointSegmentSquared(b, c, d));
                    float corners = Mathf.Min(PointSegmentSquared(c, a, b), PointSegmentSquared(d, a, b));
                    best = Mathf.Min(best, Mathf.Min(endpoints, corners));
                    if (best <= RadiusSquared) return best;
                }
                return best;
            }
            static float PointSegmentSquared(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                return (p - (a + ab * t)).sqrMagnitude;
            }
            static double Cross(Vector2 a, Vector2 b) { return (double)a.x * b.y - (double)a.y * b.x; }
            static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
            {
                if (Mathf.Max(a.x, b.x) < Mathf.Min(c.x, d.x) || Mathf.Max(c.x, d.x) < Mathf.Min(a.x, b.x)
                    || Mathf.Max(a.y, b.y) < Mathf.Min(c.y, d.y) || Mathf.Max(c.y, d.y) < Mathf.Min(a.y, b.y)) return false;
                double abC = Cross(b - a, c - a), abD = Cross(b - a, d - a);
                double cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
                return abC * abD <= 0 && cdA * cdB <= 0;
            }
        }
    }
}
