using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JarVis.Warehouse.App;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Data;
using JarVis.Warehouse.Exceptions;
using JarVis.Warehouse.Labels;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.Providers;
using JarVis.Warehouse.Scanner;
using JarVis.Warehouse.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    public sealed class WarehouseWorkspaceController : MonoBehaviour
    {
        private enum WorkspacePanel
        {
            Tasks,
            Inventory,
            Scanner,
            Label,
            Chat,
            Exceptions,
            Map,
            TeamChat
        }

        [SerializeField] private RectTransform safeAreaRoot;

        private readonly Dictionary<WorkspacePanel, GameObject> panels =
            new Dictionary<WorkspacePanel, GameObject>();
        private readonly Dictionary<string, InputField> labelInputs =
            new Dictionary<string, InputField>();

        private MockWarehouseRepository repository;
        private BarcodeScannerService scannerService;
        private WarehouseLabelService labelService;
        private IJarvisBackend jarvisBackend;
        private IMicrophoneInputService microphoneService;
        private ITextToSpeechService speechService;
        private WarehouseExceptionService exceptionService;
        private WarehouseMapService mapService;
        private RectTransform workspaceRoot;
        private WarehouseInventoryScreen inventoryScreen;
        private RawImage cameraPreview;
        private Text scannerStatus;
        private Text scannerResult;
        private InputField scannerInput;
        private Text labelPreview;
        private Text labelStatus;
        private Text chatHistory;
        private Text chatStatus;
        private InputField chatInput;
        private Button chatSendButton;
        private ScrollRect chatScroll;
        private Text teamChatHistory;
        private Text teamChatStatus;
        private InputField teamChatInput;
        private Button teamChatSendButton;
        private ScrollRect teamChatScroll;
        private Coroutine teamChatPollRoutine;
        private OmsMobileApi.Conversation activeTeamConversation;
        private bool teamChatPending;
        private bool teamChatRefreshing;
        private const float TeamChatPollSeconds = 25f;
        private Dropdown exceptionTypeDropdown;
        private InputField exceptionTaskInput;
        private InputField exceptionSkuInput;
        private InputField exceptionBarcodeInput;
        private InputField exceptionLocationInput;
        private InputField exceptionDescriptionInput;
        private Text exceptionResult;
        private Dropdown mapStartDropdown;
        private Dropdown mapDestinationDropdown;
        private WarehouseRouteGraphic routeGraphic;
        private Text routeArrow;
        private Text routeSteps;
        private bool chatPending;

        private void Awake()
        {
            repository = new MockWarehouseRepository();
            scannerService = new BarcodeScannerService(repository);
            labelService = new WarehouseLabelService();
            JarvisApiClient apiClient = gameObject.GetComponent<JarvisApiClient>();
            if (apiClient == null)
                apiClient = gameObject.AddComponent<JarvisApiClient>();
            jarvisBackend = apiClient.TryConfigureFromLocalSettings()
                ? apiClient
                : new MockJarvisBackend();
            microphoneService = new PlaceholderMicrophoneInputService();
            speechService = new PlaceholderTextToSpeechService();
            exceptionService = new WarehouseExceptionService();
            mapService = new WarehouseMapService();
            JarvisLocalization.LanguageChanged += RebuildUi;
            BuildUi();
        }

        private void OnDestroy()
        {
            JarvisLocalization.LanguageChanged -= RebuildUi;
            StopTeamChatPolling();
            scannerService?.StopCamera();
            speechService?.Stop();
            microphoneService?.StopCapture();
        }

        private void BuildUi()
        {
            StopTeamChatPolling();
            scannerService?.StopCamera();
            panels.Clear();
            labelInputs.Clear();
            if (workspaceRoot != null)
                Destroy(workspaceRoot.gameObject);

            if (safeAreaRoot == null)
                safeAreaRoot = transform.Find("Safe Area") as RectTransform;
            if (safeAreaRoot == null)
            {
                Debug.LogError("[WarehouseWorkspaceController] Safe Area root is missing.", this);
                return;
            }

            AdjustDashboardLayout();
            workspaceRoot = CreateRect("Operations Workspace", safeAreaRoot, Vector2.zero, Vector2.one);
            CreateNavigationBar();
            BuildInventoryPanel();
            panels[WorkspacePanel.Scanner] = CreatePanel(
                "Scanner Panel",
                JarvisLocalization.Get("scan_barcode")
            );
            panels[WorkspacePanel.Label] = CreatePanel(
                "Label Panel",
                JarvisLocalization.Get("create_barcode")
            );
            panels[WorkspacePanel.Chat] = CreatePanel("Chat Panel", JarvisLocalization.Get("chat"));
            panels[WorkspacePanel.Exceptions] = CreatePanel(
                "Exceptions Panel",
                JarvisLocalization.Get("exception_report")
            );
            panels[WorkspacePanel.Map] = CreatePanel("Map Panel", JarvisLocalization.Get("map"));
            panels[WorkspacePanel.TeamChat] = CreatePanel(
                "Team Chat Panel", Local("Chat với quản lý", "Manager chat"));

            BuildScannerPanel(panels[WorkspacePanel.Scanner].transform);
            BuildLabelPanel(panels[WorkspacePanel.Label].transform);
            BuildChatPanel(panels[WorkspacePanel.Chat].transform);
            BuildExceptionsPanel(panels[WorkspacePanel.Exceptions].transform);
            BuildMapPanel(panels[WorkspacePanel.Map].transform);
            BuildTeamChatPanel(panels[WorkspacePanel.TeamChat].transform);
            MobileTextStyle.Apply(safeAreaRoot);
            ShowPanel(WorkspacePanel.Tasks);
        }

        private void AdjustDashboardLayout()
        {
            SetAnchors(safeAreaRoot.Find("Task Card") as RectTransform, new Vector2(0.03f, 0.31f), new Vector2(0.97f, 0.79f));
            SetAnchors(safeAreaRoot.Find("Controls Panel") as RectTransform, new Vector2(0.03f, 0.16f), new Vector2(0.97f, 0.29f));
            SetAnchors(safeAreaRoot.Find("Activity Label") as RectTransform, new Vector2(0.05f, 0.135f), new Vector2(0.95f, 0.16f));
            Transform header = safeAreaRoot.Find("Header Panel");
            if (header != null && header.Find("Inventory Entry Button") == null)
            {
                Button entry = CreateButton(header, Local("Lô hàng", "Lots"),
                    new Vector2(0.74f, 0.53f), new Vector2(0.97f, 0.91f),
                    () => ShowPanel(WorkspacePanel.Inventory));
                entry.gameObject.name = "Inventory Entry Button";
                entry.GetComponent<Image>().color = ColorFromHex("1762C4");
            }
        }

        private void BuildInventoryPanel()
        {
            GameObject prefab = Resources.Load<GameObject>("JarVisInventoryPanel");
            RectTransform root;
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, workspaceRoot);
                instance.name = "Inventory Panel";
                root = instance.GetComponent<RectTransform>();
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                inventoryScreen = instance.GetComponent<WarehouseInventoryScreen>();
            }
            else
            {
                root = CreateRect("Inventory Panel", workspaceRoot, Vector2.zero, Vector2.one);
                inventoryScreen = root.gameObject.AddComponent<WarehouseInventoryScreen>();
            }
            inventoryScreen.Build(
                () => ShowPanel(WorkspacePanel.Tasks),
                () => ShowPanel(WorkspacePanel.Tasks),
                () => ShowPanel(WorkspacePanel.Scanner),
                () => ShowPanel(WorkspacePanel.Map),
                () => ShowPanel(WorkspacePanel.Chat),
                query =>
                {
                    ShowPanel(WorkspacePanel.Scanner);
                    if (scannerInput != null) scannerInput.text = query;
                    StartCoroutine(LookupOmsBarcode(query));
                },
                () => ShowPanel(WorkspacePanel.TeamChat));
            // The workspace owns navigation on every page. Keep the prefab's own
            // navigation for its standalone preview, but do not stack two bars at runtime.
            RectTransform inventoryNavigation = root.Find("Bottom Navigation") as RectTransform;
            if (inventoryNavigation != null)
                inventoryNavigation.gameObject.SetActive(false);
            RectTransform lotScroll = root.Find("Lot Scroll") as RectTransform;
            if (lotScroll != null)
                SetAnchors(lotScroll, new Vector2(0.04f, 0.02f),
                    new Vector2(0.96f, 0.625f));
            SetAnchors(root, new Vector2(0f, 0.135f), Vector2.one);
            panels[WorkspacePanel.Inventory] = root.gameObject;
            root.gameObject.SetActive(false);
        }

        private void CreateNavigationBar()
        {
            Image background = CreateImage(
                "Operations Navigation",
                workspaceRoot,
                new Color(0.02f, 0.06f, 0.10f, 0.98f),
                Vector2.zero,
                new Vector2(1f, 0.135f)
            );
            AddNavigationButton(background.transform, 0, 0, JarvisLocalization.Get("tasks"),
                () => ShowPanel(WorkspacePanel.Tasks));
            AddNavigationButton(background.transform, 1, 0, Local("Lô hàng", "Lots"),
                () => ShowPanel(WorkspacePanel.Inventory));
            AddNavigationButton(background.transform, 2, 0, Local("Quét mã", "Scan"),
                () => ShowPanel(WorkspacePanel.Scanner));
            AddNavigationButton(background.transform, 3, 0, "JarVis",
                () => ShowPanel(WorkspacePanel.Chat));
            AddNavigationButton(background.transform, 4, 0, Local("Bản đồ", "Map"),
                () => ShowPanel(WorkspacePanel.Map));
            AddNavigationButton(background.transform, 0, 1, Local("Tạo tem", "Labels"),
                () => ShowPanel(WorkspacePanel.Label));
            AddNavigationButton(background.transform, 1, 1, Local("Báo cáo", "Reports"),
                () => ShowPanel(WorkspacePanel.Exceptions));
            AddNavigationButton(background.transform, 2, 1, Local("Đội ngũ", "Team"),
                () => ShowPanel(WorkspacePanel.TeamChat));
            AddNavigationButton(background.transform, 3, 1,
                JarvisLocalization.CurrentLanguage == SupportedLanguage.Vi ? "VI / EN" : "EN / VI",
                ToggleLanguage);
        }

        private void AddNavigationButton(Transform parent, int column, int row,
            string label, Action clicked)
        {
            float step = row == 0 ? 0.2f : 0.25f;
            float x0 = 0.006f + column * step;
            float x1 = (column + 1) * step - 0.006f;
            float y0 = row == 0 ? 0.525f : 0.035f;
            float y1 = row == 0 ? 0.965f : 0.475f;
            Button button = CreateButton(parent, label,
                new Vector2(x0, y0), new Vector2(x1, y1), clicked);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = 39;
            text.resizeTextMinSize = 34;
            text.resizeTextMaxSize = 39;
        }

        private GameObject CreatePanel(string name, string title)
        {
            Image panel = CreateImage(
                name,
                workspaceRoot,
                new Color(0.025f, 0.075f, 0.12f, 0.99f),
                new Vector2(0.02f, 0.135f),
                new Vector2(0.98f, 0.805f)
            );
            panel.raycastTarget = true;
            Text titleLabel = CreateText(
                "Panel Title",
                panel.transform,
                title,
                34,
                FontStyle.Bold,
                ColorFromHex("E9FBFF"),
                new Vector2(0.04f, 0.91f),
                new Vector2(0.78f, 0.99f),
                TextAnchor.MiddleLeft
            );
            titleLabel.resizeTextForBestFit = true;
            CreateButton(panel.transform, Local("ĐÓNG", "CLOSE"),
                new Vector2(0.80f, 0.915f), new Vector2(0.96f, 0.985f), () => ShowPanel(WorkspacePanel.Tasks));
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        private void ShowPanel(WorkspacePanel panel)
        {
            StopTeamChatPolling();
            foreach (KeyValuePair<WorkspacePanel, GameObject> entry in panels)
                entry.Value.SetActive(entry.Key == panel);

            bool showDashboard = panel == WorkspacePanel.Tasks;
            bool showInventory = panel == WorkspacePanel.Inventory;
            SetDashboardElementActive("Task Card", showDashboard);
            SetDashboardElementActive("Controls Panel", showDashboard);
            SetDashboardElementActive("Activity Label", showDashboard);
            SetDashboardElementActive("Header Panel", !showInventory);
            if (showInventory) inventoryScreen?.Refresh();

            if (panel == WorkspacePanel.Scanner)
                StartCoroutine(StartScannerCamera());
            else
                StopScannerCamera();
            if (panel == WorkspacePanel.TeamChat)
                teamChatPollRoutine = StartCoroutine(PollTeamChat());
        }

        private void StopTeamChatPolling()
        {
            if (teamChatPollRoutine == null)
                return;
            StopCoroutine(teamChatPollRoutine);
            teamChatPollRoutine = null;
            teamChatRefreshing = false;
        }

        private IEnumerator PollTeamChat()
        {
            while (panels.TryGetValue(WorkspacePanel.TeamChat, out GameObject panel) &&
                   panel != null && panel.activeInHierarchy)
            {
                if (OmsMobileSession.IsReady)
                    yield return RefreshTeamChat();
                else if (teamChatStatus != null)
                    teamChatStatus.text = Local("Cần đăng nhập OMS để xem chat quản lý.",
                        "Sign in to OMS to view manager chat.");
                yield return new WaitForSecondsRealtime(TeamChatPollSeconds);
            }
            teamChatPollRoutine = null;
        }

        private void SetDashboardElementActive(string objectName, bool active)
        {
            Transform element = safeAreaRoot != null ? safeAreaRoot.Find(objectName) : null;
            if (element != null)
                element.gameObject.SetActive(active);
        }

        private void ToggleLanguage()
        {
            JarvisLocalization.SetLanguage(
                JarvisLocalization.CurrentLanguage == SupportedLanguage.Vi
                    ? SupportedLanguage.En
                    : SupportedLanguage.Vi
            );
        }

        private void RebuildUi()
        {
            BuildUi();
        }

        private void BuildScannerPanel(Transform panel)
        {
            cameraPreview = CreateRawImage(
                "Camera Preview",
                panel,
                Color.black,
                new Vector2(0.05f, 0.53f),
                new Vector2(0.95f, 0.88f)
            );
            CreateFrame(cameraPreview.transform, ColorFromHex("55D6F4"));
            scannerStatus = CreateText(
                "Scanner Status",
                panel,
                Local(
                    "Camera chỉ hiển thị preview. Chưa cài decoder Code 128, EAN-13 hoặc QR.",
                    "Camera preview only. No Code 128, EAN-13, or QR decoder is installed."
                ),
                20,
                FontStyle.Bold,
                ColorFromHex("F9B65D"),
                new Vector2(0.05f, 0.45f),
                new Vector2(0.95f, 0.52f),
                TextAnchor.MiddleCenter
            );
            scannerInput = CreateInputField(
                "Manual Barcode Input",
                panel,
                Local("Nhập barcode thủ công", "Enter barcode manually"),
                new Vector2(0.05f, 0.36f),
                new Vector2(0.69f, 0.44f),
                false,
                false
            );
            CreateButton(
                panel,
                Local("TRA CỨU", "LOOK UP"),
                new Vector2(0.71f, 0.36f),
                new Vector2(0.95f, 0.44f),
                LookupManualBarcode
            );
            scannerResult = CreateText(
                "Scanner Result",
                panel,
                JarvisLocalization.Get("inventory_unverified"),
                22,
                FontStyle.Normal,
                ColorFromHex("D7E6EC"),
                new Vector2(0.05f, 0.04f),
                new Vector2(0.95f, 0.34f),
                TextAnchor.UpperLeft
            );
        }

        private IEnumerator StartScannerCamera()
        {
            if (scannerStatus != null)
                scannerStatus.text = JarvisLocalization.Get("loading");

            bool started = false;
            string state = null;
            yield return scannerService.StartCamera((success, result) =>
            {
                started = success;
                state = result;
            });

            if (cameraPreview != null)
                cameraPreview.texture = started ? scannerService.CameraTexture : null;
            if (scannerStatus != null)
            {
                scannerStatus.text = started
                    ? Local(
                        "CAMERA PREVIEW — chưa có decoder; dùng nhập mã thủ công.",
                        "CAMERA PREVIEW — no decoder; use manual entry."
                    )
                    : GetCameraError(state);
            }
        }

        private void StopScannerCamera()
        {
            scannerService?.StopCamera();
            if (cameraPreview != null)
                cameraPreview.texture = null;
        }

        private void LookupManualBarcode()
        {
            string barcode = scannerInput != null ? scannerInput.text : string.Empty;
            if (OmsMobileSession.IsReady)
            {
                StartCoroutine(LookupOmsBarcode(barcode));
                return;
            }
            if (!scannerService.TryManualLookup(
                    barcode,
                    out WarehouseItem item,
                    out WarehouseTask task,
                    out string errorKey))
            {
                if (scannerResult != null)
                    scannerResult.text = GetScannerError(errorKey);
                return;
            }

            if (scannerResult != null)
            {
                scannerResult.text =
                    $"BARCODE: {item.barcode}\nSKU: {item.sku}\n{item.productName}\n"
                    + $"LOT: {item.lotNumber}  |  EXP: {item.expiryDate}\n"
                    + $"RECEIVED: {item.receivedDate}  |  QTY: {item.quantity} {item.unit}\n"
                    + $"{JarvisLocalization.Get("location")}: {item.location}\n"
                    + $"STATUS: {item.status.ToUpperInvariant()}  |  MOCK: {item.isMock}\n"
                    + $"TASK: {task?.taskId ?? Local("Không có", "None")}\n"
                    + JarvisLocalization.Get("inventory_unverified");
            }
        }

        private IEnumerator LookupOmsBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                if (scannerResult != null)
                    scannerResult.text = Local("Nhập barcode, SKU hoặc mã lô.", "Enter barcode, SKU or lot code.");
                yield break;
            }
            OmsMobileApi.InventoryList result = null;
            string error = null;
            yield return OmsMobileApi.SearchInventory(barcode.Trim(),
                value => result = value, value => error = value);
            if (scannerResult == null)
                yield break;
            if (result == null)
            {
                scannerResult.text = Local("Tra cứu thất bại: ", "Lookup failed: ") + error;
                yield break;
            }
            OmsMobileApi.InventoryRow[] rows = result.items ?? Array.Empty<OmsMobileApi.InventoryRow>();
            if (rows.Length == 0)
            {
                scannerResult.text = Local("Không tìm thấy mã này trong kho.", "No matching warehouse record.");
                yield break;
            }
            int total = rows.Sum(row => row.available);
            scannerResult.text = $"{rows[0].name}\nSKU: {rows[0].sku}  |  BARCODE: {rows[0].barcode}\n"
                + string.Join("\n", rows.Take(4).Select(row =>
                    $"{row.lotCode} @ {row.locationCode}: {row.available} {row.unit}  EXP: {row.expiryDate}"))
                + $"\nTOTAL AVAILABLE: {total}\n"
                + Local("Dữ liệu mẫu backend; camera chưa giải mã mã vạch.",
                    "Sample backend data; camera does not decode barcodes.");
        }

        private void BuildLabelPanel(Transform panel)
        {
            ScrollRect formScroll = CreateScrollView(
                "Label Form",
                panel,
                new Vector2(0.04f, 0.36f),
                new Vector2(0.96f, 0.88f),
                out RectTransform content
            );
            formScroll.scrollSensitivity = 36f;
            WarehouseItem sample = repository.FindByBarcode("8931234567890") ?? new WarehouseItem();
            AddLabelField(content, "itemId", "Item ID", sample.itemId);
            AddLabelField(content, "productName", Local("Tên sản phẩm", "Product name"), sample.productName);
            AddLabelField(content, "sku", "SKU", sample.sku);
            AddLabelField(content, "barcode", "Barcode", sample.barcode);
            AddLabelField(content, "lotNumber", Local("Số lô", "Lot number"), sample.lotNumber);
            AddLabelField(content, "manufacturedDate", Local("Ngày sản xuất", "Manufactured date"), sample.manufacturedDate);
            AddLabelField(content, "expiryDate", Local("Hạn sử dụng", "Expiry date"), sample.expiryDate);
            AddLabelField(content, "receivedDate", Local("Ngày nhập kho", "Received date"), sample.receivedDate);
            AddLabelField(content, "quantity", Local("Số lượng", "Quantity"), sample.quantity.ToString(CultureInfo.InvariantCulture));
            AddLabelField(content, "unit", Local("Đơn vị", "Unit"), sample.unit);
            AddLabelField(content, "location", JarvisLocalization.Get("location"), sample.location);
            AddLabelField(content, "supplier", Local("Nhà cung cấp", "Supplier"), sample.supplier);
            AddLabelField(content, "status", Local("Trạng thái", "Status"), sample.status);
            AddLabelField(content, "taskId", "Task ID", sample.taskId);
            AddLabelField(content, "receivingCode", Local("Mã nhập hàng", "Receiving code"), sample.receivingCode);

            labelPreview = CreateText(
                "Label Preview",
                panel,
                labelService.BuildPreview(sample),
                18,
                FontStyle.Normal,
                ColorFromHex("E9FBFF"),
                new Vector2(0.04f, 0.04f),
                new Vector2(0.65f, 0.33f),
                TextAnchor.UpperLeft
            );
            CreateButton(
                panel,
                Local("TẠO TEM DEMO", "CREATE DEMO LABEL"),
                new Vector2(0.68f, 0.20f),
                new Vector2(0.96f, 0.31f),
                GenerateLabelPreview
            );
            labelStatus = CreateText(
                "Label Status",
                panel,
                Local(
                    "QR/Data Matrix chưa được mã hóa; preview không quét được.",
                    "QR/Data Matrix is not encoded; the preview is not scannable."
                ),
                18,
                FontStyle.Bold,
                ColorFromHex("F9B65D"),
                new Vector2(0.68f, 0.04f),
                new Vector2(0.96f, 0.18f),
                TextAnchor.UpperLeft
            );
        }

        private void AddLabelField(RectTransform content, string key, string label, string value)
        {
            GameObject row = new GameObject(
                $"{key} Row",
                typeof(RectTransform),
                typeof(LayoutElement)
            );
            row.transform.SetParent(content, false);
            row.GetComponent<LayoutElement>().preferredHeight = 76f;
            Text fieldLabel = CreateText(
                "Field Label",
                row.transform,
                label,
                20,
                FontStyle.Bold,
                ColorFromHex("B8C9D1"),
                new Vector2(0.02f, 0.08f),
                new Vector2(0.35f, 0.92f),
                TextAnchor.MiddleLeft
            );
            fieldLabel.resizeTextForBestFit = true;
            InputField input = CreateInputField(
                "Field Input",
                row.transform,
                label,
                new Vector2(0.37f, 0.08f),
                new Vector2(0.98f, 0.92f),
                false,
                false
            );
            input.text = value ?? string.Empty;
            labelInputs[key] = input;
        }

        private void GenerateLabelPreview()
        {
            WarehouseItem item = new WarehouseItem
            {
                itemId = LabelValue("itemId"),
                productName = LabelValue("productName"),
                sku = LabelValue("sku"),
                barcode = LabelValue("barcode"),
                lotNumber = LabelValue("lotNumber"),
                manufacturedDate = LabelValue("manufacturedDate"),
                expiryDate = LabelValue("expiryDate"),
                receivedDate = LabelValue("receivedDate"),
                quantity = ParseQuantity(LabelValue("quantity")),
                unit = LabelValue("unit"),
                location = LabelValue("location"),
                supplier = LabelValue("supplier"),
                status = LabelValue("status"),
                taskId = LabelValue("taskId"),
                receivingCode = LabelValue("receivingCode"),
                language = JarvisLocalization.CurrentLanguage == SupportedLanguage.Vi ? "vi" : "en",
                isMock = true
            };
            if (!labelService.Validate(item, out string errorKey))
            {
                if (labelStatus != null)
                    labelStatus.text = GetLabelError(errorKey);
                return;
            }

            if (labelPreview != null)
                labelPreview.text = labelService.BuildPreview(item);
            if (labelStatus != null)
            {
                labelStatus.text = Local(
                    "Đã tạo preview tem mock. Không cập nhật tồn kho. Mã 2D chưa quét được.",
                    "Mock label preview created. Inventory was not updated. The 2D code is not scannable."
                );
            }
        }

        private void BuildChatPanel(Transform panel)
        {
            Sprite figmaMascot = Resources.Load<Sprite>("Figma/JarVisMascotLogin");
            if (figmaMascot != null)
            {
                Image mascot = CreateImage("JarVis Figma Mascot", panel, Color.white,
                    new Vector2(0.04f, 0.91f), new Vector2(0.12f, 0.99f));
                mascot.sprite = figmaMascot;
                mascot.preserveAspect = true;
                RectTransform titleRect = panel.Find("Panel Title") as RectTransform;
                if (titleRect != null)
                    titleRect.anchorMin = new Vector2(0.14f, titleRect.anchorMin.y);
            }
            chatScroll = CreateTextScroll(
                "Chat History",
                panel,
                new Vector2(0.04f, 0.37f),
                new Vector2(0.96f, 0.88f),
                out chatHistory
            );
            chatHistory.text = OmsMobileSession.IsReady
                ? Local(
                    "JarVis tra cứu dữ liệu kho và SOP nội bộ. Số liệu hiện là dữ liệu mẫu; không gửi ra OpenAI.",
                    "JarVis uses local warehouse/SOP lookup. Current figures are samples and are not sent to OpenAI."
                )
                : jarvisBackend is MockJarvisBackend
                ? Local(
                    "JarVis DEMO: Tôi chỉ đưa ra hướng dẫn mock và không xác nhận tồn kho thật.",
                    "JarVis DEMO: I only provide mock guidance and do not verify live inventory."
                )
                : Local(
                    "JarVis AI: Đã kết nối backend OpenAI. Hướng dẫn AI không xác nhận tồn kho thật.",
                    "JarVis AI: Connected to the OpenAI backend. AI guidance does not verify live inventory."
                );
            chatInput = CreateInputField(
                "Chat Input",
                panel,
                Local("Nhập câu hỏi cho JarVis", "Ask JarVis a question"),
                new Vector2(0.04f, 0.26f),
                new Vector2(0.73f, 0.35f),
                false,
                false
            );
            chatSendButton = CreateButton(
                panel,
                Local("GỬI", "SEND"),
                new Vector2(0.75f, 0.26f),
                new Vector2(0.96f, 0.35f),
                SendChat
            );
            CreateButton(panel, Local("MIC (PLACEHOLDER)", "MIC (PLACEHOLDER)"),
                new Vector2(0.04f, 0.15f), new Vector2(0.34f, 0.23f), StartVoiceInput);
            CreateButton(panel, Local("ĐỌC", "READ"),
                new Vector2(0.36f, 0.15f), new Vector2(0.61f, 0.23f), SpeakLatestMessage);
            CreateButton(panel, Local("DỪNG ĐỌC", "STOP"),
                new Vector2(0.63f, 0.15f), new Vector2(0.96f, 0.23f), StopSpeaking);
            chatStatus = CreateText(
                "Chat Status",
                panel,
                JarvisLocalization.Get("inventory_unverified"),
                19,
                FontStyle.Bold,
                ColorFromHex("F9B65D"),
                new Vector2(0.04f, 0.04f),
                new Vector2(0.96f, 0.13f),
                TextAnchor.MiddleCenter
            );
        }

        private void SendChat()
        {
            if (chatPending || chatInput == null || string.IsNullOrWhiteSpace(chatInput.text))
                return;
            StartCoroutine(SendChatRequest(chatInput.text));
        }

        private IEnumerator SendChatRequest(string question)
        {
            chatPending = true;
            if (chatSendButton != null)
                chatSendButton.interactable = false;
            if (chatStatus != null)
                chatStatus.text = JarvisLocalization.Get("thinking");

            ChatResponse response = null;
            string failure = null;
            if (OmsMobileSession.IsReady)
            {
                OmsMobileApi.JarvisReply localReply = null;
                yield return OmsMobileApi.AskJarvis(question,
                    value => localReply = value, error => failure = error);
                if (localReply != null)
                {
                    string sourceText = localReply.sources != null && localReply.sources.Length > 0
                        ? "\n" + Local("Nguồn: ", "Sources: ")
                            + string.Join(", ", localReply.sources.Select(source => source.type + " " + source.id))
                        : string.Empty;
                    response = new ChatResponse
                    {
                        message = localReply.message + sourceText,
                        isMock = localReply.synthetic,
                        inventoryVerified = false
                    };
                }
            }
            else
            {
                ChatRequest request = new ChatRequest
                {
                    question = question,
                    language = JarvisLocalization.CurrentLanguage == SupportedLanguage.Vi ? "vi" : "en",
                    role = JarvisAppSession.Role.ToString().ToLowerInvariant(),
                    taskId = "TASK-DEMO-001",
                    sku = "ICE-CREAM-001",
                    barcode = "8931234567890",
                    location = "B-12-03",
                    history = Array.Empty<ChatMessage>()
                };
                yield return jarvisBackend.SendChat(request, value => response = value, error => failure = error);
            }

            if (response != null)
            {
                string assistantName = OmsMobileSession.IsReady
                    ? "JarVis · OMS"
                    : response.isMock ? "JarVis DEMO" : "JarVis AI";
                AppendChat($"\n\n{JarvisAppSession.EmployeeDisplayName}: {question}\n\n{assistantName}: {response.message}");
                if (chatInput != null)
                    chatInput.text = string.Empty;
                if (chatStatus != null)
                {
                    chatStatus.text = response.inventoryVerified
                        ? Local("Tồn kho đã xác minh", "Inventory verified")
                        : JarvisLocalization.Get("inventory_unverified");
                }
            }
            else if (chatStatus != null)
            {
                chatStatus.text = string.IsNullOrWhiteSpace(failure)
                    ? JarvisLocalization.Get("network_error")
                    : failure;
            }

            chatPending = false;
            if (chatSendButton != null)
                chatSendButton.interactable = true;
        }

        private void AppendChat(string message)
        {
            if (chatHistory == null)
                return;
            chatHistory.text += message;
            Canvas.ForceUpdateCanvases();
            if (chatScroll != null)
                chatScroll.verticalNormalizedPosition = 0f;
        }

        private void BuildTeamChatPanel(Transform panel)
        {
            teamChatScroll = CreateTextScroll(
                "Team Chat History", panel,
                new Vector2(0.04f, 0.34f), new Vector2(0.96f, 0.88f),
                out teamChatHistory);
            teamChatHistory.text = OmsMobileSession.IsReady
                ? Local("Mở panel để tải hội thoại với quản lý.", "Open panel to load manager conversation.")
                : Local("Chat đội ngũ cần đăng nhập OMS.", "Team chat requires OMS login.");
            teamChatInput = CreateInputField(
                "Team Chat Input", panel,
                Local("Nhắn cho quản lý", "Message the manager"),
                new Vector2(0.04f, 0.22f), new Vector2(0.73f, 0.32f), false, false);
            teamChatSendButton = CreateButton(
                panel, Local("GỬI", "SEND"),
                new Vector2(0.75f, 0.22f), new Vector2(0.96f, 0.32f), SendTeamChat);
            teamChatSendButton.interactable = false;
            teamChatStatus = CreateText(
                "Team Chat Status", panel,
                Local("Chỉ tài khoản nhân viên OMS mới có thể gửi.", "OMS employee sign-in is required."),
                18, FontStyle.Bold, ColorFromHex("F9B65D"),
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.17f),
                TextAnchor.MiddleCenter);
            if (teamChatScroll != null)
                teamChatScroll.verticalNormalizedPosition = 0f;
        }

        private IEnumerator RefreshTeamChat()
        {
            if (!OmsMobileSession.IsReady || teamChatRefreshing)
                yield break;
            teamChatRefreshing = true;
            try
            {
            OmsMobileApi.ConversationList conversations = null;
            string error = null;
            yield return OmsMobileApi.GetConversations(
                value => conversations = value, value => error = value);
            if (conversations == null)
            {
                if (teamChatStatus != null)
                    teamChatStatus.text = Local("Không tải được chat; sẽ thử lại. ",
                        "Unable to load chat; retrying. ") + error;
                yield break;
            }
            activeTeamConversation = conversations.conversations?.FirstOrDefault();
            if (teamChatSendButton != null)
                teamChatSendButton.interactable = activeTeamConversation != null && !teamChatPending;
            if (activeTeamConversation == null)
            {
                if (teamChatHistory != null)
                    teamChatHistory.text = Local("Chưa có cuộc trò chuyện. Quản lý có thể mở chat từ web.",
                        "No conversation yet. A manager can open one from the web.");
                yield break;
            }
            OmsMobileApi.MessageList messages = null;
            yield return OmsMobileApi.GetMessages(activeTeamConversation.id,
                value => messages = value, value => error = value);
            if (messages == null)
            {
                if (teamChatStatus != null)
                    teamChatStatus.text = Local("Không tải được tin nhắn; sẽ thử lại. ",
                        "Unable to load messages; retrying. ") + error;
                yield break;
            }
            if (teamChatHistory != null)
            {
                string history = string.Join("\n\n",
                    (messages.messages ?? Array.Empty<OmsMobileApi.StaffMessage>())
                    .TakeLast(30)
                    .Select(message => $"{message.sender} · {FormatChatTime(message.createdAt)}\n{message.content}"));
                if (string.IsNullOrWhiteSpace(history))
                    history = Local("Chưa có tin nhắn.", "No messages yet.");
                if (teamChatHistory.text != history)
                {
                    bool wasAtBottom = teamChatScroll == null || teamChatScroll.verticalNormalizedPosition < 0.08f;
                    float previousPosition = teamChatScroll != null ? teamChatScroll.verticalNormalizedPosition : 0f;
                    teamChatHistory.text = history;
                    Canvas.ForceUpdateCanvases();
                    if (teamChatScroll != null)
                        teamChatScroll.verticalNormalizedPosition = wasAtBottom ? 0f : previousPosition;
                }
            }
            if (teamChatStatus != null)
                teamChatStatus.text = Local("Đã đồng bộ lúc ", "Synced at ") +
                    DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
            }
            finally
            {
                teamChatRefreshing = false;
            }
        }

        private static string FormatChatTime(string createdAt)
        {
            return DateTimeOffset.TryParse(createdAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset date)
                ? date.ToLocalTime().ToString("HH:mm dd/MM", CultureInfo.CurrentCulture)
                : createdAt ?? string.Empty;
        }

        private void SendTeamChat()
        {
            if (teamChatPending || !OmsMobileSession.IsReady ||
                activeTeamConversation == null || teamChatInput == null ||
                string.IsNullOrWhiteSpace(teamChatInput.text))
                return;
            StartCoroutine(SendTeamChatRequest(teamChatInput.text.Trim()));
        }

        private IEnumerator SendTeamChatRequest(string content)
        {
            teamChatPending = true;
            if (teamChatSendButton != null)
                teamChatSendButton.interactable = false;
            OmsMobileApi.StaffMessage sent = null;
            string error = null;
            yield return OmsMobileApi.SendMessage(activeTeamConversation.id, content,
                value => sent = value, value => error = value);
            if (sent != null)
            {
                if (teamChatInput != null && teamChatInput.text.Trim() == content)
                    teamChatInput.text = string.Empty;
                if (panels.TryGetValue(WorkspacePanel.TeamChat, out GameObject panel) &&
                    panel != null && panel.activeInHierarchy)
                {
                    while (teamChatRefreshing)
                        yield return null;
                    yield return RefreshTeamChat();
                }
            }
            else if (teamChatStatus != null)
                teamChatStatus.text = Local("Gửi thất bại: ", "Send failed: ") + error;
            teamChatPending = false;
            if (teamChatSendButton != null)
                teamChatSendButton.interactable = OmsMobileSession.IsReady && activeTeamConversation != null;
        }

        private void StartVoiceInput()
        {
            microphoneService.BeginCapture(
                value =>
                {
                    if (chatInput != null)
                        chatInput.text = value;
                },
                _ => SetChatStatus(Local(
                    "Nhập giọng nói chưa được cấu hình.",
                    "Voice input is not configured."
                ))
            );
        }

        private void SpeakLatestMessage()
        {
            speechService.Speak(
                chatHistory != null ? chatHistory.text : string.Empty,
                _ => SetChatStatus(Local(
                    "Đọc câu trả lời chưa được cấu hình.",
                    "Text-to-speech is not configured."
                ))
            );
        }

        private void StopSpeaking()
        {
            speechService.Stop();
            SetChatStatus(Local("Đã dừng đọc.", "Speech stopped."));
        }

        private void SetChatStatus(string value)
        {
            if (chatStatus != null)
                chatStatus.text = value;
        }

        private void BuildExceptionsPanel(Transform panel)
        {
            exceptionTypeDropdown = CreateDropdown(
                "Exception Type",
                panel,
                GetExceptionOptions(),
                new Vector2(0.04f, 0.79f),
                new Vector2(0.96f, 0.88f)
            );
            exceptionTaskInput = CreateInputField("Exception Task", panel, "Task ID",
                new Vector2(0.04f, 0.68f), new Vector2(0.47f, 0.76f), false, false);
            exceptionSkuInput = CreateInputField("Exception SKU", panel, "SKU",
                new Vector2(0.53f, 0.68f), new Vector2(0.96f, 0.76f), false, false);
            exceptionBarcodeInput = CreateInputField("Exception Barcode", panel, "Barcode",
                new Vector2(0.04f, 0.57f), new Vector2(0.47f, 0.65f), false, false);
            exceptionLocationInput = CreateInputField("Exception Location", panel, JarvisLocalization.Get("location"),
                new Vector2(0.53f, 0.57f), new Vector2(0.96f, 0.65f), false, false);
            exceptionDescriptionInput = CreateInputField(
                "Exception Description",
                panel,
                Local("Mô tả ngoại lệ", "Describe the exception"),
                new Vector2(0.04f, 0.35f),
                new Vector2(0.96f, 0.54f),
                false,
                true
            );
            Text photoPlaceholder = CreateText(
                "Photo Placeholder",
                panel,
                Local(
                    "ẢNH PLACEHOLDER — chưa cấu hình dịch vụ tải ảnh",
                    "PHOTO PLACEHOLDER — no upload service configured"
                ),
                19,
                FontStyle.Bold,
                ColorFromHex("8FA8B5"),
                new Vector2(0.04f, 0.27f),
                new Vector2(0.96f, 0.34f),
                TextAnchor.MiddleCenter
            );
            photoPlaceholder.resizeTextForBestFit = true;
            CreateButton(
                panel,
                Local("GỬI BÁO CÁO DEMO", "SUBMIT DEMO REPORT"),
                new Vector2(0.04f, 0.17f),
                new Vector2(0.96f, 0.25f),
                SubmitException
            );
            exceptionResult = CreateText(
                "Exception Result",
                panel,
                Local(
                    "Báo cáo sẽ được lưu cục bộ và chờ giám sát xem xét.",
                    "The report will be stored locally and await supervisor review."
                ),
                20,
                FontStyle.Normal,
                ColorFromHex("D7E6EC"),
                new Vector2(0.04f, 0.03f),
                new Vector2(0.96f, 0.15f),
                TextAnchor.UpperLeft
            );

            exceptionTaskInput.text = "TASK-DEMO-001";
            exceptionSkuInput.text = "ICE-CREAM-001";
            exceptionBarcodeInput.text = "8931234567890";
            exceptionLocationInput.text = "B-12-03";
        }

        private void SubmitException()
        {
            WarehouseExceptionType type = (WarehouseExceptionType)Mathf.Clamp(
                exceptionTypeDropdown != null ? exceptionTypeDropdown.value : 0,
                0,
                Enum.GetValues(typeof(WarehouseExceptionType)).Length - 1
            );
            WarehouseExceptionReport report = exceptionService.Create(
                type,
                exceptionTaskInput?.text,
                exceptionSkuInput?.text,
                exceptionBarcodeInput?.text,
                exceptionDescriptionInput?.text,
                exceptionLocationInput?.text,
                JarvisAppSession.EmployeeId
            );
            if (exceptionResult != null)
            {
                exceptionResult.text =
                    $"{report.exceptionId}\n{GetExceptionLabel(report.type)}  |  {report.status}\n"
                    + $"{report.location}  |  {report.createdAtUtc}\n"
                    + Local(
                        "MOCK — chờ supervisor review; chưa gửi WMS.",
                        "MOCK — awaiting supervisor review; not sent to WMS."
                    );
            }
        }

        private void BuildMapPanel(Transform panel)
        {
            IReadOnlyList<Waypoint> waypoints = mapService.GetWaypoints();
            List<string> options = waypoints.Select(value => value.displayName).ToList();
            mapStartDropdown = CreateDropdown(
                "Start Location",
                panel,
                options,
                new Vector2(0.04f, 0.80f),
                new Vector2(0.47f, 0.88f)
            );
            mapDestinationDropdown = CreateDropdown(
                "Destination",
                panel,
                options,
                new Vector2(0.53f, 0.80f),
                new Vector2(0.96f, 0.88f)
            );
            mapDestinationDropdown.value = Mathf.Min(3, options.Count - 1);
            CreateButton(
                panel,
                Local("TÍNH ROUTE WAYPOINT", "PLAN WAYPOINT ROUTE"),
                new Vector2(0.04f, 0.70f),
                new Vector2(0.96f, 0.78f),
                PlanRoute
            );

            Image mapBackground = CreateImage(
                "Map 2D",
                panel,
                ColorFromHex("102330"),
                new Vector2(0.04f, 0.34f),
                new Vector2(0.96f, 0.68f)
            );
            GameObject routeObject = new GameObject(
                "Route Graphic",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(WarehouseRouteGraphic)
            );
            routeObject.transform.SetParent(mapBackground.transform, false);
            RectTransform routeRect = routeObject.GetComponent<RectTransform>();
            routeRect.anchorMin = Vector2.zero;
            routeRect.anchorMax = Vector2.one;
            routeRect.offsetMin = Vector2.zero;
            routeRect.offsetMax = Vector2.zero;
            routeGraphic = routeObject.GetComponent<WarehouseRouteGraphic>();
            routeGraphic.color = ColorFromHex("55D6F4");

            routeArrow = CreateText(
                "Direction Arrow",
                panel,
                Local("Chọn điểm đầu và điểm đích.", "Select a start and destination."),
                30,
                FontStyle.Bold,
                ColorFromHex("73E2A7"),
                new Vector2(0.04f, 0.24f),
                new Vector2(0.96f, 0.32f),
                TextAnchor.MiddleCenter
            );
            routeSteps = CreateText(
                "Route Steps",
                panel,
                Local(
                    "ROUTE MOCK — chưa được WMS hoặc quản lý kho phê duyệt.",
                    "MOCK ROUTE — not approved by WMS or warehouse management."
                ),
                19,
                FontStyle.Normal,
                ColorFromHex("D7E6EC"),
                new Vector2(0.04f, 0.03f),
                new Vector2(0.96f, 0.23f),
                TextAnchor.UpperLeft
            );
        }

        private void PlanRoute()
        {
            IReadOnlyList<Waypoint> waypoints = mapService.GetWaypoints();
            if (waypoints.Count == 0 || mapStartDropdown == null || mapDestinationDropdown == null)
                return;

            Waypoint start = waypoints[Mathf.Clamp(mapStartDropdown.value, 0, waypoints.Count - 1)];
            Waypoint destination = waypoints[Mathf.Clamp(mapDestinationDropdown.value, 0, waypoints.Count - 1)];
            Route route = mapService.GetRoute(start.locationId, destination.locationId);
            if (routeGraphic != null)
                routeGraphic.SetRoute(route);
            if (!route.IsValid)
            {
                if (routeSteps != null)
                    routeSteps.text = Local("Không tìm thấy route waypoint.", "No waypoint route found.");
                if (routeArrow != null)
                    routeArrow.text = "—";
                return;
            }

            if (routeArrow != null)
            {
                Vector3 direction = route.waypoints[1].Position - route.waypoints[0].Position;
                routeArrow.text = $"{GetDirectionArrow(direction)}  {route.waypoints[1].displayName}";
            }

            if (routeSteps != null)
            {
                string steps = string.Join("  →  ", route.waypoints.Select(value => value.displayName));
                routeSteps.text =
                    $"{Local("Khoảng cách", "Distance")}: {route.totalDistance:0.0} m\n{steps}\n"
                    + Local(
                        "MOCK ROUTE — chưa được phê duyệt. GPS không được dùng làm định vị chính trong kho.",
                        "MOCK ROUTE — not approved. GPS is not the primary indoor positioning source."
                    );
            }
        }

        private List<string> GetExceptionOptions()
        {
            return Enum.GetValues(typeof(WarehouseExceptionType))
                .Cast<WarehouseExceptionType>()
                .Select(GetExceptionLabel)
                .ToList();
        }

        private string GetExceptionLabel(WarehouseExceptionType type)
        {
            return type switch
            {
                WarehouseExceptionType.ShortPick => JarvisLocalization.Get("short_pick"),
                WarehouseExceptionType.ItemOnFloor => Local("Hàng rơi xuống sàn", "Item on floor"),
                WarehouseExceptionType.Damaged => JarvisLocalization.Get("damaged"),
                WarehouseExceptionType.WrongLocation => JarvisLocalization.Get("misplaced"),
                WarehouseExceptionType.BarcodeUnreadable => Local("Không quét được barcode", "Barcode unreadable"),
                WarehouseExceptionType.SkuNotFound => Local("Không tìm thấy SKU", "SKU not found"),
                WarehouseExceptionType.NoReplacement => Local("Không có hàng thay thế", "No replacement available"),
                WarehouseExceptionType.WmsDisconnected => Local("WMS mất kết nối", "WMS disconnected"),
                WarehouseExceptionType.DeviceOffline => Local("PDA mất mạng", "Device offline"),
                _ => type.ToString()
            };
        }

        private string GetCameraError(string state)
        {
            return state switch
            {
                "camera_permission_denied" => Local(
                    "Quyền camera bị từ chối. Hãy cấp quyền trong cài đặt Android.",
                    "Camera permission was denied. Grant it in Android settings."
                ),
                "camera_unavailable" => JarvisLocalization.Get("camera_error"),
                _ => JarvisLocalization.Get("camera_error")
            };
        }

        private string GetScannerError(string errorKey)
        {
            return errorKey switch
            {
                "barcode_required" => Local("Vui lòng nhập barcode.", "Enter a barcode."),
                "duplicate_scan" => Local("Barcode vừa được xử lý; vui lòng chờ.", "Barcode was just handled; please wait."),
                _ => JarvisLocalization.Get("barcode_not_found")
            };
        }

        private string GetLabelError(string errorKey)
        {
            return errorKey == "label_required_fields"
                ? Local(
                    "Thiếu Item ID, tên sản phẩm, SKU, barcode hoặc số lượng hợp lệ.",
                    "Item ID, product name, SKU, barcode, or a valid quantity is missing."
                )
                : Local("Không thể tạo preview tem.", "Unable to create the label preview.");
        }

        private string LabelValue(string key)
        {
            return labelInputs.TryGetValue(key, out InputField input) ? input.text.Trim() : string.Empty;
        }

        private static int ParseQuantity(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity)
                ? quantity
                : -1;
        }

        private static string GetDirectionArrow(Vector3 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
                return direction.x >= 0f ? "→" : "←";
            return direction.z >= 0f ? "↑" : "↓";
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax
        )
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax
        )
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage CreateRawImage(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax
        )
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            RawImage image = gameObject.GetComponent<RawImage>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void CreateFrame(Transform parent, Color color)
        {
            const float thickness = 0.012f;
            CreateImage("Frame Top", parent, color, new Vector2(0f, 1f - thickness), Vector2.one);
            CreateImage("Frame Bottom", parent, color, Vector2.zero, new Vector2(1f, thickness));
            CreateImage("Frame Left", parent, color, Vector2.zero, new Vector2(thickness, 1f));
            CreateImage("Frame Right", parent, color, new Vector2(1f - thickness, 0f), Vector2.one);
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle fontStyle,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAnchor alignment
        )
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = gameObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(11, fontSize / 2);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Action clicked
        )
        {
            GameObject gameObject = new GameObject(
                $"{label} Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = gameObject.GetComponent<Image>();
            image.color = ColorFromHex("155E75");
            image.raycastTarget = true;
            Button button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.68f, 0.82f, 0.88f, 1f);
            colors.disabledColor = new Color(0.45f, 0.50f, 0.53f, 0.55f);
            button.colors = colors;
            CreateText(
                "Label",
                gameObject.transform,
                label,
                18,
                FontStyle.Bold,
                ColorFromHex("E9FBFF"),
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter
            );
            if (clicked != null)
                button.onClick.AddListener(() => clicked());
            return button;
        }

        private static InputField CreateInputField(
            string name,
            Transform parent,
            string placeholderValue,
            Vector2 anchorMin,
            Vector2 anchorMax,
            bool password,
            bool multiline
        )
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(InputField)
            );
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image background = gameObject.GetComponent<Image>();
            background.color = ColorFromHex("132633");
            background.raycastTarget = true;

            Text placeholder = CreateText(
                "Placeholder",
                gameObject.transform,
                placeholderValue,
                20,
                FontStyle.Italic,
                ColorFromHex("758D99"),
                new Vector2(0.03f, 0.08f),
                new Vector2(0.97f, 0.92f),
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft
            );
            Text value = CreateText(
                "Text",
                gameObject.transform,
                string.Empty,
                21,
                FontStyle.Normal,
                ColorFromHex("E9FBFF"),
                new Vector2(0.03f, 0.08f),
                new Vector2(0.97f, 0.92f),
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft
            );
            value.supportRichText = false;

            InputField input = gameObject.GetComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = value;
            input.placeholder = placeholder;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.characterLimit = multiline ? 600 : 128;
            return input;
        }

        private static Dropdown CreateDropdown(
            string name,
            Transform parent,
            List<string> options,
            Vector2 anchorMin,
            Vector2 anchorMax
        )
        {
            GameObject dropdownObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Dropdown)
            );
            dropdownObject.transform.SetParent(parent, false);
            RectTransform rect = dropdownObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image background = dropdownObject.GetComponent<Image>();
            background.color = ColorFromHex("132633");
            background.raycastTarget = true;
            Text caption = CreateText("Label", dropdownObject.transform, string.Empty, 20, FontStyle.Normal,
                ColorFromHex("E9FBFF"), new Vector2(0.04f, 0.05f), new Vector2(0.84f, 0.95f), TextAnchor.MiddleLeft);
            CreateText("Arrow", dropdownObject.transform, "▼", 18, FontStyle.Bold,
                ColorFromHex("55D6F4"), new Vector2(0.84f, 0.05f), new Vector2(0.98f, 0.95f), TextAnchor.MiddleCenter);

            GameObject templateObject = new GameObject(
                "Template",
                typeof(RectTransform),
                typeof(Image),
                typeof(ScrollRect)
            );
            templateObject.transform.SetParent(dropdownObject.transform, false);
            RectTransform templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -4f);
            templateRect.sizeDelta = new Vector2(0f, 320f);
            templateObject.GetComponent<Image>().color = ColorFromHex("132633");

            GameObject viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(Mask)
            );
            viewportObject.transform.SetParent(templateObject.transform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(3f, 3f);
            viewport.offsetMax = new Vector2(-3f, -3f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 68f);

            GameObject itemObject = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemObject.transform.SetParent(contentObject.transform, false);
            RectTransform itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 68f);
            Image itemBackground = CreateImage("Item Background", itemObject.transform, ColorFromHex("1A3444"), Vector2.zero, Vector2.one);
            itemBackground.raycastTarget = true;
            Image checkmark = CreateImage("Checkmark", itemObject.transform, ColorFromHex("55D6F4"),
                new Vector2(0.03f, 0.28f), new Vector2(0.08f, 0.72f));
            Text itemLabel = CreateText("Item Label", itemObject.transform, "Option", 20, FontStyle.Normal,
                ColorFromHex("E9FBFF"), new Vector2(0.11f, 0.05f), new Vector2(0.97f, 0.95f), TextAnchor.MiddleLeft);
            Toggle toggle = itemObject.GetComponent<Toggle>();
            toggle.targetGraphic = itemBackground;
            toggle.graphic = checkmark;
            toggle.isOn = true;

            ScrollRect scroll = templateObject.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.template = templateRect;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.AddOptions(options ?? new List<string>());
            templateObject.SetActive(false);
            return dropdown;
        }

        private static ScrollRect CreateScrollView(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            out RectTransform content
        )
        {
            Image root = CreateImage(name, parent, ColorFromHex("0D1E29"), anchorMin, anchorMax);
            root.raycastTarget = true;
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            GameObject viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(Mask)
            );
            viewportObject.transform.SetParent(root.transform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8f, 8f);
            viewport.offsetMax = new Vector2(-8f, -8f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            );
            contentObject.transform.SetParent(viewportObject.transform, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }

        private static ScrollRect CreateTextScroll(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            out Text contentText
        )
        {
            Image root = CreateImage(name, parent, ColorFromHex("0D1E29"), anchorMin, anchorMax);
            root.raycastTarget = true;
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            GameObject viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(Mask)
            );
            viewportObject.transform.SetParent(root.transform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12f, 12f);
            viewport.offsetMax = new Vector2(-12f, -12f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject textObject = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(Text),
                typeof(ContentSizeFitter)
            );
            textObject.transform.SetParent(viewportObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.sizeDelta = Vector2.zero;
            contentText = textObject.GetComponent<Text>();
            contentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            contentText.fontSize = 21;
            contentText.color = ColorFromHex("D7E6EC");
            contentText.alignment = TextAnchor.UpperLeft;
            contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contentText.verticalOverflow = VerticalWrapMode.Overflow;
            contentText.raycastTarget = false;
            ContentSizeFitter fitter = textObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = textRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            return scroll;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rect == null)
                return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static string Local(string vietnamese, string english)
        {
            return JarvisLocalization.CurrentLanguage == SupportedLanguage.Vi ? vietnamese : english;
        }

        private static Color ColorFromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString($"#{hex}", out Color color) ? color : Color.white;
        }
    }
}
