using System.Collections.Generic;
using JarVis.Warehouse.Data;

namespace JarVis.Warehouse.Providers
{
    public interface IInventoryProvider
    {
        InventoryResult GetInventory(string sku);
    }

    public interface ITaskProvider
    {
        IReadOnlyList<WarehouseTask> GetAssignedTasks(string employeeId);
        WarehouseTask FindTaskByBarcode(string barcode);
    }

    public interface IBarcodeProvider
    {
        WarehouseItem FindByBarcode(string barcode);
    }
}
