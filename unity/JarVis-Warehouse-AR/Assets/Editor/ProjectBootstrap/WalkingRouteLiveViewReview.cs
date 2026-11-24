using System;
using System.IO;
using JarVis.Warehouse.RouteRecording;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    public static class WalkingRouteLiveViewReview
    {
        public static void Run()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity",OpenSceneMode.Single);
            WalkingRouteData data = JsonUtility.FromJson<WalkingRouteData>(Resources.Load<TextAsset>("Routes/RecordedPrefix").text);
            int checks = 0;
            foreach (float yaw in new[] { 0f, 90f, -135f })
            {
                WalkingRouteFollower follower = new WalkingRouteFollower();
                follower.Prepare(data);
                follower.AcceptQr(data.startQr);
                Vector3 origin = new Vector3(4,1.5f,-7);
                follower.Align(origin, yaw, 0, true);
                for (int index = 0; index < data.points.Count; index++)
                {
                    Vector3 actual = follower.WorldPoint(index, -0.2f);
                    Vector3 expected = origin + Quaternion.Euler(0,yaw,0) * new Vector3(data.points[index].x,0,data.points[index].z);
                    expected.y = -0.2f;
                    if (Vector3.Distance(actual, expected) > 0.001f) throw new InvalidOperationException("World route calibration / floor mismatch");
                    checks++;
                }
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = new RenderTexture(720,1080,24);
            Material floorMaterial = null;
            try
            {
                GameObject root = new GameObject("Synthetic world route QA");
                SceneManager.MoveGameObjectToScene(root,scene);
                WalkingRouteFollower follower = new WalkingRouteFollower();
                follower.Prepare(data);
                follower.AcceptQr(data.startQr);
                follower.Align(new Vector3(0,1.5f,0),0,0,true);
                WalkingRouteLiveView live = root.AddComponent<WalkingRouteLiveView>();
                if (live.TryAutoPlace(new WalkingRouteFollower(),true)) throw new InvalidOperationException("Unaligned route must not display");
                if (live.TryAutoPlace(follower,false)) throw new InvalidOperationException("Tracking required");
                if (!live.TryAutoPlace(follower,true)) throw new InvalidOperationException("Immediate placement without plane");
                checks += 3;
                Transform visual = root.transform.Find("World route - manually aligned, estimated floor");
                Vector3 pinBefore = visual.Find("Destination pin").position;
                live.ShiftGround(0.1f);
                Vector3 pinAfter = visual.Find("Destination pin").position;
                if (Vector3.Distance(pinAfter, pinBefore + Vector3.up * 0.1f) > 0.001f) throw new InvalidOperationException("Ground adjustment changes XZ");
                live.TryAutoPlace(follower,true);
                if (Mathf.Abs(live.HeightOffset - 0.1f) > 0.001f) throw new InvalidOperationException("Ground reanchors every frame");
                follower.Observe(new Vector3(0,1.65f,0),0,0.1,true);
                live.TryAutoPlace(follower,true);
                if (Vector3.Distance(visual.Find("Destination pin").position, pinAfter) > 0.001f) throw new InvalidOperationException("Moving phone must not move ground");
                checks++;
                live.ShiftGround(float.NaN);
                live.ShiftGround(10);
                if (Mathf.Abs(live.HeightOffset - 0.7f) > 0.001f) throw new InvalidOperationException("Height upper clamp");
                live.ShiftGround(-20);
                if (Mathf.Abs(live.HeightOffset + 0.7f) > 0.001f) throw new InvalidOperationException("Height lower clamp");
                follower.Stop("QA");
                follower.Prepare(data);
                follower.AcceptQr(data.startQr);
                follower.Align(new Vector3(0,1.3f,0),0,0,true);
                live.TryAutoPlace(follower,true);
                if (Mathf.Abs(live.HeightOffset) > 0.001f) throw new InvalidOperationException("New alignment must reset height");
                checks += 5;
                if (!live.Placed) throw new InvalidOperationException("No world route visuals");
                int arrowCount = 0;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                    if (filter.name.StartsWith("AR chevron"))
                    {
                        arrowCount++;
                        if (Mathf.Abs(filter.transform.position.y - 0.025f) > 0.001f || filter.sharedMesh.vertexCount != 12 || filter.sharedMesh.subMeshCount != 2) throw new InvalidOperationException("Chevron geometry / floor height");
                        checks++;
                    }
                if (arrowCount < 10 || root.transform.Find("World route - manually aligned, estimated floor/Destination pin") == null) throw new InvalidOperationException("Missing chevrons or destination pin");
                checks++;
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.SetParent(root.transform,false);
                floor.transform.position = new Vector3(0,-0.035f,-2);
                floor.transform.localScale = new Vector3(14,0.05f,16);
                floorMaterial = new Material(Resources.Load<Material>("Routes/LiveViewUnlit"));
                floorMaterial.SetColor("_BaseColor",new Color(0.12f,0.17f,0.23f));
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                GameObject cameraObject = new GameObject("Synthetic camera",typeof(Camera));
                cameraObject.transform.SetParent(root.transform,false);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(4,5.5f,5);
                camera.transform.LookAt(new Vector3(0,0,-2));
                camera.fieldOfView = 52;
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f,0.12f,0.2f);
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>()) canvas.transform.rotation = camera.transform.rotation;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                Texture2D screenshot = new Texture2D(720,1080,TextureFormat.RGB24,false);
                screenshot.ReadPixels(new Rect(0,0,720,1080),0,0);
                screenshot.Apply();
                Directory.CreateDirectory("Logs/WalkingRoutes");
                File.WriteAllBytes("Logs/WalkingRoutes/liveview-synthetic-world.png",screenshot.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(screenshot);
                File.WriteAllText("Logs/WalkingRoutes/liveview-result.txt","PASS " + checks + " world geometry checks; " + arrowCount + " chevrons + destination beacon.\nAuto placement without plane; fixed calibration height; manual vertical offsets. Synthetic Unity camera only; NOT physical ARCore/alignment acceptance.\nUTC=" + DateTime.UtcNow.ToString("O"));
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.CloseScene(scene,true);
                if (floorMaterial != null) UnityEngine.Object.DestroyImmediate(floorMaterial);
            }
        }
    }
}
