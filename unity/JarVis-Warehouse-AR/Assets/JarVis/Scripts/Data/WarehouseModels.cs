using System;

namespace JarVis.Warehouse.Data
{
    public enum WarehouseTaskType
    {
        Picking,
        Receiving,
        CycleCount
    }

    public enum WarehouseTaskPriority
    {
        Normal,
        High
    }

    public enum WarehouseTaskStatus
    {
        Assigned,
        InProgress,
        ShortPick,
        Damaged,
        Completed,
        NeedsReview,
        Accepted
    }

    [Serializable]
    public sealed class WarehouseTask
    {
        public string taskId;
        public string taskCode;
        public WarehouseTaskType type;
        public string sku;
        public string productName;
        public string barcode;
        public int requiredQuantity;
        public int actualQuantity;
        public string location;
        public WarehouseTaskPriority priority;
        public WarehouseTaskStatus status;
        public string language;
        public bool isMock;
    }

    [Serializable]
    public sealed class WarehouseItem
    {
        public string itemId;
        public string productName;
        public string sku;
        public string barcode;
        public string lotNumber;
        public string manufacturedDate;
        public string expiryDate;
        public string receivedDate;
        public int quantity;
        public string unit;
        public string location;
        public string supplier;
        public string status;
        public string taskId;
        public string receivingCode;
        public string language;
        public bool isMock;
    }

    [Serializable]
    public sealed class InventoryResult
    {
        public string sku;
        public int quantity;
        public string location;
        public bool inventoryVerified;
        public bool isMock;
        public string message;
    }
}
