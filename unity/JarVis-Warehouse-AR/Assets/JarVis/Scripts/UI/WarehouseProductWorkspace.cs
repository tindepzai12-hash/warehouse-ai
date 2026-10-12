using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;

namespace JarVis.Warehouse.UI
{
    public sealed class WarehouseProductWorkspace : MonoBehaviour
    {
        [Serializable] public sealed class Category { public string id, label; }
        [Serializable] public sealed class Attribute { public string id, label, kind, unit; public string[] choices, units; public bool required; }
        [Serializable] public sealed class Template { public string id, category, familyLabel, name; public bool manual, expiryPolicy; public Attribute[] attributes; public string[] units; }
        [Serializable] public sealed class Catalog { public Category[] categories; public Template[] templates; }
        ImportedFigmaNavigation nav;
        TMP_FontAsset font;
        Catalog catalog;
        GameObject page;
        RectTransform content;
        readonly Dictionary<string, string> values = new Dictionary<string, string>();
        readonly Dictionary<string, Dictionary<string, string>> drafts = new Dictionary<string, Dictionary<string, string>>();
        readonly HashSet<string> expandedFamilies = new HashSet<string>();
        OmsMobileApi.InventoryRow[] inventory = Array.Empty<OmsMobileApi.InventoryRow>();
        Template selected;
        OmsMobileApi.InventoryRow labelProduct;
        string tab = "products", category = "drink", mode = "list", query = "", message = "", appearance, employee;
        bool loading, additional, codesExpanded, validationMessage, knownInventory, sessionReady;
        string sessionEndpoint;
        Action choiceBack;
        Action labelBack;
        int step;
        float cursor;
        public int TemplateCount => catalog?.templates?.Length ?? 0;
        public string CurrentMode => mode;
        public string CurrentMessage => message;
        public bool Loading => loading;
        bool En => ImportedAppLocalization.IsEnglish;
        bool Dark => nav.Settings.Dark;
        string T(string vi, string en) => En ? en : vi;
        string Display(string source) => ImportedAppLocalization.Text(source);
        string Value(string name) => values.TryGetValue(name, out var value) ? value : "";

