using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.VisualMaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class VisualMapUiReview
    {
        private static int checks;
        private static void Check(bool condition,string label) { if (!condition) throw new InvalidOperationException(label); checks++; }
        private static void Set(object instance,string property,object value) => instance.GetType().GetProperty(property).SetValue(instance,value);
        private static void Field(object instance,string field,object value) => instance.GetType().GetField(field,BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance,value);
        private static void Invoke(object instance,string method) => instance.GetType().GetMethod(method,BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance,null);

        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity",OpenSceneMode.Single);
            }
            checks = 0;
            Directory.CreateDirectory("Logs/VisualMaps/UI");
            foreach (Vector2Int size in new[] { new Vector2Int(412,915),new Vector2Int(412,732) })
            {
                foreach (VisualMapMode mode in Enum.GetValues(typeof(VisualMapMode))) Render(size,mode);
                Render(size,VisualMapMode.Capture,"Dark");
                Render(size,VisualMapMode.Capture,"Left");
                Render(size,VisualMapMode.Library,"Populated");
            }
            File.WriteAllText("Logs/VisualMaps/ui-review.txt","PASS " + checks + " layout checks; " + (Enum.GetValues(typeof(VisualMapMode)).Length * 2 + 6) + " synthetic UI renders, not a camera test.");
        }

        private static void Render(Vector2Int size,VisualMapMode mode,string variant = "")
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            RenderTexture target = new RenderTexture(size.x,size.y,24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                GameObject root = new GameObject("Visual Maps synthetic UI QA"); SceneManager.MoveGameObjectToScene(root,scene);
                VisualMapSession owner = root.AddComponent<VisualMapSession>();
                Set(owner,"Ready",true); Set(owner,"Mode",mode); Set(owner,"FloorSet",mode != VisualMapMode.Capture);
                Set(owner,"Message",mode == VisualMapMode.Locate ? "Đã khớp 1/2 lượt · 62 điểm. Nhìn một góc khác đã chụp rồi giữ máy. Chưa bật đường khi hai lần định vị chưa nhất quán." : mode == VisualMapMode.Route ? "Đã ghi A. Đi chậm đúng lối muốn lưu tới B. Đích B là vị trí đứng khi quét kết thúc." : mode == VisualMapMode.Navigate ? "Khớp 62 điểm · sai số ảnh 1,2 px. Quan sát lối đi thật." : "Mở START-PC.cmd trên máy tính, dùng chung Wi-Fi và quét QR kết nối trên PC. Ảnh gốc vẫn được giữ để gửi lại nếu mạng bị ngắt.");
                Set(owner,"Remaining",5.5f); Set(owner,"Direction","Sắp rẽ phải · nhìn sang phải →"); Set(owner,"UploadProgress",.48f);
                VisualMapCapture capture = new VisualMapCapture { name = "Phòng ngủ · lần quét buổi tối",captureState = "complete" };
                for (int index = 0; index < 24; index++) capture.frames.Add(new VisualMapFrame());
                Field(owner,"capture",capture);
                Field(owner,"captureHint",variant == "Dark" ? "Khu vực không đủ ánh sáng. Bật thêm đèn hoặc hướng camera về vùng sáng hơn." : variant == "Left" ? "Lia chậm camera sang trái một chút để bổ sung góc chưa có ảnh ở vị trí này." : "Đang tự lưu ảnh. Đi chậm và hướng camera vào đồ vật, tường và các góc phòng.");
                if (mode == VisualMapMode.MapDetail) Set(owner,"SelectedMap",new VisualMapModel { id = "room_a",name = "Phòng ngủ · bản quét mới",geometryState = "missing" });
                if (mode == VisualMapMode.MapDetail || mode == VisualMapMode.Navigate || mode == VisualMapMode.MapLive || mode == VisualMapMode.Locate)
                {
                    Set(owner,"SelectedMap",new VisualMapModel { schema = 2,id = "room_a",state = "ready",name = "Lab · quét khu vực bàn thực hành",points = new[] { new VisualMapPoint { x = .4f,z = .5f },new VisualMapPoint { x = .4f,z = 1.5f },new VisualMapPoint { x = 2.4f,z = 1.5f } } });
                    int[] cells = new int[30 * 24];
                    for (int index = 0; index < cells.Length; index++) cells[index] = index % 30 == 0 ? 2 : index < 90 ? 0 : 1;
                    Set(owner,"Geometry",new VisualMapGeometry { width = 30,height = 24,resolution = .1f,origin = new[] { 0f,0f },cells = cells });
                }
                if (mode == VisualMapMode.Inspect)
                {
                    Texture2D photo = new Texture2D(240,160); Set(owner,"ObjectImage",photo);
                    Set(owner,"ObjectDetections",new[] { new VisualObjectDetection { label = "chair",confidence = .85f,box = new[] { .2f,.1f,.7f,.8f } } });
                }
                if (variant == "Populated")
                {
                    owner.LocalCaptures.Add(new VisualMapLocalEntry { Name = "Phòng ngủ của Tín · lượt chụp mới nhất cùng nhiều đồ vật trong phòng",Photos = 100,DepthFrames = 72,Interrupted = true });
                    Set(owner,"RemoteMaps",new[] { new VisualMapModel { id = "room_a",name = "Kho hàng có tên dài để kiểm tra nội dung thư viện bản đồ nhiều phòng",registered = 80,total = 100,revision = 2 } });
                }
                Field(owner,"url","http://192.168.1.8:8765"); Field(owner,"token",new string('a',64)); Field(owner,"candidateCount",1);
                VisualMapUi ui = root.AddComponent<VisualMapUi>(); ui.Owner = owner; Invoke(ui,"Start");
                GameObject cameraObject = new GameObject("UI camera",typeof(Camera)); cameraObject.transform.SetParent(root.transform,false);
                Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.075f,.12f,.19f); camera.targetTexture = target;
                owner.arCamera = camera;
                if (mode == VisualMapMode.Navigate)
                {
                    GameObject cue = new GameObject("Reference chevron fixture"); cue.transform.SetParent(root.transform,false);
                    VisualMapCue presentation = cue.AddComponent<VisualMapCue>(); presentation.Initialize(Resources.Load<Material>("Routes/LiveViewUnlit"));
                    presentation.Show(camera,new Vector3(.1f,.2f,2.5f),90,true);
                }
                Canvas canvas = root.GetComponentInChildren<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
                for (int pass = 0; pass < 3; pass++)
                {
                    Invoke(ui,"Update");
                    foreach (Text text in root.GetComponentsInChildren<Text>()) text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
                    Canvas.ForceUpdateCanvases();
                }
                foreach (Text text in root.GetComponentsInChildren<Text>())
                {
                    float preferred = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size)) / text.pixelsPerUnit;
                    Check(preferred <= text.rectTransform.rect.height + 1,"Clipped text " + mode + " " + size + " " + text.text + " needs " + preferred + " has " + text.rectTransform.rect.height);
                }
                foreach (Button button in root.GetComponentsInChildren<Button>()) Check(button.GetComponent<RectTransform>().rect.height >= 44,"Button too small");
                RectTransform header = root.transform.Find("Visual Maps HUD/Safe area/Header").GetComponent<RectTransform>();
                RectTransform footer = root.transform.Find("Visual Maps HUD/Safe area/Action card").GetComponent<RectTransform>();
                Vector3[] top = new Vector3[4],bottom = new Vector3[4]; header.GetWorldCorners(top); footer.GetWorldCorners(bottom);
                Check(top[0].y > bottom[1].y,"Header and action card overlap " + mode + size);
                RectTransform minimap = root.transform.Find("Visual Maps HUD/Safe area/2D map").GetComponent<RectTransform>();
                if (minimap.gameObject.activeSelf)
                {
                    Vector3[] corners = new Vector3[4]; minimap.GetWorldCorners(corners);
                    Check(corners[0].y >= bottom[1].y && corners[1].y <= top[0].y,"Minimap overlaps action card or header " + mode + size);
                }
                RectTransform guide = root.transform.Find("Visual Maps HUD/Safe area/QR floor guide").GetComponent<RectTransform>();
                if (guide.gameObject.activeSelf)
                {
                    Vector3[] corners = new Vector3[4]; guide.GetWorldCorners(corners);
                    Check(corners[0].y > bottom[1].y && corners[1].y < top[0].y,"QR guide overlaps cards " + mode + size);
                }
                camera.Render(); RenderTexture.active = target;
                Texture2D image = new Texture2D(size.x,size.y,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,size.x,size.y),0,0); image.Apply();
                File.WriteAllBytes("Logs/VisualMaps/UI/" + mode + (variant.Length > 0 ? "-" + variant : "") + "-" + size.x + "x" + size.y + ".png",image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    VisualMapSession owner = root.GetComponent<VisualMapSession>();
                    if (owner != null && owner.ObjectImage != null) { UnityEngine.Object.DestroyImmediate(owner.ObjectImage); Set(owner,"ObjectImage",null); }
                }
                EditorSceneManager.CloseScene(scene,true); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
