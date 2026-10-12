using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace JarVis.Warehouse.Backend
{
    public sealed class JarvisApiClient : MonoBehaviour, IJarvisBackend
    {
        [SerializeField] private JarvisBackendConfig config;
        private string runtimeBaseUrl;
        private string bearerToken;
        private string sessionId;

        [Serializable]
        private sealed class LocalBackendSettings
        {
            public string baseUrl;
            public string prototypeToken;
        }

        [Serializable]
        private sealed class FeedbackObservation
        {
        }

        [Serializable]
        private sealed class FeedbackRequest
        {
            public string requestId;
            public string sessionId;
            public string taskId;
            public string language;
            public string locale;
            public string eventType;
            public string occurredAtUtc;
            public string question;
            public FeedbackObservation observation = new FeedbackObservation();
        }

        [Serializable]
        private sealed class FeedbackResponse
        {
            public string message;
            public WarehouseSource[] sources = Array.Empty<WarehouseSource>();
            public WarehouseAction action;
            public bool needsClarification;
            public string clarifyingQuestion;
            public string sessionId;
            public bool synthetic;
        }

        [Serializable]
        private sealed class ErrorResponse
        {
            public string detail;
        }

        public bool TryConfigureFromLocalSettings()
        {
#if UNITY_EDITOR
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            string path = string.IsNullOrWhiteSpace(projectRoot)
                ? string.Empty
                : Path.Combine(projectRoot, "Library", "JarvisBackend.local.json");
#else
            string path = Path.Combine(Application.persistentDataPath, "JarvisBackend.local.json");
#endif
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                LocalBackendSettings settings = JsonUtility.FromJson<LocalBackendSettings>(
                    File.ReadAllText(path)
                );
                if (settings == null
                    || string.IsNullOrWhiteSpace(settings.baseUrl)
                    || string.IsNullOrWhiteSpace(settings.prototypeToken))
                    return false;

                runtimeBaseUrl = settings.baseUrl.TrimEnd('/');
                bearerToken = settings.prototypeToken.Trim();
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[JarvisApiClient] Local backend settings are invalid: {error.Message}");
                return false;
            }
        }

        public void ResetConversation() { sessionId = null; }

        public IEnumerator SendChat(
            ChatRequest request,
            Action<ChatResponse> completed,
            Action<string> failed
        )
        {
            string configuredBaseUrl = !string.IsNullOrWhiteSpace(runtimeBaseUrl)
                ? runtimeBaseUrl
                : config != null && config.IsConfigured
                    ? config.GetEndpoint(string.Empty).TrimEnd('/')
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(configuredBaseUrl)
                || string.IsNullOrWhiteSpace(bearerToken))
            {
                failed?.Invoke("backend_not_configured");
                yield break;
            }

            FeedbackRequest feedbackRequest = new FeedbackRequest
            {
                requestId = Guid.NewGuid().ToString(),
                sessionId = sessionId,
                taskId = "TEST-PICK-001",
                language = request.language == "en" ? "en" : "vi",
                locale = request.language == "en" ? "en-GB" : "vi-VN",
                eventType = "user_question",
                occurredAtUtc = DateTime.UtcNow.ToString("o"),
                question = request.question
            };
            string endpoint = $"{configuredBaseUrl}/api/feedback";
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(feedbackRequest));
            using UnityWebRequest webRequest = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 65
            };
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                ErrorResponse error = null;
                try { error = JsonUtility.FromJson<ErrorResponse>(webRequest.downloadHandler.text); }
                catch (ArgumentException) { }
                failed?.Invoke(!string.IsNullOrWhiteSpace(error?.detail)
                    ? error.detail
                    : string.IsNullOrWhiteSpace(webRequest.error) ? "network_error" : webRequest.error);
                yield break;
            }

            FeedbackResponse response = JsonUtility.FromJson<FeedbackResponse>(webRequest.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.message))
            {
                failed?.Invoke("invalid_backend_response");
                yield break;
            }

            sessionId = response.sessionId;
            completed?.Invoke(new ChatResponse
            {
                message = response.needsClarification
                    && !string.IsNullOrWhiteSpace(response.clarifyingQuestion)
                        ? response.clarifyingQuestion
                        : response.message,
                sources = response.sources ?? Array.Empty<WarehouseSource>(),
                action = response.action,
                needsClarification = response.needsClarification,
                inventoryVerified = false,
                isMock = false
            });
        }
    }
}
