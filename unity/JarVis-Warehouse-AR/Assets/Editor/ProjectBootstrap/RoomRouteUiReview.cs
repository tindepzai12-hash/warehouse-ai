using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.RouteRecording;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class RoomRouteUiReview
    {
        private static readonly List<string> Checks = new List<string>();
        private static void Check(bool condition,string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            Checks.Add("PASS " + name);
        }
        private static void Set(object instance,string property,object value) => instance.GetType().GetProperty(property).SetValue(instance,value);
        private static void Field(object instance,string field,object value) => instance.GetType().GetField(field,BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance,value);
        private static void Invoke(object instance,string method) => instance.GetType().GetMethod(method,BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance,null);

        public static void Run()
        {
            Checks.Clear();
            Directory.CreateDirectory("Logs/RoomRoutes");
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity",OpenSceneMode.Single);
            foreach (Vector2Int size in new[] { new Vector2Int(412,915),new Vector2Int(412,732) })
            foreach (string mode in new[] { "empty","saved","saved-ack","confirm","scan-A","recording","navigate-scan","guiding","camera-hint","off-route","lost","arrived","save-error","camera-error","qr-search","qr-locating","qr-wrong","qr-locked","qr-stale","rescan","prepare-record","prepare-navigate","prepare-camera","prepare-tracking","route-checking","prepare-features","departure","departure-wait" })
                Render(size,mode);
            World();
            File.WriteAllLines("Logs/RoomRoutes/ui.txt",Checks);
        }

        private static void Render(Vector2Int size,string mode)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            RenderTexture target = new RenderTexture(size.x,size.y,24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                GameObject root = new GameObject("Room UI QA");
                SceneManager.MoveGameObjectToScene(root,scene);
                WalkingRouteRecorder recorder = root.AddComponent<WalkingRouteRecorder>();
                WalkingRouteData data = RoomRouteReview.Fixture();
                Set(recorder,"Ready",mode != "camera-error");
                if (mode != "empty" && mode != "camera-error") Field(recorder,"recovered",data);
                if (mode == "confirm") recorder.Primary();
                if (mode == "scan-A") recorder.BeginRecording();
                if (mode.StartsWith("prepare-"))
                {
                    if (mode == "prepare-record") recorder.BeginRecording();
                    else recorder.NavigateSaved();
                    if (mode == "prepare-camera") recorder.Environment.Observe(Vector3.zero,Quaternion.identity,0,true);
                    if (mode == "prepare-tracking") { recorder.Environment.Frame(0,0); recorder.Environment.Observe(Vector3.zero,Quaternion.identity,0,false); }
                    if (mode == "prepare-features")
                        for (int step = 0; step <= 32; step++)
                        {
                            double now = step * 0.05;
                            recorder.Environment.Frame(now,step * 50000000L);
                            recorder.Environment.Features(6,1,now);
                            recorder.Environment.Observe(new Vector3(step * 0.003f,1.3f,0),Quaternion.Euler(0,step * 0.6f,0),now,true);
                        }
                    recorder.Scan.Warmup(0.35f);
                }
                if (mode == "recording") { recorder.Route.Begin(RoomRouteAlignment.StartQr,Vector3.zero,0,0,true); recorder.Route.ArmEnd(true); recorder.Scan.Begin(RoomRouteAlignment.EndQr); }
                if (mode == "saved-ack") { recorder.Scan.Begin(RoomRouteAlignment.EndQr); recorder.Scan.Lock(0); }
                if (mode == "save-error") { Field(recorder,"storageFailed",true); Set(recorder,"Message","Chưa lưu được tuyến mới. Bản hoàn chỉnh trước vẫn được giữ. Kiểm tra dung lượng rồi thử lưu lại."); }
                if (mode == "camera-error") Set(recorder,"Message","Cần quyền Camera. Vào Cài đặt điện thoại → Ứng dụng → JarVis Routes → Quyền → Camera, rồi bấm Thử lại.");
                if (mode == "navigate-scan" || mode == "guiding" || mode == "camera-hint" || mode == "off-route" || mode == "lost" || mode == "arrived" || mode.StartsWith("qr-") || mode == "rescan" || mode == "route-checking" || mode.StartsWith("departure"))
                {
                    recorder.NavigateSaved();
                    Set(recorder,"GuidanceStarted",!mode.StartsWith("departure"));
                    if (mode != "navigate-scan" && (!mode.StartsWith("qr-") || mode == "qr-locked")) recorder.Follower.AlignAtMarker(new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0,0,true);
                    if (mode == "off-route")
                        for (int step = 1; step <= 24; step++) recorder.Follower.Observe(new Vector3(step * 0.15f,1.3f,0),0,step * 0.1,true);
                    if (mode == "route-checking")
                        for (int step = 1; step <= 10; step++) recorder.Follower.Observe(new Vector3(step * 0.1f,1.3f,0),0,step * 0.1,true);
                    if (mode == "lost") recorder.Follower.Observe(new Vector3(0,1.3f,0),0,0.1,false);
                    if (mode == "camera-hint") recorder.Follower.Observe(new Vector3(0,1.3f,0),179,0.1,true);
                    if (mode == "arrived")
                        for (int step = 1; step <= 40; step++) recorder.Follower.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
                    if (mode == "rescan") { recorder.Follower.Observe(new Vector3(1,1.3f,0),0,0.1,true); Set(recorder,"Message",recorder.Follower.Message); }
                    if (mode.StartsWith("qr-"))
                    {
                        recorder.Scan.Frame(0);
                        if (mode == "qr-locating") recorder.Scan.Decoded(RoomRouteAlignment.StartQr,0);
                        if (mode == "qr-wrong") recorder.Scan.Decoded(RoomRouteAlignment.EndQr,0);
                        if (mode == "qr-locked") recorder.Scan.Lock(0);
                        recorder.Scan.Tick(mode == "qr-stale" ? 2 : 0,true,mode == "qr-locating" ? 0.65f : 0);
                    }
                }
                if (!mode.StartsWith("prepare-") && mode != "rescan") RoomRouteReadinessReview.Prepared(recorder.Environment);
                if (mode == "scan-A" || mode == "navigate-scan") { recorder.Scan.Frame(0); recorder.Scan.Tick(0,true,0); }
                if (mode == "rescan") recorder.Scan.Warmup(0);
                if (mode.StartsWith("departure"))
                {
                    Set(recorder,"AwaitingDeparture",true);
                    if (mode == "departure-wait") { recorder.Environment.Begin(); recorder.Scan.Warmup(0); }
                }
                WalkingRouteRecorderUi ui = root.AddComponent<WalkingRouteRecorderUi>();
                ui.Recorder = recorder;
                Invoke(ui,"Start");
                GameObject cameraObject = new GameObject("Room UI camera",typeof(Camera));
                cameraObject.transform.SetParent(root.transform,false);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.09f,0.14f,0.2f);
                camera.targetTexture = target;
                Canvas canvas = root.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 5;
                Invoke(ui,"Update");
                foreach (Text text in root.GetComponentsInChildren<Text>()) text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
                Canvas.ForceUpdateCanvases();
                Check(root.transform.Find("Room route HUD/Safe area/Header").GetComponentsInChildren<Text>()[1].text.Contains("v" + WalkingRouteRecorderUi.DisplayVersion),"Visible version fingerprint " + mode);
                if (recorder.PreparingEnvironment || recorder.Route.AwaitingEnd)
                {
                    string headerStatus = root.transform.Find("Room route HUD/Safe area/Header").GetComponentsInChildren<Text>()[1].text;
                    Check(headerStatus.Contains("%"),"Numeric progress survives in always-visible header " + mode);
                    Transform progress = root.transform.Find("Room route HUD/Safe area/Action card/Preparation progress");
                    Check(progress.gameObject.activeSelf && progress.GetComponentInChildren<Text>().text.Contains("%"),"Inline progress visible independent of reticle " + mode);
                    Check(progress.Find("Preparation track/Preparation fill").GetComponent<RectTransform>().anchorMax.x >= 0 && progress.Find("Preparation track/Preparation fill").GetComponent<RectTransform>().anchorMax.x <= 1,"Progress bar bounded " + mode);
                }
                foreach (Text text in root.GetComponentsInChildren<Text>())
                {
                    Check(text.font != null,"Native font " + mode);
                    float height = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size)) / text.pixelsPerUnit;
                    Check(height <= text.rectTransform.rect.height + 1,"No clipped label " + size + " " + mode + " " + text.text + " needs=" + height + " available=" + text.rectTransform.rect.height);
                }
                Button[] buttons = root.GetComponentsInChildren<Button>();
                foreach (Button button in buttons) Check(button.GetComponent<RectTransform>().rect.height >= 44,"Touch target >=44 " + mode);
                Check(buttons.Length == 2,"No redundant buttons " + mode);
                RectTransform header = root.transform.Find("Room route HUD/Safe area/Header").GetComponent<RectTransform>();
                RectTransform footer = root.transform.Find("Room route HUD/Safe area/Action card").GetComponent<RectTransform>();
                Vector3[] top = new Vector3[4],bottom = new Vector3[4];
                header.GetWorldCorners(top); footer.GetWorldCorners(bottom);
                Check(top[0].y > bottom[1].y,"Clear camera area between cards " + mode + size);
                RoomScanReticle reticle = root.GetComponentInChildren<RoomScanReticle>();
                if (mode == "guiding" || mode == "arrived" || mode == "off-route" || mode == "camera-hint" || mode == "saved-ack" || mode == "route-checking") Check(reticle == null,"No obsolete QR frame after scanning " + mode);
                if (mode.StartsWith("prepare-") || mode == "rescan")
                    Check(reticle != null && reticle.State == RoomScanState.WarmingUp && !recorder.Environment.Ready,"Preparation is distinct from QR scanning " + mode);
                if (mode == "departure") Check(!recorder.GuidanceStarted && PrimaryButton(root).GetComponentInChildren<Text>().text == "Bắt đầu đi","Explicit Start is visible before navigation begins");
                if (mode == "departure-wait") Check(!PrimaryButton(root).interactable,"Missing environment evidence disables departure");
                if (reticle != null)
                {
                    if (mode == "qr-wrong" || mode == "qr-stale") Check(reticle.transform.Find("Scan status").GetComponentInChildren<Text>().text.Contains(mode == "qr-wrong" ? "Sai mã" : "chờ ảnh camera"),"Progress never conceals wrong-code or stale-camera reason " + mode);
                    Vector3[] guide = new Vector3[4]; reticle.transform.Find("Scan status").GetComponent<RectTransform>().GetWorldCorners(guide);
                    Check(guide[0].y > bottom[1].y,"Scanner/status do not overlap actions " + mode + size);
                    Check(!reticle.raycastTarget,"Scanner never blocks taps");
                }
                Save(camera,target,"Logs/RoomRoutes/ui-" + mode + "-" + size.x + "x" + size.y + ".png");
                if (mode == "saved")
                {
                    foreach (Button button in buttons) if (button.name == "Ghi tuyến") button.onClick.Invoke();
                    Check(recorder.ReplacementPending && recorder.HasSavedRoute,"Record asks before replacing current route");
                    Invoke(ui,"Update");
                    foreach (Button button in root.GetComponentsInChildren<Button>()) if (button.name == "Chỉ đường") button.onClick.Invoke();
                    Check(recorder.Home && !recorder.ReplacementPending && recorder.HasSavedRoute,"Keep current cancels replacement");
                    recorder.Primary(); recorder.BeginRecording(); recorder.Cancel();
                    Check(recorder.HasSavedRoute && recorder.SavedRoute.id == data.id,"Cancel new recording preserves current route");
                }
                if (mode == "camera-error") Check(root.transform.Find("Room route HUD/Safe area/Action card").GetComponentsInChildren<Text>()[1].text.Contains("quyền Camera"),"Permission instructions remain visible");
                if (mode == "off-route")
                {
                    PrimaryButton(root).onClick.Invoke();
                    Check(recorder.NavigationMode && recorder.Follower.WaitingQr && recorder.PreparingStart && recorder.HasSavedRoute,"Existing primary action restarts A preparation without deleting route");
                }
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.CloseScene(scene,true);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void World()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = new RenderTexture(720,1080,24);
            Material floorMaterial = null;
            try
            {
                GameObject root = new GameObject("Synthetic room AR QA");
                SceneManager.MoveGameObjectToScene(root,scene);
                WalkingRouteFollower follower = new WalkingRouteFollower();
                follower.Prepare(RoomRouteReview.Fixture());
                follower.AlignAtMarker(new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0,0,true);
                WalkingRouteLiveView live = root.AddComponent<WalkingRouteLiveView>();
                Check(!live.TryAutoPlace(follower,false),"No arrows while tracking unavailable");
                Check(live.TryAutoPlace(follower,true),"Measured route appears without waiting for a plane");
                Transform visual = root.transform.Find("World route - manually aligned, estimated floor");
                Check(visual.Find("B destination pin") != null && visual.Find("B destination pin/B pin letter/B") != null,"Blue B pin and B label present, no stacked green arrows");
                int arrows = 0;
                Vector3 previousArrow = Vector3.zero;
                foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>())
                    if (mesh.name.StartsWith("AR chevron"))
                    {
                        Check(mesh.sharedMesh.vertexCount == 12 && Mathf.Abs(mesh.transform.position.y - 0.025f) < 0.001f,"Thin cyan floor arrow geometry");
                        previousArrow = mesh.transform.position;
                        arrows++;
                    }
                Check(arrows == 2,"Only two reusable arrow objects exist, independent of total route length");
                Vector3 before = visual.Find("B destination pin").position;
                follower.Observe(new Vector3(0,1.5f,0),0,0.1,true);
                live.TryAutoPlace(follower,true);
                Check(Vector3.Distance(before,visual.Find("B destination pin").position) < 0.001f,"Phone height does not drag destination");
                Check(Mathf.Abs(WalkingRouteLiveView.MotionOffset(0.45f,0,false) - 0.035f) < 0.0001f && Mathf.Abs(WalkingRouteLiveView.MotionOffset(1.35f,0,false) + 0.035f) < 0.0001f,"Purposeful forward then back motion bounded to 3.5cm");
                Check(WalkingRouteLiveView.MotionOffset(0.45f,0,true) == 0,"Reduced motion disables movement");
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.SetParent(root.transform,false);
                floor.transform.position = new Vector3(0,-0.04f,3);
                floor.transform.localScale = new Vector3(7,0.05f,10);
                floorMaterial = new Material(Resources.Load<Material>("Routes/LiveViewUnlit"));
                floorMaterial.SetColor("_BaseColor",new Color(0.22f,0.27f,0.33f));
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                GameObject cameraObject = new GameObject("Synthetic AR camera",typeof(Camera));
                cameraObject.transform.SetParent(root.transform,false);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(1.4f,2,-1.4f);
                camera.transform.LookAt(new Vector3(0,0.45f,2.7f));
                camera.fieldOfView = 60;
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.09f,0.14f,0.2f);
                visual.Find("Destination label").rotation = camera.transform.rotation;
                visual.Find("B destination pin").rotation = Quaternion.Euler(0,camera.transform.eulerAngles.y,0);
                Canvas.ForceUpdateCanvases();
                live.UpdatePresentation(follower,camera,true);
                int visible = 0;
                foreach (Transform child in visual) if (child.name.StartsWith("AR chevron") && child.gameObject.activeSelf) visible++;
                Check(visible <= 1,"Four-metre route renders at most one arrow");
                Save(camera,target,"Logs/RoomRoutes/ar-synthetic.png");
                Directory.CreateDirectory("Logs/RoomRoutes/Motion");
                for (int frame = 0; frame < 48; frame++)
                {
                    live.AnimateGlyphs(frame / 24f,false);
                    Save(camera,target,"Logs/RoomRoutes/Motion/frame-" + frame.ToString("D3") + ".png");
                }
                Check(Vector3.Distance(before,visual.Find("B destination pin").position) < 0.001f,"Animation never moves destination anchor");
                for (int step = 1; step <= 10; step++) follower.Observe(new Vector3(step * 0.1f,1.5f,0),0,0.1 + step * 0.1,true);
                live.ApplyVisibility(follower,true);
                live.UpdatePresentation(follower,camera,true);
                Check(follower.RouteChecking && visual.gameObject.activeSelf && visual.Find("B destination pin").gameObject.activeInHierarchy,"Caution retains destination only with valid tracking/alignment");
                foreach (Transform child in visual) if (child.name.StartsWith("AR chevron")) Check(!child.gameObject.activeSelf,"Caution hides every arrow while retaining B");
                Check(visual.GetComponentInChildren<Text>().text == "B","B pin still identifiable during caution");
                Save(camera,target,"Logs/RoomRoutes/ar-caution.png");
                follower.Observe(new Vector3(1,1.5f,0),0,1.8,true);
                live.ApplyVisibility(follower,true);
                Check(follower.OffRoute && visual.gameObject.activeSelf,"Sustained off-route does not blink B away");
                Save(camera,target,"Logs/RoomRoutes/ar-off-route.png");
                foreach (float depth in new[] { 0.5f,1,2,5f })
                {
                    camera.transform.position = new Vector3(0,1.3f,4.5f - depth);
                    camera.transform.LookAt(new Vector3(0,0.65f,4.5f));
                    live.UpdatePresentation(follower,camera,true);
                    float projected = visual.Find("B destination pin").localScale.x * 0.73f / (camera.WorldToViewportPoint(visual.Find("B destination pin").position).z * 2 * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f));
                    Check(projected <= 0.0601f,"B pin remains compact at camera distance " + depth);
                    Save(camera,target,"Logs/RoomRoutes/ar-near-" + depth.ToString("F1",System.Globalization.CultureInfo.InvariantCulture) + ".png");
                }
                live.ApplyVisibility(follower,false);
                Check(!visual.gameObject.activeSelf,"Missing camera/tracking hides destination as well as arrows");
                live.ApplyVisibility(follower,true);
                follower.Observe(new Vector3(1,1.5f,0),0,1.9,false);
                live.ApplyVisibility(follower,true);
                Check(follower.Suspended && !visual.gameObject.activeSelf,"Suspension hides all AR even if external tracking flag is stale");
                follower.RequireScan("QA rescan"); live.ApplyVisibility(follower,true);
                Check(!visual.gameObject.activeSelf,"Invalidated A alignment never leaves an old B onscreen");
                WalkingRouteData longData = RoomRouteReview.Fixture();
                foreach (RoutePoint point in longData.points) point.z *= 250;
                longData.horizontalMetres = 1000;
                longData.destinationInRoute = new Vector3(0,0,1000);
                WalkingRouteFollower longFollower = new WalkingRouteFollower();
                longFollower.Prepare(longData);
                Check(longFollower.AlignAtMarker(new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0,0,true),"Thousand-metre presentation fixture aligns");
                live.BuildVisuals(longFollower,0);
                visual = root.transform.Find("World route - manually aligned, estimated floor");
                camera.transform.position = new Vector3(0,1.3f,0);
                camera.transform.LookAt(new Vector3(0,0,4));
                live.UpdatePresentation(longFollower,camera,true);
                int totalPool = 0,visiblePool = 0;
                foreach (Transform child in visual)
                    if (child.name.StartsWith("AR chevron")) { totalPool++; if (child.gameObject.activeSelf) visiblePool++; }
                Check(totalPool == 2 && visiblePool <= 2,"Thousand-metre map still uses only two pooled glyphs and bounded visible budget");
                Save(camera,target,"Logs/RoomRoutes/ar-long-route.png");
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.CloseScene(scene,true);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
                if (floorMaterial != null) UnityEngine.Object.DestroyImmediate(floorMaterial);
            }
        }
        private static Button PrimaryButton(GameObject root)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>()) if (button.name == "Ghi tuyến") return button;
            throw new InvalidOperationException("Primary button missing");
        }
        private static void Save(Camera camera,RenderTexture target,string path)
        {
            camera.Render();
            Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = target;
            Texture2D screenshot = new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            screenshot.ReadPixels(new Rect(0,0,target.width,target.height),0,0); screenshot.Apply();
            File.WriteAllBytes(path,screenshot.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(screenshot);
        }
    }
}
