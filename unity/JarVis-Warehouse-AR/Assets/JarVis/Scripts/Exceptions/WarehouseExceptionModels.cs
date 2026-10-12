using System;
using System.Collections.Generic;

namespace JarVis.Warehouse.Exceptions
{
    public enum WarehouseExceptionType
    {
        ShortPick,
        ItemOnFloor,
        Damaged,
        WrongLocation,
        BarcodeUnreadable,
        SkuNotFound,
        NoReplacement,
        WmsDisconnected,
        DeviceOffline
    }

    public enum WarehouseExceptionStatus
    {
        Submitted,
        UnderReview,
        Resolved
    }

    [Serializable]
    public sealed class WarehouseExceptionReport
    {
        public string exceptionId;
        public WarehouseExceptionType type;
        public string taskId;
        public string sku;
        public string barcode;
        public string description;
        public string location;
        public string createdBy;
        public string createdAtUtc;
        public WarehouseExceptionStatus status;
        public bool supervisorReviewed;
        public string photoPlaceholder;
        public bool isMock;
    }

    public sealed class WarehouseExceptionService
    {
        private readonly List<WarehouseExceptionReport> reports = new List<WarehouseExceptionReport>();

        public IReadOnlyList<WarehouseExceptionReport> Reports => reports;

        public WarehouseExceptionReport Create(
            WarehouseExceptionType type,
            string taskId,
            string sku,
            string barcode,
            string description,
            string location,
            string createdBy
        )
        {
            WarehouseExceptionReport report = new WarehouseExceptionReport
            {
                exceptionId = $"EXC-DEMO-{DateTime.UtcNow:yyyyMMddHHmmss}",
                type = type,
                taskId = taskId?.Trim(),
                sku = sku?.Trim(),
                barcode = barcode?.Trim(),
                description = description?.Trim(),
                location = location?.Trim(),
                createdBy = createdBy?.Trim(),
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                status = WarehouseExceptionStatus.Submitted,
                supervisorReviewed = false,
                photoPlaceholder = "PHOTO PLACEHOLDER — no upload service configured",
                isMock = true
            };
            reports.Add(report);
            return report;
        }
    }
}
