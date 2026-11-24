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
            public string assignedAt,assigned_at,createdAt,created_at;
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
        // Receipt protocol is preferred. Older deployed servers use the same authenticated
        // transcription/chat routes once; an ambiguous legacy upload is never auto-replayed.
        public sealed class VoiceUpload : IDisposable
        {
            UnityWebRequest request;
            public bool RetryAllowed { get; private set; } = true;
            [Serializable] sealed class VoiceCapability { public string protocol; public bool idempotency; public int maxSeconds; }
            public IEnumerator Send(byte[] wav, Action<VoiceReply> completed, Action<string> failed,
                Action waiting, string messageId = null, byte[] jpeg = null)
            {
                if (!OmsMobileSession.IsReady) { failed?.Invoke("AUTH_REQUIRED: Đăng nhập để hỏi JarVis"); yield break; }
                if (wav == null || wav.Length <= 44 || wav.Length > 2048044) { failed?.Invoke("INVALID_AUDIO: Bản ghi không hợp lệ"); yield break; }
                string endpoint = OmsMobileSession.BaseUrl, token = OmsMobileSession.AccessToken;
                bool receipts = false;
                request = UnityWebRequest.Get(endpoint + "/api/oms/mobile/jarvis/voice");
                request.timeout = 20; request.SetRequestHeader("Authorization", "Bearer " + token);
                try
                {
                    yield return request.SendWebRequest();
                    if (!SessionMatches(endpoint, token)) { failed?.Invoke("SESSION_CHANGED"); yield break; }
                    if (request.responseCode == 401)
                    {
                        bool verified=false;
                        yield return VerifyRejectedSession(endpoint, token,ok=>verified=ok);
                        if(!verified){failed?.Invoke(OmsMobileSession.IsReady?"AI_AUTH_ERROR: Voice capability rejected":"AUTH_REQUIRED: Đăng nhập lại");yield break;}
                        Debug.Log("[JarVis voice] Capability GET=401, session /me=200; trying authenticated legacy transcription once.");
                    }
                    else if (request.result == UnityWebRequest.Result.Success)
                    {
                        VoiceCapability capability = Parse<VoiceCapability>(request.downloadHandler.text);
                        receipts = capability != null && capability.protocol == "voice-message-v1" && capability.idempotency && capability.maxSeconds >= 30;
                        if (!receipts) { failed?.Invoke("SERVER_UPDATE_REQUIRED: Giao thức voice không hợp lệ"); yield break; }
                    }
                    else if (request.responseCode != 404 && request.responseCode != 405)
                    { failed?.Invoke(Failure(request)); yield break; }
                }
                finally { Dispose(); }
                if (!receipts || jpeg != null)
                {
                    // The legacy API has no receipt ID. Once a request is sent, require a new
                    // deliberate recording after a failure instead of risking duplicate charges.
                    RetryAllowed = false;
                    string transcript = null, error = null;
                    yield return Json(endpoint, token, "/api/oms/mobile/jarvis/transcribe",
                        JsonUtility.ToJson(new AudioBody { audio = Convert.ToBase64String(wav) }),
                        value => transcript = Parse<Transcript>(value)?.text, value => error = value, waiting);
                    if (error != null || string.IsNullOrWhiteSpace(transcript)) { failed?.Invoke(error ?? "EMPTY_TRANSCRIPT: Không nhận được lời nói"); yield break; }
                    if (transcript.Length > 1000) { failed?.Invoke("INVALID_AUDIO: Hãy ghi câu hỏi ngắn hơn"); yield break; }
                    string response = null;
                    yield return Json(endpoint, token, "/api/oms/mobile/jarvis/chat", ChatJson(transcript, jpeg),
                        value => response = value, value => error = value, waiting);
                    JarvisReply answer = Parse<JarvisReply>(response);
                    if (error != null || string.IsNullOrWhiteSpace(answer?.message)) { failed?.Invoke(error ?? "INVALID_RESPONSE: Phản hồi AI không hợp lệ"); yield break; }
                    completed?.Invoke(new VoiceReply { success = true, transcript = transcript, reply = answer.message,
                        messageId = messageId, language = answer.language, sources = answer.sources, mode = answer.mode, synthetic = answer.synthetic });
                    yield break;
                }
                var form = new System.Collections.Generic.List<IMultipartFormSection> { new MultipartFormFileSection("audio", wav, "question.wav", "audio/wav") };
                request = UnityWebRequest.Post(endpoint + "/api/oms/mobile/jarvis/voice", form);
                request.timeout = 110; request.SetRequestHeader("Authorization", "Bearer " + token);
                request.SetRequestHeader("Accept", "application/json");
                if (!string.IsNullOrEmpty(messageId)) request.SetRequestHeader("X-Voice-Message-Id", messageId);
                try
                {
                    var operation = request.SendWebRequest(); bool notified = false;
                    while (!operation.isDone) { if (!notified && request.uploadProgress >= 1) { notified = true; waiting?.Invoke(); } yield return null; }
                    if (!SessionMatches(endpoint, token)) { failed?.Invoke("SESSION_CHANGED"); yield break; }
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        var error = Parse<ErrorBody>(request.downloadHandler.text); RetryAllowed = error?.retryPolicy != "never";
                        if (request.responseCode == 401) yield return VerifyRejectedSession(endpoint, token);
                        failed?.Invoke(Failure(request)); yield break;
                    }
                    var reply = Parse<VoiceReply>(request.downloadHandler.text);
                    if (reply == null || !reply.success || string.IsNullOrWhiteSpace(reply.transcript) || string.IsNullOrWhiteSpace(reply.reply) || reply.messageId != messageId)
                        failed?.Invoke("INVALID_RESPONSE: Phản hồi giọng nói không hợp lệ");
                    else completed?.Invoke(reply);
                }
                finally { Dispose(); }
            }
            IEnumerator Json(string endpoint, string token, string route, string body, Action<string> done, Action<string> fail, Action waiting)
            {
                if (!SessionMatches(endpoint, token)) { fail?.Invoke("SESSION_CHANGED"); yield break; }
                request = new UnityWebRequest(endpoint + route, "POST") { timeout = 100,
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)), downloadHandler = new DownloadHandlerBuffer() };
                request.SetRequestHeader("Authorization", "Bearer " + token); request.SetRequestHeader("Content-Type", "application/json");
                try
                {
                    var operation = request.SendWebRequest(); waiting?.Invoke(); yield return operation;
                    if (!SessionMatches(endpoint, token)) { fail?.Invoke("SESSION_CHANGED"); yield break; }
                    if (request.result != UnityWebRequest.Result.Success)
                    { if (request.responseCode == 401) yield return VerifyRejectedSession(endpoint, token); Debug.LogWarning("[JarVis voice] "+(route.EndsWith("transcribe")?"transcription":"answer")+" HTTP="+request.responseCode);fail?.Invoke(Failure(request)); yield break; }
                    done?.Invoke(request.downloadHandler.text);
                }
                finally { Dispose(); }
            }
            public void Dispose() { if (request == null) return; request.Abort(); request.Dispose(); request = null; }
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
            // Share the startup health request, then retry that safe GET once if it failed.
            // Credentials are submitted once, only after the configured server replies.
            yield return Warmup(baseUrl);
            if(!ServerReady(baseUrl))yield return ProbeServer(baseUrl,true);
            if(!ServerReady(baseUrl)){failed?.Invoke(connectionError??"OMS network request failed (0) [CONNECTION]");yield break;}
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
            LoginReply reply = null;
            try { if (!string.IsNullOrWhiteSpace(response) && response.Length <= 2097152) reply = JsonUtility.FromJson<LoginReply>(response); }
            catch (Exception) { }
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
                ChatJson(question, jpeg), completed, failed);
        }

        public static IEnumerator AskJarvisForProduct(string question,byte[] jpeg,string verifiedBarcode,Action<JarvisReply> completed,Action<string> failed)
        {
            string body=ChatJson(question,jpeg,verifiedBarcode);
            var parsed=JsonUtility.FromJson<ChatBody>(body);
            if(parsed.message.Length>1000){failed?.Invoke("INVALID_INPUT: Hãy rút ngắn câu hỏi để gửi kèm thông tin sản phẩm");yield break;}
            yield return Write("/api/oms/mobile/jarvis/chat",body,completed,failed);
        }

        // Keep the language decision on the user's words, not the English task metadata.
        public static IEnumerator AskJarvisInTask(string question,string context,string previous,Action<JarvisReply> completed,Action<string> failed)
        {
            string q=(question??"").Trim();if(q.Length>360)q=q.Substring(0,360);
            bool english=JarvisChatLanguage.English(q,JarVis.Warehouse.Localization.ImportedAppLocalization.IsEnglish);
            string instruction=english?"Reply in English.":"Trả lời bằng tiếng Việt.";
            string tail="\n[App task context] "+context;
            int budget=1000-q.Length-instruction.Length-2;
            if(tail.Length>budget)tail=tail.Substring(0,budget);
            string message=q+tail+"\n"+instruction;
            yield return Write("/api/oms/mobile/jarvis/chat",JsonUtility.ToJson(new ChatBody{message=message,images=new ChatImage[0]}),completed,failed);
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
            string endpoint=OmsMobileSession.BaseUrl, token=OmsMobileSession.AccessToken;
            var request=UnityWebRequestMultimedia.GetAudioClip(endpoint+"/api/oms/mobile/jarvis/speech",AudioType.MPEG);owner.request=request;
            try
            {
                request.method="POST";request.timeout=70;
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SpeechBody{text=text})));
                request.SetRequestHeader("Content-Type","application/json");
                request.SetRequestHeader("Authorization","Bearer "+token);
                yield return request.SendWebRequest();
                if(!SessionMatches(endpoint,token)){failed?.Invoke("SESSION_CHANGED");yield break;}
                if(request.result!=UnityWebRequest.Result.Success)
                {
                    if(request.responseCode==401)yield return VerifyRejectedSession(endpoint,token);
                    failed?.Invoke(Failure(request));yield break;
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
            T value = null;
            try { if (!string.IsNullOrWhiteSpace(response) && response.Length <= 2097152) value = JsonUtility.FromJson<T>(response); }
            catch (Exception) { }
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
            T value = Parse<T>(response);
            if (value == null || value is JarvisReply answer && string.IsNullOrWhiteSpace(answer.message)) failed?.Invoke("INVALID_RESPONSE: Máy chủ trả phản hồi không hợp lệ");
            else completed?.Invoke(value);
        }

        internal static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 2097152) return null;
            try { return JsonUtility.FromJson<T>(json); } catch (Exception) { return null; }
        }
        internal static bool SessionMatches(string endpoint, string token) =>
            OmsMobileSession.IsReady && OmsMobileSession.BaseUrl == endpoint && OmsMobileSession.AccessToken == token;
        // /me is the authority for the mobile session. A provider 401 is not a logout.
        internal static IEnumerator VerifyRejectedSession(string endpoint, string token, Action<bool> verified=null)
        {
            if (!SessionMatches(endpoint, token)) yield break;
            using (var check = UnityWebRequest.Get(endpoint + "/api/oms/mobile/me"))
            {
                check.timeout = 12; check.SetRequestHeader("Authorization", "Bearer " + token);
                yield return check.SendWebRequest();
                if (check.responseCode == 401 && SessionMatches(endpoint, token)) OmsMobileSession.Clear();
                verified?.Invoke(check.responseCode==200&&check.result==UnityWebRequest.Result.Success&&SessionMatches(endpoint,token));
            }
        }
        internal static string Failure(UnityWebRequest request)
        {
            if (request.responseCode == 401) return OmsMobileSession.IsReady
                ? "AI_AUTH_ERROR: Kết nối AI phía máy chủ bị từ chối; phiên JarVis được giữ"
                : "AUTH_REQUIRED: Phiên đăng nhập đã hết hạn";
            if (request.responseCode == 429) return "RATE_LIMIT: Đã chạm giới hạn lượt; chờ rồi thử lại";
            if (request.responseCode == 404 || request.responseCode == 405) return "SERVER_UPDATE_REQUIRED: Máy chủ chưa hỗ trợ chức năng này";
            if (request.responseCode == 413) return "MEDIA_TOO_LARGE: Ảnh hoặc âm thanh vượt giới hạn máy chủ";
            if (request.responseCode == 409) return "BUSY: JarVis đang xử lý một yêu cầu khác";
            if (request.responseCode >= 500) return "AI_UNAVAILABLE: Máy chủ AI tạm thời chưa phản hồi; dữ liệu nhập được giữ";
            if (request.responseCode == 0) return NetworkFailureKind(request.error) + ": Không kết nối được máy chủ";
            return "REQUEST_REJECTED: Máy chủ từ chối yêu cầu (" + request.responseCode + ")";
        }
        internal static string ChatJson(string question, byte[] jpeg,string verifiedBarcode=null)
        {
            // Owner-supplied test metadata is context, never an unconditional image answer.
            string message = question ?? "";
            bool english=JarvisChatLanguage.English(message,JarVis.Warehouse.Localization.ImportedAppLocalization.IsEnglish);
            if(JarVis.Warehouse.UI.JarvisLocalCatalogue.Find(verifiedBarcode)!=null)message=JarVis.Warehouse.UI.JarvisLocalCatalogue.Context(message,verifiedBarcode,english);
            else if (jpeg != null) message = JarvisTestProduct.PhotoContext(message,english);
            string instruction=english?"\nReply in English.":"\nTrả lời bằng tiếng Việt.";
            if(message.Length+instruction.Length<=1000)message+=instruction;
            return JsonUtility.ToJson(new ChatBody { message = message, images = jpeg == null ? new ChatImage[0] :
                new[] { new ChatImage { mimeType = "image/jpeg", data = Convert.ToBase64String(jpeg) } } });
        }
        public static string FriendlyError(string error, bool english)
        {
            string code=(error??"").Split(':')[0];
            switch(code)
            {
                case "AUTH_REQUIRED": return english?"Session expired. Sign in again; your input is kept.":"Phiên đăng nhập hết hạn. Đăng nhập lại; nội dung nhập được giữ.";
                case "AI_AUTH_ERROR": return english?"The AI service rejected this request. Your JarVis session is kept. The server connection needs checking.":"Dịch vụ AI từ chối yêu cầu này. Phiên JarVis vẫn được giữ. Cần kiểm tra kết nối và quyền của dịch vụ trên máy chủ.";
                case "SERVER_UPDATE_REQUIRED": return english?"The server does not support this feature yet.":"Máy chủ chưa hỗ trợ chức năng này; cần cập nhật backend.";
                case "RATE_LIMIT": return english?"Request limit reached. Wait before trying again.":"Đã chạm giới hạn lượt AI. Chờ rồi thử lại.";
                case "BUSY": return english?"JarVis is processing another request. Please wait.":"JarVis đang xử lý yêu cầu khác. Chờ một chút rồi thử lại.";
                case "SESSION_CHANGED": return english?"Account changed. Submit again in the current account.":"Tài khoản đã thay đổi. Gửi lại trong tài khoản hiện tại.";
                case "INVALID_RESPONSE": return english?"The server returned an invalid response. Input kept.":"Máy chủ trả phản hồi không hợp lệ. Nội dung nhập được giữ.";
                case "AI_UNAVAILABLE": return english?"AI service is temporarily unavailable. Input kept.":"Dịch vụ AI tạm thời chưa sẵn sàng. Nội dung nhập được giữ.";
                default: return english?"Request failed. Check connection; your photo/recording is kept. Older servers may require a new recording.":"Gửi chưa thành công. Kiểm tra mạng; ảnh/bản ghi được giữ. Máy chủ cũ có thể cần bản ghi mới để tránh gửi trùng.";
            }
        }
        [Serializable] sealed class HealthReply { public string status; }
        static double nextWarmup,healthyAt=-100;
        static string checkingEndpoint,healthyEndpoint,connectionError;
        public static bool CheckingServer {get;private set;}
        static bool ServerReady(string endpoint)=>healthyEndpoint==endpoint.TrimEnd('/')&&Time.realtimeSinceStartupAsDouble-healthyAt<45;
        public static IEnumerator Warmup(string endpoint)
        {
            yield return ProbeServer(endpoint,false);
        }
        static IEnumerator ProbeServer(string endpoint,bool retry)
        {
            endpoint=endpoint.TrimEnd('/');
            while(CheckingServer)yield return null;
            if(ServerReady(endpoint)||(!retry&&checkingEndpoint==endpoint&&Time.realtimeSinceStartupAsDouble<nextWarmup))yield break;
            CheckingServer=true;checkingEndpoint=endpoint;nextWarmup=Time.realtimeSinceStartupAsDouble+30;
            double started=Time.realtimeSinceStartupAsDouble;
            try
            {
                using(var request=UnityWebRequest.Get(endpoint+"/api/health"))
                {
                    request.timeout=20;request.SetRequestHeader("Accept","application/json");yield return request.SendWebRequest();
                    var health=request.result==UnityWebRequest.Result.Success?Parse<HealthReply>(request.downloadHandler.text):null;
                    if(health?.status=="ok"){healthyEndpoint=endpoint;healthyAt=Time.realtimeSinceStartupAsDouble;connectionError=null;}
                    else connectionError=request.result==UnityWebRequest.Result.Success?"OMS login response is invalid":"OMS network request failed ("+request.responseCode+") ["+NetworkFailureKind(request.error)+"]";
                    Debug.Log("[JarVis connection] health HTTP="+request.responseCode+" ready="+ServerReady(endpoint)+" seconds="+(Time.realtimeSinceStartupAsDouble-started).ToString("F1"));
                }
            }
            finally{CheckingServer=false;}
        }

        private static IEnumerator Request(string baseUrl, string route, string method, string body,
            bool authorized, Action<string> completed, Action<string> failed)
        {
            string token = OmsMobileSession.AccessToken;
            if (authorized && !SessionMatches(baseUrl, token)) { failed?.Invoke("AUTH_REQUIRED"); yield break; }
            using (UnityWebRequest request = new UnityWebRequest(baseUrl.TrimEnd('/') + route, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = RequestTimeoutSeconds(route);
                request.SetRequestHeader("Accept", "application/json");
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (authorized)
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                yield return request.SendWebRequest();
                if (authorized && !SessionMatches(baseUrl, token)) { failed?.Invoke("SESSION_CHANGED"); yield break; }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (request.responseCode == 401 && authorized)
                        yield return VerifyRejectedSession(baseUrl, token);
                    // Login retains its public error classification; authenticated media never
                    // exposes provider error bodies or destroys a session on upstream failure.
                    failed?.Invoke(authorized ? Failure(request) :
                        "OMS network request failed (" + request.responseCode + ") [" + NetworkFailureKind(request.error) + "]");
                    yield break;
                }
                completed?.Invoke(request.downloadHandler.text);
            }
        }
        public static int RequestTimeoutSeconds(string route)
        {
            return route == "/api/oms/mobile/login" ? 90 : route.Contains("/jarvis/") ? 70 : 20;
        }
        public static string NetworkFailureKind(string message)
        {
            string value = (message ?? "").ToLowerInvariant();
            if (value.Contains("ssl") || value.Contains("certificate") || value.Contains("tls")) return "TLS";
            if (value.Contains("resolve") || value.Contains("dns")) return "DNS";
            if (value.Contains("timeout") || value.Contains("timed out")) return "TIMEOUT";
            return "CONNECTION";
        }
    }
}
