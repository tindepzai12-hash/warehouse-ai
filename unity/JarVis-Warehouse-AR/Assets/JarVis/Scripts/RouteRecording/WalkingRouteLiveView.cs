using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class WalkingRouteLiveView : MonoBehaviour
    {
        public WalkingRouteRecorder Recorder;
        public const float DefaultCameraHeight = 1.30f;
        public float HeightOffset { get; private set; }
        public float EstimatedCameraHeight => DefaultCameraHeight - HeightOffset;
        public bool Placed { get; private set; }
        public int VisibleArrowCount { get; private set; }
        public string ViewHint { get; private set; } = "";
        public string Hint { get; private set; } = "Đường AR dùng độ cao ước lượng; chỉnh nâng/hạ nếu chưa sát sàn.";
        private readonly List<GameObject> arrows = new List<GameObject>();
        private readonly List<Vector3> arrowOrigins = new List<Vector3>();
        private readonly List<Mesh> destinationMeshes = new List<Mesh>();
        private readonly List<Transform> rings = new List<Transform>();
        private GameObject visuals;
        private Transform label,beacon;
        private Text destinationText;
        private Mesh arrowMesh;
        private Mesh glowMesh;
        private Material cyan, orange, edge, white,glow;
        private MaterialPropertyBlock pulse;
        private int alignment;

        private void Awake()
        {
            pulse = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (Recorder == null) return;
            WalkingRouteFollower follower = Recorder.Follower;
            if (!Recorder.NavigationMode)
            {
                ResetPlacement();
                return;
            }
            TryAutoPlace(follower, Recorder.Tracking);
            ApplyVisibility(follower,Recorder.Tracking && Recorder.GuidanceStarted);
            if (Placed)
            {
                UpdatePresentation(follower,Recorder.arCamera,Recorder.Tracking && Recorder.GuidanceStarted);
                AnimateGlyphs(Time.unscaledTime,PlayerPrefs.GetInt("JarVis.ReduceMotion",0) == 1);
                if (label != null && Recorder.arCamera != null) label.rotation = Recorder.arCamera.transform.rotation;
                if (beacon != null && Recorder.arCamera != null) beacon.rotation = Quaternion.Euler(0,Recorder.arCamera.transform.eulerAngles.y,0);
                return;
            }
        }

        public static float MotionOffset(float seconds,int index,bool reduced) => reduced ? 0 : 0.035f * Mathf.Sin(seconds * (2 * Mathf.PI / 1.8f) - index * 0.35f);

        public void UpdatePresentation(WalkingRouteFollower follower,Camera camera,bool tracking)
        {
            if (!Placed || camera == null) return;
            int budget = RoomRoutePresentation.Budget(follower.Remaining),shown = 0;
            ViewHint = "";
            Rect lastRect = default;
            for (int index = 0; index < arrows.Count; index++)
            {
                bool sampled = RoomRoutePresentation.Arrow(follower,index,follower.CalibrationCameraY - DefaultCameraHeight + 0.025f,out Vector3 position,out Vector3 direction,out _);
                Vector3 view = camera.WorldToViewportPoint(position + visuals.transform.position);
                Quaternion rotation = direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction,Vector3.up) : Quaternion.identity;
                float arrowScale = RoomRoutePresentation.FitArrow(camera,position + visuals.transform.position,rotation,RoomRoutePresentation.ArrowScale(view.z,camera.fieldOfView,camera.aspect),out Rect projected);
                bool visible = tracking && follower.ShowArrow && sampled && shown < budget && view.z >= 1.1f && arrowScale > 0 && RoomRoutePresentation.InGuidanceWindow(projected) && (shown == 0 || RoomRoutePresentation.Separated(lastRect,projected));
                arrows[index].SetActive(visible);
                if (!visible) continue;
                shown++;
                lastRect = projected;
                arrows[index].transform.localPosition = arrowOrigins[index] = position;
                arrows[index].transform.rotation = rotation;
                arrows[index].transform.localScale = Vector3.one * arrowScale;
            }
            VisibleArrowCount = shown;
            if (tracking && follower.ShowArrow && budget > 0 && shown == 0)
            {
                if (RoomRoutePresentation.NextTurn(follower,out float corner,out _) && corner - follower.TravelledMetres <= 1.2f)
                    ViewHint = "Theo hướng rẽ trên thẻ dưới; mũi tên AR ở góc có thể nằm ngoài camera.";
                else if (RoomRoutePresentation.Arrow(follower,0,follower.CalibrationCameraY - DefaultCameraHeight,out Vector3 aim,out _,out _))
                {
                    Vector3 aimView = camera.WorldToViewportPoint(aim + visuals.transform.position);
                    ViewHint = aimView.z <= 0 || aimView.x < 0.06f || aimView.x > 0.94f ? "Xoay máy về phía tuyến đã ghi để thấy mũi tên." : aimView.y < 0.34f ? "Chúc máy xuống nhẹ để thấy mũi tên trên sàn." : aimView.y > 0.84f ? "Ngẩng máy nhẹ để thấy đường phía trước." : "Đưa tuyến vào giữa camera để thấy mũi tên.";
                }
            }
            if (beacon == null) return;
            Vector3 destination = follower.DestinationWorld(follower.CalibrationCameraY - DefaultCameraHeight) + visuals.transform.position;
            Vector3 groundView = camera.WorldToViewportPoint(destination);
            foreach (Transform ring in rings)
            {
                ring.localScale = Vector3.one * Mathf.Min(0.65f,RoomRoutePresentation.ArrowScale(groundView.z,camera.fieldOfView,camera.aspect));
                ring.gameObject.SetActive(tracking && groundView.z > 0.15f && !follower.Suspended);
            }
            beacon.position = destination + Vector3.up * 0.65f;
            Vector3 pinView = camera.WorldToViewportPoint(beacon.position);
            bool pinVisible = tracking && !follower.Suspended && pinView.z > 0.15f && pinView.x >= 0.04f && pinView.x <= 0.96f && pinView.y >= 0.12f && pinView.y <= 0.86f;
            beacon.gameObject.SetActive(pinVisible);
            float scale = RoomRoutePresentation.MarkerScale(pinView.z,camera.fieldOfView,0.73f,0.06f);
            beacon.localScale = Vector3.one * scale;
            if (label != null)
            {
                label.position = beacon.position + Vector3.up * 0.58f * scale;
                label.localScale = Vector3.one * 0.003f * scale;
                Vector3 cardView = camera.WorldToViewportPoint(label.position);
                label.gameObject.SetActive(pinVisible && cardView.y >= 0.27f && cardView.y <= 0.82f);
            }
        }

        public void ApplyVisibility(WalkingRouteFollower follower,bool tracking)
        {
            bool permitted = tracking && !follower.Suspended && (follower.Active || follower.Arrived);
            if (visuals != null) visuals.SetActive(Placed && permitted);
            if (orange != null) orange.SetColor("_BaseColor",follower.RouteCaution ? new Color(0.95f,0.53f,0.06f) : new Color(0.025f,0.29f,0.94f));
            if (destinationText != null)
            {
                destinationText.text = follower.RouteCaution ? "B · ƯỚC LƯỢNG\nĐang đối chiếu tuyến" : "ĐIỂM B\nVị trí AR ước lượng";
                destinationText.color = follower.RouteCaution ? new Color(0.5f,0.26f,0.02f) : new Color(0.035f,0.08f,0.19f);
            }
            if (!follower.ShowArrow || !permitted)
                foreach (GameObject arrow in arrows) arrow.SetActive(false);
        }

        public void AnimateGlyphs(float seconds,bool reduced)
        {
            if (pulse == null) pulse = new MaterialPropertyBlock();
            for (int index = 0; index < arrows.Count; index++)
            {
                arrows[index].transform.localPosition = arrowOrigins[index] + arrows[index].transform.forward * MotionOffset(seconds,index,reduced);
                float wave = reduced ? 0 : Mathf.Pow(Mathf.Max(0,Mathf.Sin(seconds * 3 - index * 0.8f)),6);
                pulse.SetColor("_BaseColor",Color.Lerp(new Color(0.35f,0.92f,1),new Color(0.72f,1,1),wave));
                arrows[index].GetComponent<MeshRenderer>().SetPropertyBlock(pulse,0);
            }
        }

        public bool TryAutoPlace(WalkingRouteFollower follower, bool tracking)
        {
            if (alignment != follower.AlignmentSerial)
            {
                ResetPlacement();
                HeightOffset = 0;
                alignment = follower.AlignmentSerial;
            }
            if (!Placed && follower.Active && !follower.Suspended && tracking)
                BuildVisuals(follower, follower.CalibrationCameraY - DefaultCameraHeight);
            return Placed;
        }

        public void ShiftGround(float delta)
        {
            if (!Placed || visuals == null || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            float next = Mathf.Clamp(HeightOffset + delta, -0.7f, 0.7f);
            visuals.transform.position += Vector3.up * (next - HeightOffset);
            HeightOffset = next;
        }

        public void BuildVisuals(WalkingRouteFollower follower, float floorY)
        {
            ResetPlacement();
            Material source = Resources.Load<Material>("Routes/LiveViewUnlit");
            if (source == null) { Hint = "Thiếu vật liệu AR. Không hiển thị đường giả."; return; }
            cyan = new Material(source);
            cyan.SetColor("_BaseColor", new Color(0.25f, 0.92f, 1));
            orange = new Material(source);
            orange.SetColor("_BaseColor", new Color(0.025f, 0.29f, 0.94f));
            edge = new Material(source);
            edge.SetColor("_BaseColor",new Color(0.015f,0.07f,0.28f));
            white = new Material(source);
            white.SetColor("_BaseColor",Color.white);
            Material glowSource = Resources.Load<Material>("Routes/RoomGlow");
            if (glowSource != null)
            {
                glow = new Material(glowSource);
                glow.SetColor("_BaseColor",new Color(0.15f,0.9f,1,0.4f));
            }
            arrowMesh = ArrowMesh();
            glowMesh = new Mesh { name = "Smooth arrow halo quad" };
            glowMesh.vertices = new[] { new Vector3(-0.5f,0,-0.36f),new Vector3(-0.5f,0,0.5f),new Vector3(0.5f,0,0.5f),new Vector3(0.5f,0,-0.36f) };
            glowMesh.triangles = new[] { 0,1,2,0,2,3 };
            glowMesh.RecalculateBounds();
            visuals = new GameObject("World route - manually aligned, estimated floor");
            visuals.transform.SetParent(transform, false);
            WalkingRouteData data = follower.Data;
            for (int index = 0; index < 2; index++)
            {
                    float next = 1.5f + index * RoomRoutePresentation.Spacing;
                    RoomRoutePresentation.Sample(follower,Mathf.Min(next,follower.TotalLength),floorY + 0.025f,out Vector3 from,out Vector3 direction,out int segment);
                    GameObject arrow = new GameObject("AR chevron " + index, typeof(MeshFilter), typeof(MeshRenderer));
                    arrow.transform.SetParent(visuals.transform, false);
                    arrow.transform.position = from;
                    arrow.transform.rotation = Quaternion.LookRotation(direction == Vector3.zero ? Vector3.forward : direction, Vector3.up);
                    arrow.transform.localScale = Vector3.one * 0.45f;
                    arrow.GetComponent<MeshFilter>().sharedMesh = arrowMesh;
                    arrow.GetComponent<MeshRenderer>().sharedMaterials = new[] { cyan, edge };
                    GameObject outline = new GameObject("White contrast rim",typeof(MeshFilter),typeof(MeshRenderer));
                    outline.transform.SetParent(arrow.transform,false);
                    outline.transform.localPosition = Vector3.down * 0.012f;
                    outline.transform.localScale = new Vector3(1.035f,0.65f,1.035f);
                    outline.GetComponent<MeshFilter>().sharedMesh = arrowMesh;
                    outline.GetComponent<MeshRenderer>().sharedMaterials = new[] { white,edge };
                    if (glow != null)
                    {
                        GameObject halo = new GameObject("Cyan soft edge",typeof(MeshFilter),typeof(MeshRenderer));
                        halo.transform.SetParent(arrow.transform,false);
                        halo.transform.localPosition = Vector3.down * 0.008f;
                        halo.GetComponent<MeshFilter>().sharedMesh = glowMesh;
                        halo.GetComponent<MeshRenderer>().sharedMaterial = glow;
                    }
                    arrows.Add(arrow);
                    arrowOrigins.Add(arrow.transform.localPosition);
            }
            Vector3 end = follower.MarkerAligned ? follower.DestinationWorld(floorY) : follower.WorldPoint(data.points.Count - 1, floorY);
            beacon = new GameObject("B destination pin").transform;
            beacon.SetParent(visuals.transform,false);
            beacon.position = end + Vector3.up * 0.65f;
            List<Vector3> pinVertices = new List<Vector3> { new Vector3(0,0.08f,0),new Vector3(0,-0.38f,0),new Vector3(-0.2f,-0.04f,0) };
            for (int step = 0; step <= 24; step++)
            {
                float angle = Mathf.PI - step * Mathf.PI / 24;
                pinVertices.Add(new Vector3(Mathf.Cos(angle) * 0.22f,0.13f + Mathf.Sin(angle) * 0.22f,0));
            }
            pinVertices.Add(new Vector3(0.2f,-0.04f,0));
            List<int> pinTriangles = new List<int>();
            for (int index = 1; index < pinVertices.Count; index++) pinTriangles.AddRange(new[] { 0,index,index == pinVertices.Count - 1 ? 1 : index + 1 });
            Mesh pin = new Mesh { name = "Room B pin silhouette" };
            pin.SetVertices(pinVertices); pin.SetTriangles(pinTriangles,0); pin.RecalculateNormals(); pin.RecalculateBounds();
            destinationMeshes.Add(pin);
            MeshObject("B pin shape",beacon,pin,orange);
            GameObject pinLetter = new GameObject("B pin letter",typeof(RectTransform),typeof(Canvas));
            pinLetter.transform.SetParent(beacon,false);
            pinLetter.transform.localPosition = new Vector3(0,0.13f,-0.01f);
            pinLetter.transform.localScale = Vector3.one * 0.003f;
            pinLetter.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            pinLetter.GetComponent<RectTransform>().sizeDelta = new Vector2(110,100);
            GameObject glyph = new GameObject("B",typeof(RectTransform),typeof(Text));
            glyph.transform.SetParent(pinLetter.transform,false); Fill(glyph.GetComponent<RectTransform>());
            Text letter = glyph.GetComponent<Text>(); letter.font = Resources.Load<Font>("BeVietnamPro-Bold"); letter.fontSize = 60; letter.text = "B"; letter.alignment = TextAnchor.MiddleCenter; letter.color = Color.white; letter.raycastTarget = false;
            Ring(end,0.27f,0.019f,orange);
            Ring(end,0.31f,0.008f,white);
            GameObject canvasObject = new GameObject("Destination label", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(visuals.transform, false);
            canvasObject.transform.position = end + Vector3.up * 1.35f;
            canvasObject.transform.localScale = Vector3.one * 0.003f;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(320, 100);
            GameObject panel = new GameObject("Destination card", typeof(RectTransform), typeof(CanvasRenderer), typeof(WalkingRouteSurface));
            panel.transform.SetParent(canvasObject.transform, false);
            Fill(panel.GetComponent<RectTransform>());
            panel.GetComponent<WalkingRouteSurface>().color = new Color(1,1,1,0.97f);
            panel.GetComponent<WalkingRouteSurface>().raycastTarget = false;
            GameObject textObject = new GameObject("End label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            Fill(textObject.GetComponent<RectTransform>());
            Text text = textObject.GetComponent<Text>();
            text.text = "ĐIỂM B\nVị trí AR ước lượng";
            text.font = Resources.Load<Font>("BeVietnamPro-Bold");
            text.fontSize = 21;
            text.color = new Color(0.035f,0.17f,0.16f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            destinationText = text;
            label = canvasObject.transform;
            Placed = true;
        }

        private static void MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {
            GameObject item = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            item.transform.SetParent(parent,false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private void Ring(Vector3 end,float radius,float width,Material material)
        {
            Mesh mesh = new Mesh { name = "Thin B destination ring" };
            Vector3[] vertices = new Vector3[98];
            int[] triangles = new int[288];
            for (int step = 0; step <= 48; step++)
            {
                float angle = step * Mathf.PI * 2 / 48;
                Vector3 radial = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                vertices[step * 2] = radial * radius;
                vertices[step * 2 + 1] = radial * (radius - width);
                if (step == 48) continue;
                int offset = step * 6,vertex = step * 2;
                triangles[offset] = vertex; triangles[offset + 1] = vertex + 2; triangles[offset + 2] = vertex + 1;
                triangles[offset + 3] = vertex + 1; triangles[offset + 4] = vertex + 2; triangles[offset + 5] = vertex + 3;
            }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            destinationMeshes.Add(mesh);
            MeshObject("Destination ring",visuals.transform,mesh,material);
            visuals.transform.GetChild(visuals.transform.childCount - 1).position = end + Vector3.up * 0.02f;
            rings.Add(visuals.transform.GetChild(visuals.transform.childCount - 1));
        }

        private void Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(visuals.transform, false);
            part.transform.position = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = orange;
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Release(collider);
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static Mesh ArrowMesh()
        {
            Mesh mesh = new Mesh { name = "Floor chevron" };
            Vector3[] outline = { new Vector3(-0.34f,0,-0.20f),new Vector3(-0.16f,0,-0.14f),new Vector3(0,0,-0.04f),new Vector3(0.16f,0,-0.14f),new Vector3(0.34f,0,-0.20f),new Vector3(0,0,0.34f) };
            Vector3[] vertices = new Vector3[12];
            for (int index = 0; index < 6; index++)
            {
                vertices[index] = outline[index] + Vector3.up * 0.008f;
                vertices[index + 6] = outline[index];
            }
            mesh.vertices = vertices;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0,1,2,0,2,5,5,2,3,5,3,4 },0);
            List<int> sides = new List<int>();
            for (int index = 0; index < 6; index++)
            {
                int next = (index + 1) % 6;
                sides.AddRange(new[] { index,next,index + 6,next,next + 6,index + 6 });
            }
            sides.AddRange(new[] { 6,8,7,6,11,8,11,9,8,11,10,9 });
            mesh.SetTriangles(sides,1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ResetPlacement()
        {
            if (visuals != null) Release(visuals);
            if (arrowMesh != null) Release(arrowMesh);
            if (glowMesh != null) Release(glowMesh);
            foreach (Mesh mesh in destinationMeshes) Release(mesh);
            destinationMeshes.Clear();
            rings.Clear();
            if (cyan != null) Release(cyan);
            if (orange != null) Release(orange);
            if (edge != null) Release(edge);
            if (white != null) Release(white);
            if (glow != null) Release(glow);
            visuals = null;
            label = null;
            destinationText = null;
            beacon = null;
            arrows.Clear();
            arrowOrigins.Clear();
            VisibleArrowCount = 0;
            ViewHint = "";
            Placed = false;
        }
        private void OnDisable() { ResetPlacement(); }
        private static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
