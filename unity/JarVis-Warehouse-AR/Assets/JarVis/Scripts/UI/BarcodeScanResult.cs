using System;
using System.Linq;
using JarVis.Warehouse.Backend;

namespace JarVis.Warehouse.UI
{
    public enum ScannerState { Idle, RequestingPermission, Starting, Scanning, Decoding, LookingUp, Result, Paused, Error, Released }
    public enum ScannerContext { ProductLookup, WarehouseCheckpoint }
    [Serializable] public sealed class BarcodeScanResult
    {
        public string RawValue, BarcodeFormat, Source, Timestamp, ValidationStatus, NormalizedIdentifier, MatchedProductId, ErrorCode;
        public ScannerContext Context;
    }
    public static class ScannerValidation
    {
        public const int MaximumLength = 160;
        public static BarcodeScanResult Validate(string raw, string format, string source)
        {
            var result = new BarcodeScanResult { RawValue = raw, BarcodeFormat = format, Source = source,
                Timestamp = DateTime.UtcNow.ToString("O"), NormalizedIdentifier = raw, ValidationStatus = "Invalid", ErrorCode = "INVALID_CODE" };
            if (string.IsNullOrWhiteSpace(raw)) { result.ErrorCode = "EMPTY_CODE"; return result; }
            if (raw.Length > MaximumLength) { result.ErrorCode = "CODE_TOO_LONG"; return result; }
            if (raw.Any(char.IsControl) || raw != raw.Trim()) return result;
            if (raw.StartsWith("{") || raw.StartsWith("[") || raw.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase)) { result.ErrorCode = "UNSUPPORTED_CONTENT"; return result; }
            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && new[] { "http", "https", "javascript", "data", "file", "mailto", "tel" }.Contains(uri.Scheme.ToLowerInvariant()))
            { result.ErrorCode = "UNSUPPORTED_CONTENT"; return result; }
            result.Context = raw.StartsWith("JWM:", StringComparison.Ordinal) ? ScannerContext.WarehouseCheckpoint : ScannerContext.ProductLookup;
            result.ValidationStatus = "Valid"; result.ErrorCode = null; return result;
        }
        public static OmsMobileApi.InventoryRow[] ExactMatches(OmsMobileApi.InventoryList reply, string code, out bool lotOnly)
        {
            var items = (reply?.items ?? Array.Empty<OmsMobileApi.InventoryRow>()).Where(row => row != null).ToArray();
            var products = items.Where(row => row.barcode == code || row.sku == code).ToArray();
            lotOnly = products.Length == 0;
            return lotOnly ? items.Where(row => row.lotCode == code).ToArray() : products;
        }
        public static bool SameIdentity(string endpoint, string token, string employee)
        {
            return OmsMobileSession.IsReady && endpoint == OmsMobileSession.BaseUrl && token == OmsMobileSession.AccessToken && employee == OmsMobileSession.EmployeeCode;
        }
    }
}
