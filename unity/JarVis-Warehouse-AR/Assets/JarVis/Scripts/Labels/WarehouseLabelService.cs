using System;
using System.Globalization;
using JarVis.Warehouse.Data;
using JarVis.Warehouse.Localization;

namespace JarVis.Warehouse.Labels
{
    public sealed class WarehouseLabelService
    {
        public bool HasScannableEncoder => false;
        public string TracePayload(WarehouseItem item) => item?.itemId ?? string.Empty;

        public string BuildPreview(WarehouseItem item)
        {
            if (item == null)
                return string.Empty;

            bool vietnamese = JarvisLocalization.CurrentLanguage == SupportedLanguage.Vi;
            string manufactured = FormatDate(item.manufacturedDate);
            string expiry = FormatDate(item.expiryDate);
            string received = FormatDate(item.receivedDate);
            if (vietnamese)
            {
                return
                    $"JARVIS WAREHOUSE\n{item.productName}\nSKU: {item.sku}\nLOT: {item.lotNumber}\n"
                    + $"NSX: {manufactured}\nHSD: {expiry}\nNHẬP KHO: {received}\n"
                    + $"SL: {item.quantity} {TranslateUnit(item.unit)}\nVỊ TRÍ: {item.location}\n\n"
                    + $"TRACE: {TracePayload(item)}\nMÃ 2D DEMO — CHƯA MÃ HÓA / KHÔNG QUÉT ĐƯỢC";
            }

            return
                $"JARVIS WAREHOUSE\n{item.productName}\nSKU: {item.sku}\nLOT: {item.lotNumber}\n"
                + $"MFG: {manufactured}\nEXP: {expiry}\nRECEIVED: {received}\n"
                + $"QTY: {item.quantity} {TranslateUnit(item.unit)}\nLOCATION: {item.location}\n\n"
                + $"TRACE: {TracePayload(item)}\nDEMO 2D CODE — NOT ENCODED / NOT SCANNABLE";
        }

        public bool Validate(WarehouseItem item, out string errorKey)
        {
            if (item == null
                || string.IsNullOrWhiteSpace(item.itemId)
                || string.IsNullOrWhiteSpace(item.productName)
                || string.IsNullOrWhiteSpace(item.sku)
                || string.IsNullOrWhiteSpace(item.barcode)
                || item.quantity < 0)
            {
                errorKey = "label_required_fields";
                return false;
            }

            errorKey = null;
            return true;
        }

        private static string FormatDate(string isoDate)
        {
            return DateTime.TryParseExact(
                isoDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime value
            )
                ? value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : isoDate;
        }

        private static string TranslateUnit(string unit)
        {
            if (JarvisLocalization.CurrentLanguage == SupportedLanguage.Vi
                && string.Equals(unit, "box", StringComparison.OrdinalIgnoreCase))
            {
                return "hộp";
            }

            return unit;
        }
    }
}
