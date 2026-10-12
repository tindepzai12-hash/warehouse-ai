using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace JarVis.Warehouse.Backend
{
    // Keeps employee credentials out of Unity assets. The short-lived token only lives in memory.
    public static class OmsMobileSession
    {
        [Serializable]
        private sealed class LocalEndpoint
        {
            public string baseUrl;
        }

        public static string BaseUrl { get; private set; }
        public static string AccessToken { get; private set; }
        public static string EmployeeCode { get; private set; }
        public static DateTime ExpiresAtUtc { get; private set; }
        public static bool IsReady =>
            !string.IsNullOrWhiteSpace(BaseUrl) &&
            !string.IsNullOrWhiteSpace(AccessToken) &&
            DateTime.UtcNow < ExpiresAtUtc;

        public static bool TryLoadEndpoint(out string baseUrl)
        {
            baseUrl = null;
#if UNITY_EDITOR
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string file = string.IsNullOrWhiteSpace(root)
                ? string.Empty : Path.Combine(root, "Library", "JarvisBackend.local.json");
#else
            string file = Path.Combine(Application.persistentDataPath, "JarvisBackend.local.json");
#endif
            try
            {
                // Explicit local/device override takes precedence. Builds share this public
                // HTTPS endpoint for login, text, photo and voice; never bundle credentials.
                string json;
                if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                    json = File.ReadAllText(file);
                else
                {
                    TextAsset bundled = Resources.Load<TextAsset>("JarvisBackend");
                    if (bundled == null) return false;
                    json = bundled.text;
                }
                LocalEndpoint config = JsonUtility.FromJson<LocalEndpoint>(json);
                if (config == null || !Uri.TryCreate(config.baseUrl, UriKind.Absolute, out Uri endpoint))
                    return false;
                bool secure = endpoint.Scheme == Uri.UriSchemeHttps;
#if UNITY_EDITOR
                secure |= endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback;
#endif
                if (!secure || !string.IsNullOrEmpty(endpoint.UserInfo))
                    return false;
                baseUrl = config.baseUrl.TrimEnd('/');
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Set(string baseUrl, string token, string employeeCode, string expiresAt)
        {
            if (!DateTime.TryParse(expiresAt, out DateTime parsed))
                throw new ArgumentException("Invalid OMS session expiry");
            BaseUrl = baseUrl;
            AccessToken = token;
            EmployeeCode = employeeCode;
            ExpiresAtUtc = parsed.ToUniversalTime();
        }

        public static void Clear()
        {
            AccessToken = null;
            EmployeeCode = null;
            ExpiresAtUtc = DateTime.MinValue;
        }
    }

    public static class OmsMobileApi
    {
        [Serializable] public sealed class PresenceReply
        {
            public string status, timezone, serverNow, nextResetAt, sessionId, onlineStartedAt, leaseUntil;
            public int todaySeconds, sessionSeconds;
            public ActivityStreak streak;
        }
        [Serializable] public sealed class ActivityDay {public string date;public bool qualified;}
        [Serializable] public sealed class ActivityStreak
        {public int currentStreak,minimumSeconds;public bool todayQualified,scheduleAware;public string basis;public ActivityDay[] week;}
        [Serializable] sealed class PresenceBody { public string status; }
        [Serializable] public sealed class Ack { public bool ok; }
        public static IEnumerator GetPresence(Action<PresenceReply> done,Action<string> fail)
        {return Read("/api/oms/mobile/presence",done,fail);}
        public static IEnumerator SetPresence(bool online,Action<PresenceReply> done,Action<string> fail)
        {return Write("/api/oms/mobile/presence",JsonUtility.ToJson(new PresenceBody{status=online?"ONLINE":"OFFLINE"}),done,fail);}
        public static IEnumerator Heartbeat(Action<PresenceReply> done,Action<string> fail)
        {return Write("/api/oms/mobile/presence/heartbeat","{}",done,fail);}
        public static IEnumerator Logout(Action<Ack> done,Action<string> fail)
        {return Write("/api/oms/mobile/logout","{}",done,fail);}
        [Serializable] public sealed class MobileUser
        {
            public string id;
            public string employeeCode;
            public string role;
            public string language;
        }
        [Serializable] public sealed class LoginReply
        {
            public MobileUser user;
            public string accessToken;
            public string expiresAt;
        }
        [Serializable] public sealed class TaskRow
        {
            public string id;
            public string task_code;
            public string task_type;
            public string title;
            public string status;
            public string priority;
            public string employeeCode;
            public string sku;
            public string productName;
            public string barcode;
            public string unit;
            public string locationCode;
            public int expected_quantity;
        }
        [Serializable] public sealed class TaskList
        {
            public TaskRow[] tasks;
            public bool synthetic;
        }
        [Serializable] public sealed class InventoryRow
        {
            public string productId;
            public string sku;
            public string name;
            public string barcode;
            public string unit;
            public string lotId;
            public string lotCode;
            public string receivedAt;
            public string expiryDate;
            public string locationCode;
            public int onHand;
            public int reserved;
            public int damaged;
            public int available;
        }
        [Serializable] public sealed class InventoryList
        {
            public InventoryRow[] items;
            public bool synthetic;
        }
        [Serializable] public sealed class WarehouseMapReply
        {
            public int version;
            public WarehouseMapDocument document;
        }
        [Serializable] public sealed class WarehouseMapDocument
        {
            public int schemaVersion;
            public string warehouseId;
            public bool surveyVerified;
            public WarehouseMapFloor[] floors;
            public WarehouseMapNode[] nodes;
            public WarehouseMapEdge[] edges;
            public WarehouseMapLocation[] locations;
            public WarehouseMapMarker[] markers;
        }
        [Serializable] public sealed class WarehouseMapImage { public string mime, base64; }
        [Serializable] public sealed class WarehouseMapFloor
        {
            public string id, name;
            public float width, depth;
            public WarehouseMapImage image;
        }
        [Serializable] public sealed class WarehouseMapNode
        {
            public string id, floorId;
            public float x, z;
        }
        [Serializable] public sealed class WarehouseMapEdge
        {
            public string from, to;
            public bool blocked, oneWay;
        }
        [Serializable] public sealed class WarehouseMapLocation
        {
            public string code, floorId, accessNodeId;
            public float x, z;
        }
        [Serializable] public sealed class WarehouseMapMarker
        {
            public string id, nodeId, floorId;
            public float x, z, yawDegrees, widthMetres;
        }
        [Serializable] public sealed class Source
        {
            public string type;
            public string id;
        }
        [Serializable] public sealed class JarvisReply
        {
            public string message;
            public string language;
            public Source[] sources;
            public string mode;
            public bool synthetic;
        }
        [Serializable] public sealed class Conversation
        {
            public string id;
            public string employeeCode;
            public string status;
            public string latestMessage;
        }
        [Serializable] public sealed class ConversationList
        {
            public Conversation[] conversations;
        }
        [Serializable] public sealed class StaffMessage
        {
            public string id;
            public string senderId;
            public string sender;
            public string content;
            public string createdAt;
        }
        [Serializable] public sealed class MessageList
        {
            public StaffMessage[] messages;
        }
        [Serializable] private sealed class LoginBody
        {
            public string employeeCode;
            public string password;
        }
        [Serializable] private sealed class StatusBody
        {
            public string status;
            public string requestId;
        }
        [Serializable] private sealed class ChatBody
        {
            public string message;
            public ChatImage[] images;
        }
        [Serializable] public sealed class ChatImage { public string mimeType; public string data; }
        [Serializable] public sealed class Transcript { public string text; public string language; }
        [Serializable] public sealed class VoiceReply
        {
            public bool success; public string transcript; public string reply; public string message; public string messageId;
            public string language; public Source[] sources; public string mode; public bool synthetic;
            public JarvisReply Answer => new JarvisReply {message=reply,language=language,sources=sources,mode=mode,synthetic=synthetic};
        }
        // Explicit ownership releases the request even when Unity stops a nested coroutine.
        public sealed class VoiceUpload : IDisposable
        {
            UnityWebRequest request;
            public bool RetryAllowed { get; private set; }=true;
            [Serializable] private sealed class VoiceCapability { public string protocol; public bool idempotency; public int maxSeconds; }
            public IEnumerator Send(byte[] wav,Action<VoiceReply> completed,Action<string> failed,Action waiting,string messageId=null)
            {
                if(!OmsMobileSession.IsReady){failed?.Invoke("AUTH_REQUIRED: Đăng nhập để hỏi JarVis");yield break;}
                if(wav==null||wav.Length==0){failed?.Invoke("NO_AUDIO: Không có bản ghi âm");yield break;}
                if(wav.Length>2048044){failed?.Invoke("AUDIO_TOO_LARGE: Bản ghi vượt dung lượng cho phép");yield break;}
                if(!string.IsNullOrEmpty(messageId))
                {
                    request=UnityWebRequest.Get(OmsMobileSession.BaseUrl+"/api/oms/mobile/jarvis/voice");
                    request.timeout=15;request.SetRequestHeader("Authorization","Bearer "+OmsMobileSession.AccessToken);
                    try
                    {
                        yield return request.SendWebRequest();
                        if(request.result!=UnityWebRequest.Result.Success)
                        {
                            failed?.Invoke(request.responseCode==404?"SERVER_UPDATE_REQUIRED: Máy chủ chưa hỗ trợ tin nhắn thoại":request.responseCode==401?"AUTH_REQUIRED: Đăng nhập lại để dùng voice":"NETWORK_ERROR: Không kiểm tra được máy chủ voice");
                            if(request.responseCode==401)OmsMobileSession.Clear();
                            yield break;
                        }
                        VoiceCapability capability=null;try{capability=JsonUtility.FromJson<VoiceCapability>(request.downloadHandler.text);}catch(Exception){}
                        if(capability==null||capability.protocol!="voice-message-v1"||!capability.idempotency||capability.maxSeconds<30)
                        {failed?.Invoke("SERVER_UPDATE_REQUIRED: Máy chủ cần cập nhật voice");yield break;}
                    }
                    finally{Dispose();}
                }
                var form=new System.Collections.Generic.List<IMultipartFormSection>{new MultipartFormFileSection("audio",wav,"question.wav","audio/wav")};
                request=UnityWebRequest.Post(OmsMobileSession.BaseUrl+"/api/oms/mobile/jarvis/voice",form);
                request.timeout=110;request.downloadHandler=new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization","Bearer "+OmsMobileSession.AccessToken);request.SetRequestHeader("Accept","application/json");
                if(!string.IsNullOrEmpty(messageId))request.SetRequestHeader("X-Voice-Message-Id",messageId);
                try
                {
                    var operation=request.SendWebRequest();bool notified=false;
                    while(!operation.isDone){if(!notified&&request.uploadProgress>=1){notified=true;waiting?.Invoke();}yield return null;}
                    if(request.result!=UnityWebRequest.Result.Success)
                    {
                        ErrorBody error=null;try{error=JsonUtility.FromJson<ErrorBody>(request.downloadHandler.text);}catch(Exception){}
                        RetryAllowed=error?.retryPolicy!="never";
                        if(request.responseCode==401)OmsMobileSession.Clear();
                        failed?.Invoke(!string.IsNullOrEmpty(error?.code)?error.code+": "+error.detail:
                            (request.error!=null&&request.error.IndexOf("tim",StringComparison.OrdinalIgnoreCase)>=0?"TIMEOUT":"NETWORK_ERROR")+": Không nhận được phản hồi. Hãy thử lại.");yield break;
                    }
                    VoiceReply reply=null;try{reply=JsonUtility.FromJson<VoiceReply>(request.downloadHandler.text);}catch(Exception){}
                    if(reply==null||!reply.success||string.IsNullOrWhiteSpace(reply.transcript)||string.IsNullOrWhiteSpace(reply.reply)||(messageId!=null&&reply.messageId!=messageId))failed?.Invoke("SERVER_ERROR: Phản hồi giọng nói không hợp lệ");
                    else completed?.Invoke(reply);
                }
                finally{Dispose();}
            }
            public void Dispose(){if(request==null)return;request.Abort();request.Dispose();request=null;}
        }
        [Serializable] private sealed class AudioBody { public string audio; }
        [Serializable] private sealed class SpeechBody { public string text; }
        [Serializable] private sealed class MessageBody
        {
            public string content;
        }
        [Serializable] private sealed class ErrorBody
        {
            public string detail;
            public string code;
            public string retryPolicy;
        }

        public static IEnumerator Login(string baseUrl, string employeeCode, string password,
            Action<LoginReply> completed, Action<string> failed)
        {
            LoginBody body = new LoginBody { employeeCode = employeeCode, password = password };
            string response = null;
            string error = null;
            yield return Request(baseUrl, "/api/oms/mobile/login", "POST", JsonUtility.ToJson(body),
                false, value => response = value, value => error = value);
            if (error != null)
            {
                failed?.Invoke(error);
                yield break;
            }
            LoginReply reply = JsonUtility.FromJson<LoginReply>(response);
            if (reply == null || reply.user == null || string.IsNullOrWhiteSpace(reply.accessToken))
            {
                failed?.Invoke("OMS login response is invalid");
                yield break;
            }
            completed?.Invoke(reply);
        }

        public static IEnumerator GetTasks(Action<TaskList> completed, Action<string> failed)
        {
            yield return Read("/api/oms/mobile/tasks", completed, failed);
        }
        [Serializable] sealed class PasswordBody { public string currentPassword,newPassword; }
        [Serializable] public sealed class PasswordReply { public bool ok,loginRequired; }
        public static IEnumerator ChangePassword(string current,string next,Action<PasswordReply> completed,Action<string> failed)
        {
            yield return Write("/api/oms/mobile/account/password",JsonUtility.ToJson(new PasswordBody{currentPassword=current,newPassword=next}),completed,failed);
        }

        public static IEnumerator SearchInventory(string query, Action<InventoryList> completed, Action<string> failed)
        {
            yield return Read("/api/oms/mobile/inventory/search?q=" + Uri.EscapeDataString(query ?? string.Empty),
                completed, failed);
        }

        public static IEnumerator GetInventory(Action<InventoryList> completed, Action<string> failed)
        {
            yield return Read("/api/oms/mobile/inventory", completed, failed);
        }

        public static IEnumerator ChangeTaskStatus(string taskId, string status,
            Action<TaskRow> completed, Action<string> failed)
        {
            StatusBody body = new StatusBody { status = status, requestId = Guid.NewGuid().ToString() };
            yield return Write("/api/oms/mobile/tasks/" + Uri.EscapeDataString(taskId) + "/status",
                JsonUtility.ToJson(body), completed, failed);
        }

        public static IEnumerator AskJarvis(string question, Action<JarvisReply> completed, Action<string> failed)
        { return AskJarvis(question, null, completed, failed); }

        public static IEnumerator AskJarvis(string question, byte[] jpeg, Action<JarvisReply> completed, Action<string> failed)
        {
            yield return Write("/api/oms/mobile/jarvis/chat",
                JsonUtility.ToJson(new ChatBody { message = question, images = jpeg == null ? new ChatImage[0] : new[] { new ChatImage { mimeType="image/jpeg", data=Convert.ToBase64String(jpeg) } } }), completed, failed);
        }

        public static IEnumerator Transcribe(byte[] wav, Action<Transcript> completed, Action<string> failed)
        {
            yield return Write("/api/oms/mobile/jarvis/transcribe",JsonUtility.ToJson(new AudioBody { audio=Convert.ToBase64String(wav) }),completed,failed);
        }

        public sealed class SpeechDownload : IDisposable
        {
            internal UnityWebRequest request;
            public void Dispose(){if(request==null)return;request.Abort();request.Dispose();request=null;}
        }
        public static IEnumerator Speak(string text, Action<AudioClip> completed, Action<string> failed, SpeechDownload owner=null)
        {
            if(!OmsMobileSession.IsReady){failed?.Invoke("Đăng nhập để nghe JarVis");yield break;}
            owner=owner??new SpeechDownload();
            var request=UnityWebRequestMultimedia.GetAudioClip(OmsMobileSession.BaseUrl+"/api/oms/mobile/jarvis/speech",AudioType.MPEG);owner.request=request;
            try
            {
                request.method="POST";request.timeout=70;
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SpeechBody{text=text})));
                request.SetRequestHeader("Content-Type","application/json");
                request.SetRequestHeader("Authorization","Bearer "+OmsMobileSession.AccessToken);
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success)
                {
                    if(request.responseCode==401)OmsMobileSession.Clear();
                    failed?.Invoke("Không tải được giọng đọc ("+request.responseCode+")");yield break;
                }
                var clip=DownloadHandlerAudioClip.GetContent(request);
                if(clip==null){failed?.Invoke("Giọng đọc không hợp lệ");yield break;}
                completed?.Invoke(clip);
            }
            finally{owner.Dispose();}
        }

        public static IEnumerator GetConversations(Action<ConversationList> completed, Action<string> failed)
        {
            yield return Read("/api/oms/mobile/conversations", completed, failed);
        }

        public static IEnumerator GetWarehouseMap(Action<WarehouseMapReply> completed, Action<string> failed)
        {
            yield return Read("/api/oms/mobile/warehouse-map", completed, failed);
        }

        public static IEnumerator GetMessages(string conversationId, Action<MessageList> completed, Action<string> failed)
        {
            yield return Read("/api/oms/mobile/conversations/" + Uri.EscapeDataString(conversationId) + "/messages",
                completed, failed);
        }

        public static IEnumerator SendMessage(string conversationId, string content,
            Action<StaffMessage> completed, Action<string> failed)
        {
            yield return Write("/api/oms/mobile/conversations/" + Uri.EscapeDataString(conversationId) + "/messages",
                JsonUtility.ToJson(new MessageBody { content = content }), completed, failed);
        }

        private static IEnumerator Read<T>(string route, Action<T> completed, Action<string> failed) where T : class
        {
            if (!OmsMobileSession.IsReady)
            {
                failed?.Invoke("OMS employee session is unavailable");
                yield break;
            }
            string response = null;
            string error = null;
            yield return Request(OmsMobileSession.BaseUrl, route, "GET", null, true,
                value => response = value, value => error = value);
            if (error != null) { failed?.Invoke(error); yield break; }
            T value = JsonUtility.FromJson<T>(response);
            if (value == null) failed?.Invoke("OMS response is invalid");
            else completed?.Invoke(value);
        }

        private static IEnumerator Write<T>(string route, string body, Action<T> completed, Action<string> failed) where T : class
        {
            if (!OmsMobileSession.IsReady)
            {
                failed?.Invoke("OMS employee session is unavailable");
                yield break;
            }
            string response = null;
            string error = null;
            yield return Request(OmsMobileSession.BaseUrl, route, "POST", body, true,
                value => response = value, value => error = value);
            if (error != null) { failed?.Invoke(error); yield break; }
            T value = JsonUtility.FromJson<T>(response);
            if (value == null) failed?.Invoke("OMS response is invalid");
            else completed?.Invoke(value);
        }

        private static IEnumerator Request(string baseUrl, string route, string method, string body,
            bool authorized, Action<string> completed, Action<string> failed)
        {
            using (UnityWebRequest request = new UnityWebRequest(baseUrl.TrimEnd('/') + route, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = route.Contains("/jarvis/") ? 70 : 20;
                request.SetRequestHeader("Accept", "application/json");
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (authorized)
                    request.SetRequestHeader("Authorization", "Bearer " + OmsMobileSession.AccessToken);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ErrorBody error = null;
                    try { error = JsonUtility.FromJson<ErrorBody>(request.downloadHandler.text); }
                    catch (Exception) { }
                    if (request.responseCode == 401 && authorized)
                        OmsMobileSession.Clear();
                    failed?.Invoke(!string.IsNullOrWhiteSpace(error?.detail)
                        ? error.detail : "OMS network request failed (" + request.responseCode + ")");
                    yield break;
                }
                completed?.Invoke(request.downloadHandler.text);
            }
        }
    }
}
