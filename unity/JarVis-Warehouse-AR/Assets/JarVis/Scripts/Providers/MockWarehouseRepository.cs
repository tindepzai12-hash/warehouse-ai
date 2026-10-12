using System;
using System.Collections.Generic;
using System.Linq;
using JarVis.Warehouse.Data;

namespace JarVis.Warehouse.Providers
{
    public sealed class MockWarehouseRepository : IInventoryProvider, ITaskProvider, IBarcodeProvider
    {
        private readonly List<WarehouseTask> tasks = new List<WarehouseTask>
        {
            new WarehouseTask
            {
                taskId = "TASK-DEMO-001",
                type = WarehouseTaskType.Picking,
                sku = "ICE-CREAM-001",
                productName = "Kem vani / Vanilla ice cream",
                barcode = "8931234567890",
                requiredQuantity = 5,
                actualQuantity = 3,
                location = "B-12-03",
                priority = WarehouseTaskPriority.High,
                status = WarehouseTaskStatus.Assigned,
                language = "vi",
                isMock = true
            },
            new WarehouseTask
            {
                taskId = "TASK-DEMO-002",
                type = WarehouseTaskType.Receiving,
                sku = "MILK-BOX-024",
                productName = "S\u1EEFa h\u1ED9p / Milk boxes",
                barcode = "8931234567005",
                requiredQuantity = 24,
                actualQuantity = 24,
                location = "IN-A-03",
                priority = WarehouseTaskPriority.Normal,
                status = WarehouseTaskStatus.Assigned,
                language = "vi",
                isMock = true
            },
            new WarehouseTask
            {
                taskId = "TASK-DEMO-003",
                type = WarehouseTaskType.CycleCount,
                sku = "COFFEE-500G",
                productName = "C\u00E0 ph\u00EA 500 g / Coffee 500 g",
                barcode = "8931234567012",
                requiredQuantity = 18,
                actualQuantity = 18,
                location = "C-07-02",
                priority = WarehouseTaskPriority.High,
                status = WarehouseTaskStatus.NeedsReview,
                language = "vi",
                isMock = true
            }
        };

        private readonly List<WarehouseItem> items = new List<WarehouseItem>
        {
            new WarehouseItem
            {
                itemId = "ITEM-2026-0001",
                productName = "Kem vani / Vanilla ice cream",
                sku = "ICE-CREAM-001",
                barcode = "8931234567890",
                lotNumber = "LOT-2026-09-A",
                manufacturedDate = "2026-09-01",
                expiryDate = "2027-09-01",
                receivedDate = "2026-09-25",
                quantity = 24,
                unit = "box",
                location = "B-12-03",
                supplier = "Supplier Demo",
                status = "unverified",
                taskId = "TASK-DEMO-001",
                receivingCode = "RCV-DEMO-001",
                language = "vi",
                isMock = true
            },
            new WarehouseItem
            {
                itemId = "ITEM-2026-0002",
                productName = "S\u1EEFa h\u1ED9p / Milk boxes",
                sku = "MILK-BOX-024",
                barcode = "8931234567005",
                lotNumber = "LOT-2026-09-B",
                manufacturedDate = "2026-08-15",
                expiryDate = "2027-02-15",
                receivedDate = "2026-09-25",
                quantity = 24,
                unit = "box",
                location = "IN-A-03",
                supplier = "Supplier Demo",
                status = "unverified",
                taskId = "TASK-DEMO-002",
                receivingCode = "RCV-DEMO-002",
                language = "vi",
                isMock = true
            },
            new WarehouseItem
            {
                itemId = "ITEM-2026-0003",
                productName = "C\u00E0 ph\u00EA 500 g / Coffee 500 g",
                sku = "COFFEE-500G",
                barcode = "8931234567012",
                lotNumber = "LOT-2026-09-C",
                manufacturedDate = "2026-07-01",
                expiryDate = "2027-07-01",
                receivedDate = "2026-09-20",
                quantity = 18,
                unit = "bag",
                location = "C-07-02",
                supplier = "Supplier Demo",
                status = "unverified",
                taskId = "TASK-DEMO-003",
                receivingCode = "RCV-DEMO-003",
                language = "vi",
                isMock = true
            }
        };

        public IReadOnlyList<WarehouseTask> GetAssignedTasks(string employeeId)
        {
            return tasks;
        }

        public WarehouseTask FindTaskByBarcode(string barcode)
        {
            return tasks.FirstOrDefault(task => string.Equals(task.barcode, barcode, StringComparison.Ordinal));
        }

        public WarehouseItem FindByBarcode(string barcode)
        {
            return items.FirstOrDefault(item => string.Equals(item.barcode, barcode, StringComparison.Ordinal));
        }

        public InventoryResult GetInventory(string sku)
        {
            WarehouseItem item = items.FirstOrDefault(
                value => string.Equals(value.sku, sku, StringComparison.OrdinalIgnoreCase)
            );
            return new InventoryResult
            {
                sku = sku,
                quantity = item?.quantity ?? 0,
                location = item?.location ?? string.Empty,
                inventoryVerified = false,
                isMock = true,
                message = "Inventory has not been verified directly."
            };
        }
    }
}
