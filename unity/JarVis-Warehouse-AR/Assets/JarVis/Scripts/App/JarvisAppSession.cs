using JarVis.Warehouse.Backend;

namespace JarVis.Warehouse.App
{
    public enum WarehouseRole
    {
        Associate,
        Supervisor,
        Inventory
    }

    public static class JarvisAppSession
    {
        public static string EmployeeId { get; private set; } = "DEMO-001";
        public static string EmployeeDisplayName { get; private set; } = "Demo Associate";
        public static WarehouseRole Role { get; private set; } = WarehouseRole.Associate;
        public static bool IsAuthenticated { get; private set; }

        public static void SetAuthenticated(string employeeId, WarehouseRole role)
        {
            EmployeeId = string.IsNullOrWhiteSpace(employeeId) ? "DEMO-001" : employeeId.Trim();
            EmployeeDisplayName = EmployeeId;
            Role = role;
            IsAuthenticated = true;
        }

        public static void Clear()
        {
            OmsMobileSession.Clear();
            EmployeeId = "DEMO-001";
            EmployeeDisplayName = "Demo Associate";
            Role = WarehouseRole.Associate;
            IsAuthenticated = false;
        }
    }
}
