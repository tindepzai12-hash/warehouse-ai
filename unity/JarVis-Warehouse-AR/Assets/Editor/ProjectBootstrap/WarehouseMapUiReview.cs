using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class WarehouseMapUiReview
    {
        static WarehouseMapUiReview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("WarehouseMapUiReview", false)) return;
                SessionState.SetBool("WarehouseMapUiReview", false);
                new GameObject("Warehouse map UI review").AddComponent<WarehouseMapUiRunner>();
            };
        }

        [MenuItem("Tools/JarVis/Review Warehouse Map UI")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("WarehouseMapUiReview", true);
            EditorApplication.isPlaying = true;
        }
    }

    public sealed class WarehouseMapUiRunner : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;
            yield return null;
            var nav = FindAnyObjectByType<ImportedFigmaNavigation>();
            nav.Show("215:2", false);
            yield return null;
            var screen = nav.screens.First(item => item.id == "215:2").root;
            var owner = screen.GetComponent<WarehouseMapScreen>();
            if (owner == null || !screen.GetComponentsInChildren<Button>().Any(button => button.name == "Tải lại" || button.name == "Retry"))
                throw new Exception("Warehouse map empty state did not render");
            owner.StopAllCoroutines();
            var map = new OmsMobileApi.WarehouseMapReply
            {
                version = 1,
                document = new OmsMobileApi.WarehouseMapDocument
                {
                    schemaVersion = 1, warehouseId = "MAIN", surveyVerified = true,
                    floors = new[] { new OmsMobileApi.WarehouseMapFloor { id = "F1", name = "Tầng 1", width = 10, depth = 10 } },
                    nodes = new[]
                    {
                        new OmsMobileApi.WarehouseMapNode { id = "A", floorId = "F1", x = 0, z = 0 },
                        new OmsMobileApi.WarehouseMapNode { id = "B", floorId = "F1", x = 3, z = 0 }
                    },
                    edges = new[] { new OmsMobileApi.WarehouseMapEdge { from = "A", to = "B" } },
                    locations = new[] { new OmsMobileApi.WarehouseMapLocation { code = "A-12", floorId = "F1", x = 3, z = 1, accessNodeId = "B" } },
                    markers = new[] { new OmsMobileApi.WarehouseMapMarker { id = "START", nodeId = "A", floorId = "F1", x = 0, z = 0 } }
                }
            };
            typeof(WarehouseMapScreen).GetField("map", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, map);
            typeof(WarehouseMapScreen).GetField("message", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, null);
            owner.SetDestination("A-12");
            if (!owner.AcceptMarkerCode("JWM:MAIN:START")) throw new Exception("Map did not accept a valid marker ID");
            typeof(WarehouseMapScreen).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
            yield return null;
            var labels = screen.GetComponentsInChildren<TMP_Text>().Select(label => label.text).ToArray();
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/WarehouseMapUiLabels.txt", labels);
            if (!labels.Any(label => label.Contains("A-12") && (label.Contains("3.0 m") || label.Contains("3,0 m"))))
            {
                File.WriteAllText("Logs/WarehouseMapUiReview.txt", "FAIL: route label missing; see WarehouseMapUiLabels.txt\n");
                EditorApplication.Exit(1);
                yield break;
            }
            if (!screen.GetComponentsInChildren<Button>().Any(button => button.name == "Quét QR điểm mốc" || button.name == "Scan checkpoint QR"))
                throw new Exception("Checkpoint scan action is missing");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/WarehouseMapUiReview.txt", "PASS: empty state, mapped route, marker scan action in imported Unity scene\n");
            EditorApplication.Exit(0);
        }
    }
}
