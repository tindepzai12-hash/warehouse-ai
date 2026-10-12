using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using JarVis.Warehouse.Backend;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // Figma lower-row frame 182:2. All figures come from OMS, never the design snapshot.
    public sealed class WarehouseInventoryScreen : MonoBehaviour
    {
        private enum ExpiryFilter { All, NearExpiry, Expired }

        private static readonly Color Blue = Hex("1762C4");
        private static readonly Color Navy = Hex("10264B");
        private static readonly Color Muted = Hex("52627C");
        private static readonly Color Background = Hex("F4F8FF");
        private static readonly Color Line = Hex("E4EBF5");
        private static readonly Color Warning = Hex("B96A0D");
        private static readonly Color Danger = Hex("C73D43");
        private static Sprite roundedSprite;

        private OmsMobileApi.InventoryRow[] rows = Array.Empty<OmsMobileApi.InventoryRow>();
        private ExpiryFilter filter;
        [SerializeField] private InputField search;
        [SerializeField] private Text status;
        [SerializeField] private Text allChip;
        [SerializeField] private Text nearChip;
        [SerializeField] private Text expiredChip;
        [SerializeField] private Text lotsCount;
        [SerializeField] private Text nearCount;
        [SerializeField] private Text expiredCount;
        [SerializeField] private RectTransform cardContent;
        [SerializeField] private ScrollRect cardScroll;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField] private UnityEngine.UI.Button refreshButton;
        [SerializeField] private UnityEngine.UI.Button searchButton;
        [SerializeField] private UnityEngine.UI.Button allButton;
        [SerializeField] private UnityEngine.UI.Button nearButton;
        [SerializeField] private UnityEngine.UI.Button expiredButton;
        [SerializeField] private UnityEngine.UI.Button homeButton;
        [SerializeField] private UnityEngine.UI.Button tasksButton;
        [SerializeField] private UnityEngine.UI.Button scanButton;
        [SerializeField] private UnityEngine.UI.Button mapButton;
        [SerializeField] private UnityEngine.UI.Button jarvisButton;
        private Coroutine refreshRoutine;
        private Action<string> lookup;
        private Action chatManager;

        public void Build(Action goHome, Action goTasks, Action goScanner, Action goMap,
            Action goJarvis, Action<string> lookupInventory, Action openManagerChat)
        {
            lookup = lookupInventory;
            chatManager = openManagerChat;
            if (search != null || transform.Find("Header") != null)
            {
                BindExisting(goHome, goTasks, goScanner, goMap, goJarvis);
                UpdateCounts();
                ApplyFilter();
                return;
            }
            RectTransform root = (RectTransform)transform;
            Image(root, "Page Background", Background, 0, 0, 1, 1, false);

            RectTransform header = Image(root, "Header", Color.white, 0, .89f, 1, 1, false);
            backButton = Button(header, "Back", "‹", 0.035f, .12f, .13f, .88f,
                Blue, Color.white, goHome, 48);
            Label(header, "Title", "Lô hàng", .17f, .14f, .70f, .86f, 52, Navy, FontStyle.Bold);
            refreshButton = Button(header, "Refresh", "↻", .84f, .18f, .96f, .82f,
                Color.white, Blue, Refresh, 44);

            RectTransform searchBox = Image(root, "Search", Color.white, .04f, .82f, .96f, .875f, true);
            search = Input(searchBox, "Tìm barcode, SKU hoặc mã lô...", .035f, 0, .85f, 1);
            searchButton = Button(searchBox, "Search Button", "⌕", .85f, .04f, .99f, .96f,
                Blue, Color.white, ApplyFilter, 42);
            search.onEndEdit.AddListener(_ => ApplyFilter());

            RectTransform filters = Image(root, "Expiry Filters", Background, .04f, .76f, .96f, .815f, false);
            allChip = Chip(filters, "All", 0, .34f, () => SelectFilter(ExpiryFilter.All));
            nearChip = Chip(filters, "Near Expiry", .36f, .67f,
                () => SelectFilter(ExpiryFilter.NearExpiry));
            expiredChip = Chip(filters, "Expired", .69f, 1,
                () => SelectFilter(ExpiryFilter.Expired));
            allButton = allChip.transform.parent.GetComponent<UnityEngine.UI.Button>();
            nearButton = nearChip.transform.parent.GetComponent<UnityEngine.UI.Button>();
            expiredButton = expiredChip.transform.parent.GetComponent<UnityEngine.UI.Button>();

            RectTransform stats = Image(root, "Statistics", Background, .04f, .675f, .96f, .75f, false);
            lotsCount = Stat(stats, "Lots", "Số lô", 0, .32f, Blue);
            nearCount = Stat(stats, "Near", "Sắp hết", .34f, .66f, Warning);
            expiredCount = Stat(stats, "Expired", "Quá HSD", .68f, 1, Danger);

            status = Label(root, "Inventory Status", "", .055f, .626f, .945f, .672f,
                30, Muted, FontStyle.Normal);
            RectTransform scrollRoot = Image(root, "Lot Scroll", Background, .04f, .125f, .96f, .625f, false);
            cardScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            cardScroll.horizontal = false;
            cardScroll.vertical = true;
            cardScroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = Image(scrollRoot, "Viewport", Background, 0, 0, 1, 1, false);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            cardContent = Image(viewport, "Content", Background, 0, 1, 1, 1, false);
            cardContent.pivot = new Vector2(.5f, 1f);
            var layout = cardContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            cardContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            cardScroll.viewport = viewport;
            cardScroll.content = cardContent;

            RectTransform nav = Image(root, "Bottom Navigation", Color.white, 0, 0, 1, .12f, false);
            Image(nav, "Top Divider", Line, 0, .985f, 1, 1, false);
            homeButton = Nav(nav, "Trang chủ", 0, .20f, goHome);
            tasksButton = Nav(nav, "Nhiệm vụ", .20f, .40f, goTasks);
            scanButton = Nav(nav, "Quét mã", .40f, .60f, goScanner);
            mapButton = Nav(nav, "Bản đồ", .60f, .80f, goMap);
            jarvisButton = Nav(nav, "JarVis", .80f, 1, goJarvis);
            UpdateCounts();
            ApplyFilter();
        }

        // A prefab owns the editable visual hierarchy; runtime only reconnects behavior/data.
        private void BindExisting(Action goHome, Action goTasks, Action goScanner,
            Action goMap, Action goJarvis)
        {
            RepairReferences();
            search.onEndEdit.RemoveAllListeners();
            search.onEndEdit.AddListener(_ => ApplyFilter());
            BindButton(backButton, "Header/Back", goHome);
            BindButton(refreshButton, "Header/Refresh", Refresh);
            BindButton(searchButton, "Search/Search Button", ApplyFilter);
            BindButton(allButton, "Expiry Filters/All", () => SelectFilter(ExpiryFilter.All));
            BindButton(nearButton, "Expiry Filters/Near Expiry",
                () => SelectFilter(ExpiryFilter.NearExpiry));
            BindButton(expiredButton, "Expiry Filters/Expired",
                () => SelectFilter(ExpiryFilter.Expired));
            BindButton(homeButton, "Bottom Navigation/Trang chủ", goHome);
            BindButton(tasksButton, "Bottom Navigation/Nhiệm vụ", goTasks);
            BindButton(scanButton, "Bottom Navigation/Quét mã", goScanner);
            BindButton(mapButton, "Bottom Navigation/Bản đồ", goMap);
            BindButton(jarvisButton, "Bottom Navigation/JarVis", goJarvis);
        }

        [ContextMenu("Repair Inventory View References")]
        public void RepairReferences()
        {
            if (search == null) search = Find<InputField>("Search/Query Input");
            if (status == null) status = Find<Text>("Inventory Status");
            if (allChip == null) allChip = Find<Text>("Expiry Filters/All/Label");
            if (nearChip == null) nearChip = Find<Text>("Expiry Filters/Near Expiry/Label");
            if (expiredChip == null) expiredChip = Find<Text>("Expiry Filters/Expired/Label");
            if (lotsCount == null) lotsCount = Find<Text>("Statistics/Lots/Value");
            if (nearCount == null) nearCount = Find<Text>("Statistics/Near/Value");
            if (expiredCount == null) expiredCount = Find<Text>("Statistics/Expired/Value");
            if (cardContent == null) cardContent = Find<RectTransform>("Lot Scroll/Viewport/Content");
            if (cardScroll == null) cardScroll = Find<ScrollRect>("Lot Scroll");
            if (backButton == null) backButton = Find<UnityEngine.UI.Button>("Header/Back");
            if (refreshButton == null) refreshButton = Find<UnityEngine.UI.Button>("Header/Refresh");
            if (searchButton == null) searchButton = Find<UnityEngine.UI.Button>("Search/Search Button");
            if (allButton == null) allButton = Find<UnityEngine.UI.Button>("Expiry Filters/All");
            if (nearButton == null) nearButton = Find<UnityEngine.UI.Button>("Expiry Filters/Near Expiry");
            if (expiredButton == null) expiredButton = Find<UnityEngine.UI.Button>("Expiry Filters/Expired");
            if (homeButton == null) homeButton = Find<UnityEngine.UI.Button>("Bottom Navigation/Trang chủ");
            if (tasksButton == null) tasksButton = Find<UnityEngine.UI.Button>("Bottom Navigation/Nhiệm vụ");
            if (scanButton == null) scanButton = Find<UnityEngine.UI.Button>("Bottom Navigation/Quét mã");
            if (mapButton == null) mapButton = Find<UnityEngine.UI.Button>("Bottom Navigation/Bản đồ");
            if (jarvisButton == null) jarvisButton = Find<UnityEngine.UI.Button>("Bottom Navigation/JarVis");
        }

        private T Find<T>(string path) where T : Component =>
            transform.Find(path)?.GetComponent<T>();

        private static void BindButton(UnityEngine.UI.Button button, string path, Action action)
        {
            if (button == null) throw new InvalidOperationException("Inventory prefab is missing " + path);
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }

        public void Refresh()
        {
            if (refreshRoutine != null) StopCoroutine(refreshRoutine);
            refreshRoutine = StartCoroutine(Load());
        }

        private IEnumerator Load()
        {
            if (!OmsMobileSession.IsReady)
            {
                rows = Array.Empty<OmsMobileApi.InventoryRow>();
                status.text = "Đăng nhập tài khoản nhân viên để xem dữ liệu kho.";
                UpdateCounts();
                ApplyFilter();
                refreshRoutine = null;
                yield break;
            }
            status.text = "Đang tải dữ liệu kho...";
            OmsMobileApi.InventoryList result = null;
            string error = null;
            yield return OmsMobileApi.GetInventory(value => result = value, value => error = value);
            if (result == null)
            {
                rows = Array.Empty<OmsMobileApi.InventoryRow>();
                status.text = "Không tải được dữ liệu: " + (error ?? "Lỗi kết nối");
                UpdateCounts();
                ApplyFilter();
            }
            else
            {
                rows = result.items ?? Array.Empty<OmsMobileApi.InventoryRow>();
                status.text = result.synthetic ? "Dữ liệu mô phỏng OMS" : "Dữ liệu kho cập nhật từ OMS";
                UpdateCounts();
                ApplyFilter();
            }
            refreshRoutine = null;
        }

        private void SelectFilter(ExpiryFilter value)
        {
            filter = value;
            ApplyFilter();
        }

        private void UpdateCounts()
        {
            lotsCount.text = rows.Length.ToString(CultureInfo.InvariantCulture);
            nearCount.text = rows.Count(row => Expiry(row.expiryDate) == ExpiryFilter.NearExpiry)
                .ToString(CultureInfo.InvariantCulture);
            expiredCount.text = rows.Count(row => Expiry(row.expiryDate) == ExpiryFilter.Expired)
                .ToString(CultureInfo.InvariantCulture);
            allChip.text = "Tất cả (" + rows.Length + ")";
            nearChip.text = "Sắp hết (" + nearCount.text + ")";
            expiredChip.text = "Quá HSD (" + expiredCount.text + ")";
        }

        private void ApplyFilter()
        {
            var chips = new[] { allChip, nearChip, expiredChip };
            for (int i = 0; i < chips.Length; i++)
            {
                Image image = chips[i].transform.parent.GetComponent<Image>();
                bool active = i == (int)filter;
                image.color = active ? Blue : Color.white;
                chips[i].color = active ? Color.white : i == 1 ? Warning : i == 2 ? Danger : Navy;
            }
            for (int i = cardContent.childCount - 1; i >= 0; i--)
            {
                GameObject oldCard = cardContent.GetChild(i).gameObject;
                oldCard.SetActive(false);
                if (Application.isPlaying) Destroy(oldCard);
                else DestroyImmediate(oldCard);
            }
            string query = search.text.Trim();
            OmsMobileApi.InventoryRow[] visible = rows.Where(row =>
                (filter == ExpiryFilter.All || Expiry(row.expiryDate) == filter) &&
                (query.Length == 0 ||
                 Contains(row.sku, query) || Contains(row.name, query) ||
                 Contains(row.barcode, query) || Contains(row.lotCode, query)))
                .ToArray();
            if (visible.Length == 0)
                EmptyCard(query.Length > 0 ? "Không tìm thấy lô hàng phù hợp."
                    : rows.Length == 0 ? "Kho chưa có dữ liệu hàng hóa."
                    : "Không có lô hàng trong bộ lọc này.");
            else
                foreach (OmsMobileApi.InventoryRow row in visible) AddCard(row);
            MobileTextStyle.Apply(transform);
            cardScroll.verticalNormalizedPosition = 1;
        }

        private void EmptyCard(string message)
        {
            RectTransform card = Image(cardContent, "Empty State", Color.white, 0, 0, 1, 1, true);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 260;
            Label(card, "Message", message, .05f, .05f, .95f, .95f,
                36, Muted, FontStyle.Normal).alignment = TextAnchor.MiddleCenter;
        }

        private void AddCard(OmsMobileApi.InventoryRow row)
        {
            RectTransform card = Image(cardContent, "Lot " + row.lotCode, Color.white, 0, 0, 1, 1, true);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 410;
            Label(card, "Product and Lot", Safe(row.name) + " · " + Safe(row.lotCode),
                .045f, .72f, .79f, .92f, 36, Navy, FontStyle.Bold);
            Label(card, "SKU and Barcode", Safe(row.sku) + " · " + Safe(row.barcode),
                .045f, .55f, .95f, .72f, 29, Muted, FontStyle.Normal);
            Label(card, "Location", "⌖  Kệ " + Safe(row.locationCode),
                .045f, .37f, .62f, .55f, 30, Muted, FontStyle.Normal);
            Label(card, "Available", row.available + " " + Safe(row.unit),
                .64f, .37f, .95f, .55f, 36, Blue, FontStyle.Bold).alignment = TextAnchor.MiddleRight;
            ExpiryFilter expiry = Expiry(row.expiryDate);
            string badge = expiry == ExpiryFilter.Expired ? "Quá HSD"
                : expiry == ExpiryFilter.NearExpiry ? "Sắp hết hạn" : "Còn hạn";
            Color badgeColor = expiry == ExpiryFilter.Expired ? Danger
                : expiry == ExpiryFilter.NearExpiry ? Warning : Hex("14806E");
            Label(card, "Expiry Badge", badge, .72f, .76f, .96f, .91f,
                26, badgeColor, FontStyle.Bold).alignment = TextAnchor.MiddleRight;
            Image(card, "Divider", Line, .045f, .32f, .955f, .325f, false);
            Button(card, "Lookup", "Tra cứu", .045f, .055f, .48f, .27f,
                Hex("E8F2FF"), Blue, () => lookup?.Invoke(
                    !string.IsNullOrWhiteSpace(row.barcode) ? row.barcode : row.sku), 31);
            Button(card, "Manager Chat", "Chat quản lý", .52f, .055f, .955f, .27f,
                Blue, Color.white, () => chatManager?.Invoke(), 31);
        }

        private static bool Contains(string value, string query) =>
            !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static ExpiryFilter Expiry(string value)
        {
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                return ExpiryFilter.All;
            int days = (date.Date - DateTime.Today).Days;
            return days < 0 ? ExpiryFilter.Expired
                : days <= 30 ? ExpiryFilter.NearExpiry : ExpiryFilter.All;
        }

        private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

        private static UnityEngine.UI.Button Nav(RectTransform parent, string text,
            float x0, float x1, Action action) =>
            Button(parent, text, text, x0, .08f, x1, .78f,
                Color.white, text == "Quét mã" ? Blue : Muted, action, 29);

        private static Text Stat(RectTransform parent, string name, string label,
            float x0, float x1, Color valueColor)
        {
            RectTransform box = Image(parent, name, Color.white, x0, 0, x1, 1, true);
            Label(box, "Caption", label, .08f, .50f, .92f, .95f, 27, Muted, FontStyle.Normal);
            return Label(box, "Value", "0", .08f, .08f, .92f, .53f, 42, valueColor, FontStyle.Bold);
        }

        private static Text Chip(RectTransform parent, string name, float x0, float x1, Action action)
        {
            Button button = Button(parent, name, "", x0, .1f, x1, .9f, Color.white, Navy, action, 30);
            return button.GetComponentInChildren<Text>();
        }

        private static RectTransform Image(Transform parent, string name, Color color,
            float x0, float y0, float x1, float y1, bool rounded)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            if (rounded)
            {
                image.sprite = RoundedSprite();
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return rect;
        }

        private static Text Label(Transform parent, string name, string value,
            float x0, float y0, float x1, float y1, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text label = go.GetComponent<Text>();
            label.font = Resources.Load<Font>("BeVietnamPro-Regular") ??
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(22, size / 2);
            label.resizeTextMaxSize = size;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(Transform parent, string name, string text,
            float x0, float y0, float x1, float y1, Color background, Color foreground,
            Action action, int fontSize)
        {
            RectTransform rect = Image(parent, name, background, x0, y0, x1, y1, true);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label(rect, "Label", text, .04f, .04f, .96f, .96f,
                fontSize, foreground, FontStyle.Bold).alignment = TextAnchor.MiddleCenter;
            if (action != null) button.onClick.AddListener(() => action());
            return button;
        }

        private static Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            roundedSprite = Resources.Load<Sprite>("JarVisRoundedPanel");
            if (roundedSprite != null) return roundedSprite;
            const int n = 32;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 7.5f);
                float dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 7.5f);
                texture.SetPixel(x, y, new Color(1, 1, 1,
                    Mathf.Clamp01(8.5f - Mathf.Sqrt(dx * dx + dy * dy))));
            }
            texture.Apply();
            roundedSprite = Sprite.Create(texture, new Rect(0, 0, n, n),
                new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(9, 9, 9, 9));
            roundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return roundedSprite;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color value);
            return value;
        }

        private static InputField Input(Transform parent, string placeholder,
            float x0, float y0, float x1, float y1)
        {
            RectTransform rect = Image(parent, "Query Input", Color.white, x0, y0, x1, y1, false);
            UnityEngine.UI.Image image = rect.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            Text placeholderText = Label(rect, "Placeholder", placeholder,
                .015f, .05f, .985f, .95f, 31, Muted, FontStyle.Normal);
            Text valueText = Label(rect, "Value", "", .015f, .05f, .985f, .95f,
                33, Navy, FontStyle.Normal);
            valueText.supportRichText = false;
            input.placeholder = placeholderText;
            input.textComponent = valueText;
            input.characterLimit = 120;
            return input;
        }
    }
}
