using System;
using System.IO;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Warehouse;
using UnityEditor;

namespace ProjectBootstrap
{
    public static class WarehouseMapRouteReview
    {
        [MenuItem("Tools/JarVis/Verify Warehouse Map Route")]
        public static void Run()
        {
            var map = new OmsMobileApi.WarehouseMapReply
            {
                version = 3,
                document = new OmsMobileApi.WarehouseMapDocument
                {
                    schemaVersion = 1,
                    warehouseId = "MAIN",
                    floors = new[]
                    {
                        new OmsMobileApi.WarehouseMapFloor { id = "F1", width = 10, depth = 10 },
                        new OmsMobileApi.WarehouseMapFloor { id = "F2", width = 10, depth = 10 }
                    },
                    nodes = new[]
                    {
                        new OmsMobileApi.WarehouseMapNode { id = "A", floorId = "F1", x = 0, z = 0 },
                        new OmsMobileApi.WarehouseMapNode { id = "B", floorId = "F1", x = 1, z = 0 },
                        new OmsMobileApi.WarehouseMapNode { id = "C", floorId = "F1", x = 2, z = 0 },
                        new OmsMobileApi.WarehouseMapNode { id = "D", floorId = "F1", x = 0, z = 2 },
                        new OmsMobileApi.WarehouseMapNode { id = "E", floorId = "F2", x = 1, z = 1 }
                    },
                    edges = new[]
                    {
                        new OmsMobileApi.WarehouseMapEdge { from = "A", to = "B" },
                        new OmsMobileApi.WarehouseMapEdge { from = "B", to = "C", oneWay = true },
                        new OmsMobileApi.WarehouseMapEdge { from = "A", to = "D", blocked = true }
                    },
                    locations = new[]
                    {
                        new OmsMobileApi.WarehouseMapLocation { code = "BIN-C", floorId = "F1", accessNodeId = "C" },
                        new OmsMobileApi.WarehouseMapLocation { code = "BIN-D", floorId = "F1", accessNodeId = "D" },
                        new OmsMobileApi.WarehouseMapLocation { code = "BIN-E", floorId = "F2", accessNodeId = "E" },
                        new OmsMobileApi.WarehouseMapLocation { code = "BIN-A", floorId = "F1", accessNodeId = "A" }
                    },
                    markers = new[]
                    {
                        new OmsMobileApi.WarehouseMapMarker { id = "START", nodeId = "A", floorId = "F1" },
                        new OmsMobileApi.WarehouseMapMarker { id = "END", nodeId = "C", floorId = "F1" }
                    }
                }
            };
            void Expect(WarehouseMapRoute.Failure expected, int version, string marker, string destination)
            {
                var actual = WarehouseMapRoute.Find(map, version, marker, destination, out var path);
                if (actual != expected) throw new Exception($"Expected {expected}, got {actual} for {marker} to {destination}");
                if (expected == WarehouseMapRoute.Failure.None && path.Count == 0) throw new Exception("Empty successful route");
            }
            Expect(WarehouseMapRoute.Failure.None, 3, "START", "BIN-C");
            var result = WarehouseMapRoute.Find(map, 3, "START", "BIN-C", out var route);
            if (result != WarehouseMapRoute.Failure.None || route.Count != 3 || route[0].id != "A" || route[2].id != "C")
                throw new Exception("Route order is incorrect");
            Expect(WarehouseMapRoute.Failure.NoRoute, 3, "START", "BIN-D");
            Expect(WarehouseMapRoute.Failure.NoRoute, 3, "END", "BIN-A");
            Expect(WarehouseMapRoute.Failure.DifferentFloor, 3, "START", "BIN-E");
            Expect(WarehouseMapRoute.Failure.VersionChanged, 2, "START", "BIN-C");
            Expect(WarehouseMapRoute.Failure.InvalidMarker, 3, "UNKNOWN", "BIN-C");
            Expect(WarehouseMapRoute.Failure.UnknownDestination, 3, "START", "UNKNOWN");
            map.document.edges[1].blocked = true;
            Expect(WarehouseMapRoute.Failure.NoRoute, 3, "START", "BIN-C");
            if (!WarehouseMapRoute.TryParseMarkerCode("JWM:MAIN:START", out string markerId) || markerId != "START" ||
                WarehouseMapRoute.TryParseMarkerCode("JWM:OTHER:START", out _) ||
                WarehouseMapRoute.TryParseMarkerCode("JWM:MAIN:", out _))
                throw new Exception("Marker code parsing is incorrect");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/WarehouseMapRouteReview.txt", "PASS: route order, blocked edges, one-way, no route, floor, version, marker, destination, QR parse\n");
        }
    }
}
