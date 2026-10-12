using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace JarVis.Warehouse.AR
{
    public sealed class WarehouseSurveyCapture : MonoBehaviour
    {
        [Serializable]
        private sealed class SurveyPoint
        {
            public string id;
            public string capturedUtc;
            public string photo;
            public float x;
            public float y;
            public float z;
            public float distanceFromPreviousMetres;
        }

        [Serializable]
        private sealed class SurveyPhoto
        {
            public string capturedUtc;
            public string file;
            public float cameraX;
            public float cameraY;
            public float cameraZ;
            public float rotationX;
            public float rotationY;
            public float rotationZ;
            public float rotationW;
        }

        [Serializable]
        private sealed class SurveyData
        {
            public int schemaVersion = 1;
            public string createdUtc;
            public string deviceModel;
            public string coordinateSystem = "AR session local metres; resets on app restart";
            public string accuracyNotice = "AR estimates only. Verify against at least one known distance. Photos alone do not provide reliable scale.";
            public List<SurveyPoint> points = new List<SurveyPoint>();
            public List<SurveyPhoto> photos = new List<SurveyPhoto>();
        }

        [SerializeField] private ARSession arSession;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private Camera arCamera;

        private readonly List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
        private SurveyData survey;
        private string sessionDirectory;
        private string message = "Di chuyển chậm để AR nhận diện sàn.";
        private bool isCapturing;
        private bool arReady;
        private bool hideOverlay;

        private void Awake()
        {
            survey = new SurveyData
            {
                createdUtc = DateTime.UtcNow.ToString("o"),
                deviceModel = SystemInfo.deviceModel
            };
            sessionDirectory = Path.Combine(
                Application.persistentDataPath,
                "survey-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")
            );
            Directory.CreateDirectory(sessionDirectory);
            if (arSession != null)
                arSession.enabled = false;
        }

        private IEnumerator Start()
        {
            XRManagerSettings manager = XRGeneralSettings.Instance != null
                ? XRGeneralSettings.Instance.Manager
                : null;
            if (manager == null)
            {
                message = "Thiếu cấu hình ARCore trên thiết bị.";
                yield break;
            }

            if (!manager.isInitializationComplete)
                yield return manager.InitializeLoader();
            if (manager.activeLoader == null)
            {
                message = "ARCore không khởi động. Kiểm tra Google Play Services for AR.";
                yield break;
            }

            manager.StartSubsystems();
            if (arSession != null)
                arSession.enabled = true;
            arReady = true;
        }

        private void OnGUI()
        {
            if (hideOverlay)
                return;

            float scale = Mathf.Min(Screen.width / 420f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            GUI.Box(new Rect(12f, 14f, width - 24f, 180f), string.Empty);
            GUI.skin.label.fontSize = 19;
            GUI.Label(new Rect(26f, 24f, width - 52f, 30f), "JARVIS · KHẢO SÁT GẦN ĐÚNG");
            GUI.skin.label.fontSize = 15;
            GUI.Label(new Rect(26f, 58f, width - 52f, 34f), "AR: " + ARSession.state + " · Điểm: " + survey.points.Count + " · Ảnh: " + survey.photos.Count);
            GUI.Label(new Rect(26f, 94f, width - 52f, 62f), message);
            GUI.Label(new Rect(26f, 159f, width - 52f, 28f), "Không tự động tạo bản đồ hay đo chính xác từ ảnh.");

            float centreX = width / 2f;
            float centreY = height / 2f;
            Color previousColor = GUI.color;
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(centreX - 22f, centreY - 1f, 44f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centreX - 1f, centreY - 22f, 2f, 44f), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float buttonY = height - 184f;
            GUI.skin.button.fontSize = 17;
            GUI.enabled = arReady && ARSession.state == ARSessionState.SessionTracking && !isCapturing;
            if (GUI.Button(new Rect(14f, buttonY, (width - 42f) / 2f, 64f), "ĐÁNH DẤU ĐIỂM"))
                StartCoroutine(Capture(true));
            if (GUI.Button(new Rect(width / 2f + 7f, buttonY, (width - 42f) / 2f, 64f), "CHỤP ẢNH"))
                StartCoroutine(Capture(false));

            GUI.enabled = !isCapturing && survey.points.Count + survey.photos.Count > 0;
            if (GUI.Button(new Rect(14f, buttonY + 77f, width - 28f, 64f), "XUẤT GÓI ẢNH + SỐ ĐO VÀO DOWNLOADS"))
                Export();
            GUI.enabled = true;
        }

        private IEnumerator Capture(bool withPoint)
        {
            isCapturing = true;
            SurveyPoint point = null;
            if (withPoint)
            {
                if (raycastManager == null
                    || !raycastManager.Raycast(
                        new Vector2(Screen.width / 2f, Screen.height / 2f),
                        raycastHits,
                        TrackableType.PlaneWithinPolygon
                    ))
                {
                    message = "Chưa nhận được mặt sàn ở tâm màn hình. Hãy đi chậm và hướng máy vào vùng sàn trống.";
                    isCapturing = false;
                    yield break;
                }

                Vector3 position = raycastHits[0].pose.position;
                float distance = 0f;
                if (survey.points.Count > 0)
                {
                    SurveyPoint previous = survey.points[survey.points.Count - 1];
                    distance = Vector2.Distance(
                        new Vector2(previous.x, previous.z),
                        new Vector2(position.x, position.z)
                    );
                }

                point = new SurveyPoint
                {
                    id = "P" + (survey.points.Count + 1).ToString("D2"),
                    capturedUtc = DateTime.UtcNow.ToString("o"),
                    x = position.x,
                    y = position.y,
                    z = position.z,
                    distanceFromPreviousMetres = distance
                };
            }

            hideOverlay = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            hideOverlay = false;
            if (screenshot == null)
            {
                message = "Không chụp được ảnh; điểm chưa được lưu.";
                isCapturing = false;
                yield break;
            }

            string fileName = "photo-" + (survey.photos.Count + 1).ToString("D3") + ".jpg";
            byte[] jpeg = screenshot.EncodeToJPG(82);
            Destroy(screenshot);
            File.WriteAllBytes(Path.Combine(sessionDirectory, fileName), jpeg);
            Vector3 cameraPosition = arCamera.transform.position;
            Quaternion cameraRotation = arCamera.transform.rotation;
            survey.photos.Add(new SurveyPhoto
            {
                capturedUtc = DateTime.UtcNow.ToString("o"),
                file = fileName,
                cameraX = cameraPosition.x,
                cameraY = cameraPosition.y,
                cameraZ = cameraPosition.z,
                rotationX = cameraRotation.x,
                rotationY = cameraRotation.y,
                rotationZ = cameraRotation.z,
                rotationW = cameraRotation.w
            });

            if (point != null)
            {
                point.photo = fileName;
                survey.points.Add(point);
                message = point.id + " đã lưu · cách điểm trước ~" + point.distanceFromPreviousMetres.ToString("F2") + " m. Đo lại một đoạn thật để đối chiếu.";
            }
            else
            {
                message = "Đã lưu " + fileName + ". Ảnh không tự cung cấp kích thước.";
            }
            isCapturing = false;
        }

        private void Export()
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(sessionDirectory, "survey.json"),
                    JsonUtility.ToJson(survey, true)
                );
                string zipName = Path.GetFileName(sessionDirectory) + ".zip";
                string zipPath = Path.Combine(Application.persistentDataPath, zipName);
                using (FileStream archiveStream = File.Create(zipPath))
                using (ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
                {
                    foreach (string filePath in Directory.GetFiles(sessionDirectory))
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(filePath));
                        using (Stream input = File.OpenRead(filePath))
                        using (Stream output = entry.Open())
                            input.CopyTo(output);
                    }
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                SaveToAndroidDownloads(zipName, File.ReadAllBytes(zipPath));
                message = "Đã xuất Downloads/JarVisSurvey/" + zipName + ". Hãy gửi file ZIP cho mình khi bạn muốn.";
#else
                message = "Đã xuất " + zipPath;
#endif
            }
            catch (Exception exception)
            {
                message = "Xuất chưa thành công: " + exception.Message;
                Debug.LogException(exception);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void SaveToAndroidDownloads(string fileName, byte[] bytes)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (AndroidJavaClass downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads"))
            using (AndroidJavaObject collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            using (AndroidJavaObject values = new AndroidJavaObject("android.content.ContentValues"))
            {
                values.Call("put", "_display_name", fileName);
                values.Call("put", "mime_type", "application/zip");
                values.Call("put", "relative_path", "Download/JarVisSurvey");
                using (AndroidJavaObject uri = resolver.Call<AndroidJavaObject>("insert", collection, values))
                {
                    if (uri == null)
                        throw new IOException("Android không tạo được file trong Downloads.");
                    using (AndroidJavaObject output = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
                    {
                        if (output == null)
                            throw new IOException("Android không mở được file xuất.");
                        output.Call("write", bytes);
                        output.Call("flush");
                    }
                }
            }
        }
#endif
    }
}