        public void Configure(ImportedFigmaNavigation owner)
        {
            nav = owner;
            font = GetComponentInChildren<TMP_Text>(true).font;
            var asset = Resources.Load<TextAsset>("JarVisProductTemplates");
            catalog = asset == null ? new Catalog { categories = Array.Empty<Category>(), templates = Array.Empty<Template>() } : JsonUtility.FromJson<Catalog>(asset.text);
            foreach (var item in catalog.categories)
            {
                var first = catalog.templates.FirstOrDefault(template => template.category == item.id);
                if (first != null) expandedFamilies.Add(item.id + "|" + first.familyLabel);
            }
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            employee = OmsMobileSession.EmployeeCode;
            sessionEndpoint = OmsMobileSession.BaseUrl; sessionReady = OmsMobileSession.IsReady;
            Build();
        }
        void OnEnable() { if (nav == null) return; SynchronizeSession(); if (!knownInventory && !loading) StartCoroutine(LoadInventory()); }
        void OnDisable() { StopAllCoroutines(); loading = false; }
        void Update()
        {
            if (nav == null) return;
            if (SynchronizeSession()) StartCoroutine(LoadInventory());
            string next = Dark + ":" + En + ":" + ImportedSettings.TextScale;
            if (appearance != next) { if (validationMessage) message = ValidateDraft(); RebuildKeepingOffset(); }
        }
        bool SynchronizeSession()
        {
            if (employee == OmsMobileSession.EmployeeCode && sessionEndpoint == OmsMobileSession.BaseUrl && sessionReady == OmsMobileSession.IsReady) return false;
            employee = OmsMobileSession.EmployeeCode; sessionEndpoint = OmsMobileSession.BaseUrl; sessionReady = OmsMobileSession.IsReady;
            inventory = Array.Empty<OmsMobileApi.InventoryRow>(); values.Clear(); drafts.Clear(); labelProduct = null; labelBack = null; selected = null; mode = "list"; knownInventory = false; message = "";
            StopAllCoroutines(); loading = false; return true;
        }
        IEnumerator LoadInventory()
        {
            if (!OmsMobileSession.IsReady) { message = T("Đăng nhập để tra cứu sản phẩm đã có. Bản nháp chưa tạo hàng thật.", "Sign in to find existing products. Drafts do not create real stock."); Build(); yield break; }
            loading = true; message = ""; Build();
            string requestedEmployee = employee, requestedEndpoint = sessionEndpoint;
            yield return OmsMobileApi.GetInventory(reply =>
            {
                if (!OmsMobileSession.IsReady || requestedEmployee != OmsMobileSession.EmployeeCode || requestedEndpoint != OmsMobileSession.BaseUrl) return;
                if (reply.synthetic) { message = T("API trả dữ liệu mẫu; không dùng để tạo tem hàng thật.", "The API returned synthetic data; it cannot be used for real labels."); return; }
                AcceptInventory(reply.items ?? Array.Empty<OmsMobileApi.InventoryRow>());
            }, error => message = T("Chưa tải được sản phẩm. Kiểm tra kết nối rồi thử lại.", "Products could not be loaded. Check the connection and retry."));
            loading = false; Build();
        }
        public void AcceptInventory(OmsMobileApi.InventoryRow[] rows)
        {
            inventory = rows.Where(row => row != null && !string.IsNullOrWhiteSpace(row.sku))
                .GroupBy(row => (row.productId ?? "") + "|" + row.sku + "|" + row.barcode)
                .Select(group => group.First()).ToArray(); knownInventory = true; if (nav != null) Build();
        }
        public static string SearchKey(string text)
        {
            var result = new StringBuilder();
            foreach (char character in (text ?? "").Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(character))
                    result.Append(char.ToLowerInvariant(character == 'đ' || character == 'Đ' ? 'd' : character));
            return result.ToString();
        }
        public static bool MatchesSearch(string text, string query)
        {
            string normalized = SearchKey(text), search = SearchKey(query);
            if (normalized.Contains(search)) return true;
            var tokens = Regex.Matches(search, "[a-z]+|[0-9]+").Cast<Match>().Select(match => match.Value).ToArray();
            return tokens.Length > 0 && tokens.All(normalized.Contains);
        }
        void Build()
        {
            if (nav == null) return;
            choiceBack = null;
            appearance = Dark + ":" + En + ":" + ImportedSettings.TextScale;
            if (page != null) { page.SetActive(false); Destroy(page); }
            page = PrototypeUi.Shell(transform, font, T("Sản phẩm & tem", "Products & labels"), Dark, Back, () => nav.Show("prototype:tools"), out content).gameObject;
            string[] tabs = { "products", "labels", "manage" };
            string[] names = { T("Sản phẩm", "Products"), T("Tem sản phẩm", "Labels"), T("Quản lý", "Manage") };
            for (int index = 0; index < tabs.Length; index++)
            {
                string destination = tabs[index];
                ActionAt(names[index], index * 124, 0, 118, () => { if (selected != null && mode == "create") drafts[selected.id] = new Dictionary<string, string>(values); labelBack = null; tab = destination; mode = "list"; query = ""; message = ""; validationMessage = false; Build(); }, tab == destination);
            }
            cursor = 66;
            if (loading) { Copy(T("Đang tải sản phẩm…", "Loading products…")); Finish(); return; }
            if (!string.IsNullOrEmpty(message)) Copy(Display(message), 64);
            if (tab == "manage")
            {
                Heading(T("Cấu hình dành cho quản lý", "Manager configuration"));
                Copy(T("Không tự chọn vai trò. API mobile chưa cấp chức năng cấu hình danh mục hoặc in; quyền phải do backend xác nhận.", "Roles are not self-selected. Mobile catalog and print permissions must be supplied by the backend."), 100);
                Finish(); return;
            }
            if (mode == "label") { LabelScreen(); Finish(); return; }
            if (mode == "create") { CreateScreen(); Finish(); return; }
            Copy(tab == "labels" ? T("Tem rách? Tìm đúng hàng rồi dùng lại mã gốc.", "Damaged label? Find the item and reuse its original code.") : T("Hàng đã có: dùng sản phẩm cũ. Chỉ tạo nháp nếu là hàng mới.", "Use an existing product when possible. Draft only a new product."), 56);
            Input(T("Tìm tên, SKU hoặc barcode", "Search name, SKU or barcode"), "search", query, text => query = text, Build);
            if (tab == "products") Action(T("Soạn sản phẩm mới · bản nháp", "Prepare new product · draft"), BeginCreate, true);
            if (tab == "products" && selected != null && drafts.ContainsKey(selected.id)) Action(T("Tiếp tục bản nháp: ", "Resume draft: ") + Value("name"), () => { mode = "create"; step = 1; message = ""; Build(); });
            var matches = inventory.Where(row => MatchesSearch(row.name + " " + row.sku + " " + row.barcode, query))
                .OrderBy(row => SearchRank(row, query)).ToArray();
            if (matches.Length == 0) Copy(T("Chưa có sản phẩm phù hợp. Thử tên ngắn hơn hoặc tải lại; đừng tạo lại SKU chỉ vì mất tem.", "No matching product. Try a shorter name or reload; missing labels do not require a new SKU."), 80);
            foreach (var product in matches.Take(60)) ProductRow(product);
            if (matches.Length > 60) Copy(T("Có hơn 60 kết quả. Nhập tên/mã cụ thể để thu hẹp.", "More than 60 results. Search a more specific name or code."));
            Action(T("Tải lại sản phẩm", "Reload products"), () => { if (!loading) StartCoroutine(LoadInventory()); });
            Finish();
        }
        void ProductRow(OmsMobileApi.InventoryRow product, Action returnToDraft = null)
        {
            var row = PrototypeUi.Panel(content, "Product row " + product.sku, 0, cursor, 366, 88, Dark, 12);
            PrototypeUi.Button(row, () => { labelBack = returnToDraft; labelProduct = product; mode = "label"; tab = "labels"; message = ""; Build(); });
            var name = PrototypeUi.Text(row, font, product.name, 12, 7, 312, 25, 14, Dark, true);
            name.overflowMode = TextOverflowModes.Ellipsis;
            PrototypeUi.Text(row, font, product.sku + " · " + product.unit, 12, 33, 312, 22, 12, Dark, false, true);
            var code = PrototypeUi.Text(row, font, "Barcode: " + (string.IsNullOrWhiteSpace(product.barcode) ? T("Thiếu mã gốc", "Missing original code") : product.barcode), 12, 56, 312, 22, 12, Dark, false, true);
            code.overflowMode = TextOverflowModes.Ellipsis;
            PrototypeUi.Icon(row, "chevron", 334, 35, 18, PrototypeUi.Muted(Dark));
            cursor += 96;
        }
        public static int SearchRank(OmsMobileApi.InventoryRow product, string query)
        {
            string code = (query ?? "").Trim();
            return code != "" && (string.Equals(product.sku, code, StringComparison.OrdinalIgnoreCase) || string.Equals(product.barcode, code, StringComparison.Ordinal)) ? 0 : 1;
        }
        public void BeginCreate() { labelBack = null; tab = "products"; mode = "create"; step = 0; query = ""; message = ""; validationMessage = false; Build(); }
        public void SelectTemplate(string id)
        {
            if (selected != null) drafts[selected.id] = new Dictionary<string, string>(values);
            selected = catalog.templates.First(template => template.id == id); category = selected.category;
            values.Clear();
            if (drafts.TryGetValue(id, out var draft)) foreach (var pair in draft) values[pair.Key] = pair.Value;
            else { values["name"] = selected.manual ? "" : Display(selected.name); values["unit"] = selected.units.FirstOrDefault() ?? "Cái"; values["skuMode"] = "auto"; values["barcodeMode"] = "internal"; values["pack"] = "single"; values["expiryPolicy"] = selected.expiryPolicy ? "yes" : "no"; }
            step = 1; query = ""; message = ""; validationMessage = false; additional = false; codesExpanded = Value("skuMode") == "manual" || Value("barcodeMode") == "package" || Value("pack") != "single"; Build();
        }
        public void SetValue(string key, string value) { values[key] = value; }
        void CreateScreen()
        {
            Copy(T("1 Chọn mẫu → 2 Điền thông tin → 3 Xác nhận", "1 Template → 2 Information → 3 Confirm"), 40);
            if (step == 0)
            {
                for (int index = 0; index < catalog.categories.Length; index++)
                {
                    var item = catalog.categories[index];
                    ActionAt(Display(item.label), index % 2 * 188, cursor + index / 2 * 60, 178, () => { category = item.id; query = ""; Build(); }, category == item.id);
                }
                cursor += Mathf.Ceil(catalog.categories.Length / 2f) * 60;
                Input(T("Tìm mẫu trong ngành", "Search templates in category"), "templateSearch", query, text => query = text, Build);
                var groups = catalog.templates.Where(item => item.category == category && MatchesSearch(item.name + " " + Display(item.name), query)).GroupBy(item => item.familyLabel).ToArray();
                if (groups.Length == 0) Copy(T("Không tìm thấy mẫu. Thử từ khóa ngắn hơn hoặc nhập thủ công.", "No template found. Try a shorter query or enter manually."), 64);
                foreach (var group in groups)
                {
                    string familyKey = category + "|" + group.Key;
                    bool expanded = query != "" || expandedFamilies.Contains(familyKey);
                    if (query != "") Heading(Display(group.Key));
                    else Action(Display(group.Key) + (expanded ? T(" · Thu gọn", " · Collapse") : T(" · Mở nhóm", " · Expand")), () => { if (!expandedFamilies.Remove(familyKey)) expandedFamilies.Add(familyKey); RebuildKeepingOffset(); });
                    if (expanded) foreach (var template in group) Action(Display(template.name), () => SelectTemplate(template.id), false, "Template " + template.id);
                }
                Action(T("Không có mẫu? Nhập thủ công", "No template? Enter manually"), () => SelectTemplate(catalog.templates.First(item => item.category == category && item.manual).id));
                return;
            }
            if (selected == null) { step = 0; return; }
            if (step == 2)
            {
                Heading(T("Kiểm tra bản nháp", "Check your draft"));
                Copy(Value("name") + "\n" + T("Đơn vị lấy hàng: ", "Picking unit: ") + Display(Value("unit")), 56);
                foreach (var attribute in selected.attributes.Where(item => !string.IsNullOrWhiteSpace(Value(item.id)))) Copy(Display(attribute.label) + ": " + (attribute.kind == "enum" ? Display(Value(attribute.id)) : Value(attribute.id)) + " " + Value(attribute.id + "Unit"), 40);
                if (Value("brand") != "") Copy(T("Thương hiệu: ", "Brand: ") + Value("brand"));
                Copy("SKU: " + (Value("skuMode") == "manual" ? Value("sku") : T("Chờ OMS cấp", "Awaiting OMS allocation")));
                Copy("Barcode: " + (Value("barcodeMode") == "package" ? Value("barcode") : T("Chờ OMS cấp mã nội bộ", "Awaiting OMS internal code")));
                Copy(T("Theo dõi HSD theo lô: ", "Track lot expiry: ") + (Value("expiryPolicy") == "yes" ? T("Có", "Yes") : T("Không / chưa áp dụng", "No / not configured")));
                if (Value("pack") != "single") Copy(T("Quy cách: ", "Packaging: ") + Value("contained") + T(" chiếc trong lốc/thùng", " units per pack/case"));
                Copy(T("Chưa tạo SKU trên OMS, chưa nhập tồn kho. API mobile chưa hỗ trợ lưu sản phẩm hoặc in; bản nháp chỉ giữ trong phiên app.", "No OMS SKU or stock is created. Mobile product saving and printing are not connected; drafts remain in this app session."), 100);
                Action(T("Sửa thông tin", "Edit information"), () => { step = 1; Build(); });
                Action(T("Giữ bản nháp trong phiên", "Keep session draft"), () => { drafts[selected.id] = new Dictionary<string, string>(values); mode = "list"; message = T("Đã giữ nháp trong phiên. Chưa lưu OMS; đóng app sẽ mất nháp.", "Draft kept for this session. Not saved to OMS; closing the app loses it."); Build(); }, true);
                return;
            }
            Heading(Display(selected.name));
            var similar = inventory.Where(product => SearchKey(product.name).Contains(SearchKey(selected.name)) || SearchKey(product.name).Contains(SearchKey(Display(selected.name)))).ToArray();
            if (similar.Length > 0) Action(T("Có hàng tương tự · kiểm tra trước", "Similar items exist · check first"), () => ShowSimilarProducts(similar));
            Input(T("Tên sản phẩm *", "Product name *"), "name", Value("name"));
            Choice(T("Đơn vị lấy hàng *", "Picking unit *"), "unit", selected.units);
            foreach (var attribute in selected.attributes.Where(item => item.required || additional || selected.manual || !string.IsNullOrWhiteSpace(Value(item.id))))
            {
                string title = Display(attribute.label) + (attribute.required ? " *" : "");
                if (attribute.kind == "enum") Choice(title, attribute.id, attribute.choices);
                else if (attribute.units != null && attribute.units.Length > 0)
                {
                    if (Value(attribute.id + "Unit") == "") values[attribute.id + "Unit"] = attribute.unit;
                    float rowTop = cursor;
                    Input(title, attribute.id, Value(attribute.id), width: 246);
                    Choice("", attribute.id + "Unit", attribute.units, x: 252, y: rowTop + 28, width: 114);
                }
                else Input(title, attribute.id, Value(attribute.id));
            }
            Action(additional ? T("Thu gọn thông tin bổ sung", "Collapse additional information") : T("Thông tin bổ sung", "Additional information"), () => { additional = !additional; RebuildKeepingOffset(); });
            if (additional || Value("brand") != "") Input(T("Thương hiệu (tùy chọn)", "Brand (optional)"), "brand", Value("brand"));
            Choice(T("Theo dõi HSD theo lô", "Track expiry per lot"), "expiryPolicy", new[] { "yes", "no" }, new[] { T("Có — nhập khi nhận lô", "Yes — entered at receiving"), T("Không / chưa áp dụng", "No / not configured") });
            bool showCodes = codesExpanded;
            string packLabel = Value("pack") == "single" ? T("Đơn chiếc", "Single unit") : Value("pack") == "case" ? T("Thùng", "Case") : T("Lốc / hộp", "Pack");
            Action(showCodes ? T("Mã & quy cách · thu gọn", "Codes & packaging · collapse") : packLabel + T(" · mã & quy cách", " · codes & packaging"), () => { codesExpanded = !showCodes; RebuildKeepingOffset(); });
            if (showCodes)
            {
                Choice(T("Quy cách SKU", "SKU packaging"), "pack", new[] { "single", "pack", "case" }, new[] { T("Đơn chiếc", "Single unit"), T("Lốc / hộp nhiều chiếc", "Multi-unit pack"), T("Thùng", "Case") });
                if (Value("pack") != "single") Input(T("Số chiếc trong quy cách *", "Units in this packaging *"), "contained", Value("contained"));
                Choice("SKU", "skuMode", new[] { "auto", "manual" }, new[] { T("Cấp khi lưu OMS", "Allocated by OMS"), T("Mã kho đang có", "Existing warehouse code") });
                if (Value("skuMode") == "manual") Input(T("SKU kho *", "Warehouse SKU *"), "sku", Value("sku"));
                Choice("Barcode", "barcodeMode", new[] { "internal", "package" }, new[] { T("Mã nội bộ khi lưu OMS", "Internal code from OMS"), T("Mã trên bao bì", "Package barcode") });
                if (Value("barcodeMode") == "package") Input(T("Barcode bao bì *", "Package barcode *"), "barcode", Value("barcode"));
                Copy(T("Dùng mã trên bao bì nếu có. Không tự tạo GTIN.", "Use the package code if available. Never invent a GTIN."), 48);
            }
            Copy(T("Bản nháp trong phiên, chưa lưu OMS. HSD/NSX và số lô điền khi nhận hàng.", "Session draft, not saved to OMS. Enter lot dates at receiving."), 48);
            Action(T("Kiểm tra bản nháp", "Check draft"), () => ReviewDraft(), true);
            Action(T("Quay lại chọn mẫu", "Back to templates"), () => { step = 0; message = ""; Build(); });
        }
        public bool ReviewDraft()
        {
            message = ValidateDraft();
            validationMessage = message != "";
            if (validationMessage) additional = true;
            if (validationMessage && (Value("skuMode") == "manual" || Value("barcodeMode") == "package" || Value("pack") != "single")) codesExpanded = true;
            if (message == "") step = 2;
            Build(); return message == "";
        }
        void ShowSimilarProducts(OmsMobileApi.InventoryRow[] products)
        {
            drafts[selected.id] = new Dictionary<string, string>(values);
            float offset = page.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content.anchoredPosition.y;
            Action returnToForm = () => { mode = "create"; tab = "products"; message = ""; validationMessage = false; BuildAtOffset(offset); };
            choiceBack = returnToForm;
            page.SetActive(false); Destroy(page);
            page = PrototypeUi.Shell(transform, font, T("Sản phẩm tương tự", "Similar products"), Dark, returnToForm, returnToForm, out content).gameObject;
            cursor = 0;
            Copy(T("Kiểm tra tên, đơn vị và mã. Cùng tên chưa chắc cùng SKU.", "Check name, unit and code. Similar names may be different SKUs."), 56);
            foreach (var product in products.Take(60)) ProductRow(product, returnToForm);
            if (products.Length > 60) Copy(T("Có hơn 60 kết quả. Dùng tìm sản phẩm để kiểm tra thêm.", "More than 60 matches. Use product search to check further."), 56);
            Action(T("Tiếp tục bản nháp", "Continue draft"), returnToForm, true);
            Finish();
        }
        string ValidateDraft()
        {
            if (selected == null || string.IsNullOrWhiteSpace(Value("name"))) return T("Nhập tên sản phẩm trước.", "Enter the product name first.");
            foreach (var attribute in selected.attributes)
            {
                string value = Value(attribute.id);
                if (attribute.required && string.IsNullOrWhiteSpace(value)) return T("Chọn hoặc nhập: ", "Choose or enter: ") + Display(attribute.label);
                if (value != "" && (attribute.kind == "measure" || attribute.kind == "number" || attribute.kind == "integer"))
                {
                    if (!double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || double.IsNaN(number) || double.IsInfinity(number) || number <= 0 || (attribute.kind == "integer" && number != Math.Truncate(number)))
                        return Display(attribute.label) + T(": dùng số dương hợp lệ.", ": use a valid positive number.");
                }
                if (value != "" && attribute.kind == "enum" && !attribute.choices.Contains(value)) return T("Chọn lại: ", "Select again: ") + Display(attribute.label);
            }
            if (Value("skuMode") == "manual" && (Value("sku").Trim() == "" || inventory.Any(product => string.Equals(product.sku, Value("sku").Trim(), StringComparison.OrdinalIgnoreCase)))) return T("SKU thiếu hoặc đã có. Dùng sản phẩm cũ nếu trùng.", "SKU is missing or exists. Use the existing product.");
            if (Value("barcodeMode") == "package" && (Value("barcode").Trim() == "" || inventory.Any(product => product.barcode == Value("barcode").Trim()))) return T("Barcode thiếu hoặc đã có. Không ghi đè mã gốc.", "Barcode is missing or exists. Do not overwrite it.");
            if (Value("skuMode") == "manual" && !Regex.IsMatch(Value("sku").Trim(), "^[A-Za-z0-9._-]{1,64}$")) return T("SKU dùng tối đa 64 ký tự chữ không dấu, số, . _ -.", "SKU must use 1–64 letters, digits, . _ -.");
            if (Value("barcodeMode") == "package")
            {
                string barcode = Value("barcode").Trim();
                if (barcode.Length > 64 || barcode.Any(character => character < 33 || character > 126)) return T("Barcode cần 1–64 ký tự ASCII, không có dấu cách.", "Barcode must use 1–64 ASCII characters without spaces.");
                if (new[] { 8, 12, 13, 14 }.Contains(barcode.Length) && barcode.All(character => character >= '0' && character <= '9'))
                {
                    int sum = 0;
                    for (int index = barcode.Length - 2, weight = 3; index >= 0; index--, weight = weight == 3 ? 1 : 3) sum += (barcode[index] - '0') * weight;
                    if ((10 - sum % 10) % 10 != barcode[barcode.Length - 1] - '0') return T("Sai check digit. Kiểm tra barcode trên bao bì.", "Wrong check digit. Check the package barcode.");
                }
            }
            if (Value("pack") != "single" && (!int.TryParse(Value("contained"), out var count) || count < 2)) return T("Số chiếc trong quy cách phải là số nguyên từ 2 trở lên.", "Packaging must contain an integer number of at least 2 units.");
            return "";
        }
        void LabelScreen()
        {
            if (labelProduct == null) { mode = "list"; return; }
            Heading(labelProduct.name); Copy("SKU: " + labelProduct.sku); Copy("Barcode: " + (string.IsNullOrWhiteSpace(labelProduct.barcode) ? T("Thiếu mã gốc — cần quản lý xác minh", "Missing original code — ask a manager") : labelProduct.barcode), 64);
            Copy(T("Tem SKU không tự chứa HSD, số lô, tồn kho hay vị trí. Không tạo mã mới khi tem rách/mất.", "SKU labels do not automatically contain lot dates, stock or locations. Never replace the code just because paper is damaged."), 80);
            Choice(T("Số tem giấy (không phải số hàng)", "Paper labels (not stock quantity)"), "copies", new[] { "1", "2", "3", "5", "10" });
            Choice(T("Khổ tem dự kiến", "Planned label size"), "paper", new[] { "50 × 30 mm", "60 × 40 mm" });
            Copy(T("Chưa kết nối máy in/encoder. Lịch sử in thật cũng chưa có API; không ghi lượt in giả. Chỉ xem dữ liệu mã gốc ở đây.", "Printer and encoder are not connected. Print history has no mobile API; no fake print events are recorded. This view only shows original codes."), 88);
            var print = Action(T("In thêm · chưa kết nối", "Print more · unavailable"), () => { }); print.interactable = false;
            var replace = Action(T("Thay tem rách · chưa kết nối", "Replace label · unavailable"), () => { }); replace.interactable = false;
            if (labelBack != null) Action(T("Quay lại bản nháp", "Back to draft"), Back);
            else Action(T("Quay lại tìm sản phẩm", "Back to product search"), () => { mode = "list"; Build(); });
        }
        public void Back()
        {
            if (!TryBack()) nav.Back();
        }
        public bool TryBack()
        {
            if (choiceBack != null) { choiceBack(); return true; }
            if (mode == "label" && labelBack != null) { var returnToDraft = labelBack; labelBack = null; returnToDraft(); return true; }
            if (mode == "create" && step > 0) { step--; message = ""; validationMessage = false; Build(); return true; }
            if (mode != "list") { mode = "list"; Build(); return true; }
            return false;
        }
        void Heading(string text)
        {
            var label = PrototypeUi.Text(content, font, text, 0, cursor, 366, 42, 18, Dark, true);
            float height = Mathf.Clamp(label.GetPreferredValues(text, 366, 0).y + 8, 42, 120);
            label.rectTransform.sizeDelta = new Vector2(366, height); cursor += height + 8;
        }
        void Copy(string text, float height = 32) { PrototypeUi.Text(content, font, text, 0, cursor, 366, height, 13, Dark, false, true); cursor += height + 8; }
        UnityEngine.UI.Button Action(string text, Action clicked, bool primary = false, string id = null)
        {
            var button = ActionAt(text, 0, cursor, 366, clicked, primary); if (id != null) button.name = id; cursor += 60; return button;
        }
        UnityEngine.UI.Button ActionAt(string text, float x, float y, float width, Action clicked, bool primary)
        {
            var rect = PrototypeUi.Panel(content, text, x, y, width, 48, Dark, 12, primary ? "1255d5" : null, primary ? "00000000" : null);
            var button = PrototypeUi.Button(rect, clicked);
            var label = PrototypeUi.Text(rect, font, text, 12, 0, width - 24, 48, 12, Dark, true); label.alignment = TextAlignmentOptions.Center;
            if (primary) label.color = Color.white;
            return button;
        }
        void Input(string title, string key, string value, Action<string> changed = null, Action submitted = null, float width = 366)
        {
            PrototypeUi.Text(content, font, title, 0, cursor, 366, 26, 13, Dark, true); cursor += 28;
            var rect = PrototypeUi.Panel(content, "Product input " + key, 0, cursor, width, 48, Dark, 12);
            float textWidth = width - (submitted == null ? 24 : 66);
            var viewport = JarVisUi.Rect(rect, "Text viewport", 12, 0, textWidth, 48); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = PrototypeUi.Text(viewport, font, "", 0, 0, textWidth, 48, 14, Dark); text.alignment = TextAlignmentOptions.MidlineLeft;
            var input = rect.gameObject.AddComponent<TMP_InputField>(); input.textComponent = text; input.textViewport = viewport; input.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>(); input.targetGraphic.raycastTarget = true; input.characterLimit = 160; input.text = value;
            input.onValueChanged.AddListener(next => { if (changed != null) changed(next); else values[key] = next; });
            if (submitted != null)
            {
                var search = PrototypeUi.Action(rect, font, "", width - 48, 0, 48, 48, Dark, submitted);
                search.name = "Submit " + key;
                PrototypeUi.Icon(search.transform, "search", 14, 14, 20, PrototypeUi.Ink(Dark));
                input.onSubmit.AddListener(submittedText => submitted());
            }
            rect.gameObject.AddComponent<ImportedInputCaretRepair>().Configure(input); cursor += 56;
        }
        void Choice(string title, string key, string[] options, string[] labels = null, float x = 0, float? y = null, float width = 366)
        {
            if (options == null || options.Length == 0) return;
            string value = Value(key); int index = Array.IndexOf(options, value);
            string caption = (title == "" ? "" : title + ": ") + (index < 0 ? T("Chưa chọn", "Not selected") : labels == null ? Display(value) : labels[index]);
            var button = ActionAt(caption, x, y ?? cursor, width, () =>
            {
                var previousScroll = page.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                float returnOffset = previousScroll.content.anchoredPosition.y;
                Action returnToForm = () => BuildAtOffset(returnOffset);
                choiceBack = returnToForm;
                page.SetActive(false); Destroy(page);
                page = PrototypeUi.Shell(transform, font, title == "" ? T("Đơn vị đo", "Measurement unit") : title, Dark, returnToForm, returnToForm, out content).gameObject; cursor = 0;
                for (int optionIndex = 0; optionIndex < options.Length; optionIndex++)
                {
                    string selectedValue = options[optionIndex]; string optionLabel = labels == null ? Display(selectedValue) : labels[optionIndex];
                    Action(optionLabel, () => { ChangeChoice(key, selectedValue); returnToForm(); });
                }
                Action(T("Giữ lựa chọn / quay lại", "Keep selection / back"), returnToForm); Finish();
            }, false);
            button.name = "Product choice " + key;
            if (!y.HasValue) cursor += 60;
        }
        void BuildAtOffset(float offset)
        {
            Build(); Canvas.ForceUpdateCanvases();
            var scroll = page.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            float maximum = Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height);
            scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, Mathf.Clamp(offset, 0, maximum)); scroll.StopMovement();
        }
        void RebuildKeepingOffset()
        {
            var scroll = page == null ? null : page.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            BuildAtOffset(scroll == null ? 0 : scroll.content.anchoredPosition.y);
        }
        void ChangeChoice(string key, string next)
        {
            if (key.EndsWith("Unit", StringComparison.Ordinal))
            {
                string measureKey = key.Substring(0, key.Length - 4), previous = Value(key);
                if (previous != next && Value(measureKey) != "")
                {
                    string[] volume = { "ml", "l" }, mass = { "g", "kg" }, length = { "mm", "cm" };
                    bool same = volume.Contains(previous) && volume.Contains(next) || mass.Contains(previous) && mass.Contains(next) || length.Contains(previous) && length.Contains(next);
                    if (same && double.TryParse(Value(measureKey).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    {
                        double oldFactor = previous == "l" || previous == "kg" ? 1000 : previous == "cm" ? 10 : 1;
                        double newFactor = next == "l" || next == "kg" ? 1000 : next == "cm" ? 10 : 1;
                        values[measureKey] = (number * oldFactor / newFactor).ToString("G12", CultureInfo.InvariantCulture);
                    }
                    else { values[measureKey] = ""; message = T("Không đổi khối lượng thành dung tích. Nhập lại số đo trên nhãn.", "Mass cannot be converted to volume. Re-enter the content shown on the label."); }
                }
            }
            values[key] = next;
        }
        void Finish() { content.sizeDelta = new Vector2(366, Mathf.Max(678, cursor + 24)); }
    }
}
