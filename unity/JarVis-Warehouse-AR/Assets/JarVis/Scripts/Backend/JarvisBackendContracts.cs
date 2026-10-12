using System;
using System.Collections;
using JarVis.Warehouse.Data;

namespace JarVis.Warehouse.Backend
{
    [Serializable]
    public sealed class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public sealed class WarehouseSource
    {
        public string documentId;
        public string sectionId;
        public string id;
        public string title;
        public string type;
    }

    [Serializable]
    public sealed class WarehouseAction
    {
        public string type;
        public string exceptionCode;
        public string targetId;
        public string payload;
    }

    [Serializable]
    public sealed class ChatRequest
    {
        public string question;
        public string language;
        public string role;
        public string taskId;
        public string sku;
        public string barcode;
        public string location;
        public ChatMessage[] history = Array.Empty<ChatMessage>();
    }

    [Serializable]
    public sealed class ChatResponse
    {
        public string message;
        public WarehouseSource[] sources = Array.Empty<WarehouseSource>();
        public WarehouseAction action;
        public bool needsClarification;
        public bool inventoryVerified;
        public bool isMock;
    }

    public interface IJarvisBackend
    {
        IEnumerator SendChat(
            ChatRequest request,
            Action<ChatResponse> completed,
            Action<string> failed
        );
    }

    public interface IWarehouseBackendGateway
    {
        IEnumerator GetTasks(Action<WarehouseTask[]> completed, Action<string> failed);
        IEnumerator GetInventory(string sku, Action<InventoryResult> completed, Action<string> failed);
        IEnumerator SubmitBarcode(string barcode, Action<WarehouseItem> completed, Action<string> failed);
    }
}
