using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Demo2026
{
    // Insert beside the parent's partial class. The parent owns page lifetime, rendering,
    // navigation, modals and Emit; this file defines no Unity lifecycle callbacks.
    public sealed partial class JarVisSystemDemo : MonoBehaviour
    {
        // Read-only capability sources:
        // D:/unity of teep/JarVis-Warehouse-AR/Assets/JarVis/Scripts/UI/PrototypeWorkspace.cs
        // D:/unity of teep/JarVis-Warehouse-AR/Assets/JarVis/Scripts/UI/WarehouseProductWorkspace.cs
        // D:/unity of teep/JarVis-Warehouse-AR/Assets/JarVis/Scripts/UI/PrototypeOperationPresentation.cs
        // D:/unity of teep/JarVis-Warehouse-AR/Assets/JarVis/Scripts/UI/ImportedSettings.cs
        // Emit strings are bridge destinations, not new API or persistence commands:
        // inventory -> 5:780; product-label -> 5:450; inbound -> 5:935;
        // outbound -> 14:387; incident -> 5:1029; settings -> 5:1308;
        // profile/activity -> prototype:profile / prototype:activity;
        // online/signout -> the original presence/sign-out review owned by PrototypeDialog.
        // The bridge must not submit these sample values. Original owners collect real inputs.

        const float SecWidth = 372f;
        const float SecInset = 20f;
        const float SecGap = 14f;

        sealed class SecondaryBook
        {
            public readonly string Id, Title, EnglishTitle, Sku, Barcode, Destination;

            public SecondaryBook(string id, string title, string englishTitle, string sku,
                string barcode, string destination)
            {
                Id = id; Title = title; EnglishTitle = englishTitle; Sku = sku;
                Barcode = barcode; Destination = destination;
            }
        }

        // Mandatory, reviewed task identities. No invented authors, lots or stock quantities.
        // A1/A2 identify books; B/C are delivery destinations, not claimed inventory locations.
        static readonly SecondaryBook[] SecondaryBooks =
        {
            new SecondaryBook("A1", "Vở A1", "Notebook A1", "CPNB-1004", "4714551570610", "B"),
            new SecondaryBook("A2", "Vở A2", "Notebook A2", "CPNB-1003", "4714551570603", "C")
        };

        sealed class SecondaryCountDraft
        {
            public string Sku, Lot, Quantity;
            public SecondaryCountDraft(string sku, string lot, string quantity)
            { Sku = sku; Lot = lot; Quantity = quantity; }
        }

        readonly SecondaryCountDraft secInbound =
            new SecondaryCountDraft("CPNB-1004", "", "");
        readonly SecondaryCountDraft secOutbound =
            new SecondaryCountDraft("CPNB-1003", "", "");

        RectTransform secBody, secViewport;
        float secY;
        int secBookIndex;
        string secQuery = "";
        RectTransform[] secInventoryRows;
        RectTransform secInventoryEmpty, secInventoryTail;
        TMP_Text secInventoryStatus;
        float secInventoryRowsY;

        // Unknown destinations leave the parent completely untouched.
        // For an AR issue action call Secondary("incident") BEFORE clearing the current page:
        // this destination is an overlay, so the AR view and scene remain parent-owned.
        bool Secondary(string id)
        {
            switch (id)
            {
                case "inventory": SecInventory(); break;
                case "product": SecProduct(); break;
                case "labels": SecLabels(); break;
                case "transactions": SecTransactions(); break;
                case "inbound": SecCountForm(true); break;
                case "outbound": SecCountForm(false); break;
                case "incident": SecIncident(); break;
                case "profile": SecProfile(); break;
                case "activity": SecActivity(); break;
                case "settings": SecSettings(); break;
                case "tools": SecTools(); break;
                default: return false;
            }
            return true;
        }

        string SecT(string vi, string en) { return English ? en : vi; }
        Color SecInk { get { return Dark ? new Color(.91f, .95f, 1f) : Ink; } }
        Color SecMuted { get { return Dark ? new Color(.68f, .75f, .85f) : Muted; } }
        Color SecBlue { get { return Dark ? new Color(.49f, .70f, 1f) : Blue; } }
        Color SecGreen { get { return Dark ? new Color(.38f, .84f, .67f) : Green; } }
        Color SecSurface { get { return Dark ? new Color(.10f, .16f, .24f) : Color.white; } }

        void SecBegin(string title, string subtitle)
        {
            Header(title, subtitle);
            // Top 140; the viewport ends at H - 102, before the parent's H - 90 footer.
            secViewport = SecRect(page, "Secondary content viewport", 20, 140,
                SecWidth, Mathf.Max(1f, H - 242f));
            var hit = secViewport.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, .001f);
            secViewport.gameObject.AddComponent<RectMask2D>();
            secBody = SecRect(secViewport, "Secondary scrolling content", 0, 0,
                SecWidth, secViewport.sizeDelta.y);
            var scroll = secViewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = secViewport;
            scroll.content = secBody;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = !ReduceMotion;
            scroll.scrollSensitivity = 28f;
            secY = 0f;
        }

        void SecFinish()
        {
            SecSizeBody(secY);
            SecFooter();
        }

        void SecFooter()
        {
            BottomNav("tools");
            // Translate this page's shared dock, without claiming the parent's other pages are translated.
            var dock = page.Find("Navigation");
            if (English && dock != null)
                foreach (var label in dock.GetComponentsInChildren<TMP_Text>(true))
                {
                    switch (label.text)
                    {
                        case "Trang chủ": label.text = "Home"; break;
                        case "Nhiệm vụ": label.text = "Tasks"; break;
                        case "Quét mã": label.text = "Scan"; break;
                        case "Bản đồ": label.text = "Map"; break;
                        case "JarVis": label.text = "JarVis"; break;
                    }
                }
            SecRespectMotion(page);
        }

        void SecRespectMotion(Transform host)
        {
            if (!ReduceMotion || host == null) return;
            foreach (var motion in host.GetComponentsInChildren<JarVisMotion>(true))
                motion.enabled = false;
        }

        void SecSizeBody(float height)
        {
            secBody.sizeDelta = new Vector2(SecWidth,
                Mathf.Max(secViewport.sizeDelta.y, height + 16f));
        }

        RectTransform SecRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            SecPlace(rect, parent, x, y, w, h);
            return rect;
        }

        void SecPlace(RectTransform rect, Transform parent, float x, float y, float w, float h)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            rect.localScale = Vector3.one;
        }

        RectTransform SecPanel(Transform parent, string name, float x, float y,
            float w, float h, Color fill)
        {
            // The parent's Card supplies its flat surface and radius 22.
            var card = Card("Secondary " + name, x, y, w, h, fill);
            SecPlace(card, parent, x, y, w, h);
            return card;
        }

        RectTransform SecCard(string name)
        { return SecPanel(secBody, name, 0, secY, SecWidth, 100, SecSurface); }

        void SecEndCard(RectTransform card, float innerBottom)
        {
            float height = Mathf.Max(72f, innerBottom + SecInset);
            card.sizeDelta = new Vector2(SecWidth, height);
            secY += height + SecGap;
        }

        float SecCopy(Transform parent, string text, float y, float size = 15f,
            bool bold = false, Color? color = null, float minimum = 24f)
        {
            var label = Label(parent, text, SecInset, y, SecWidth - SecInset * 2,
                minimum, size, bold, color ?? SecInk);
            label.richText = false;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.overflowMode = TextOverflowModes.Overflow;
            // Respect the font size supplied by the parent's Label (including LargeText).
            float height = Mathf.Max(minimum,
                Mathf.Ceil(label.GetPreferredValues(text, SecWidth - SecInset * 2, 0).y) + 4f);
            label.rectTransform.sizeDelta = new Vector2(SecWidth - SecInset * 2, height);
            return y + height;
        }

        float SecHeading(Transform parent, string text, float y = SecInset)
        { return SecCopy(parent, text, y, 22, true, SecInk, 32) + 10; }

        float SecButton(Transform parent, string text, float y, System.Action clicked,
            bool primary = false)
        {
            float height = LargeText ? 64f : 56f;
            var button = Action(parent, text, SecInset, y, SecWidth - 40, height,
                clicked, primary);
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.richText = false;
                label.raycastTarget = false;
                label.fontSize = LargeText ? 17.28f : 16f;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
                var rect = label.rectTransform;
                rect.anchoredPosition = new Vector2(12, 0);
                rect.sizeDelta = new Vector2(SecWidth - 64, height);
            }
            return y + height + 10f;
        }

        void SecTapCard(RectTransform card, System.Action clicked)
        {
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Graphic>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
        }

        TMP_InputField SecField(Transform parent, string name, string value,
            string placeholder, float y, System.Action<string> changed, bool number = false)
        {
            var input = Input(parent, placeholder, SecInset, y, SecWidth - 40, 56, false);
            input.name = name;
            var text = input.textComponent;
            var hint = input.placeholder as TMP_Text;
            text.color = SecInk;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.richText = false;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (hint != null)
            {
                hint.color = SecMuted;
                hint.alignment = TextAlignmentOptions.MidlineLeft;
                hint.richText = false;
                hint.raycastTarget = false;
                hint.textWrappingMode = TextWrappingModes.NoWrap;
            }
            input.targetGraphic = input.GetComponent<Graphic>();
            if (input.targetGraphic != null) input.targetGraphic.raycastTarget = true;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = number ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.Standard;
            input.characterLimit = number ? 4 : 120;
            input.customCaretColor = true;
            input.caretColor = SecBlue;
            input.selectionColor = new Color(.22f, .45f, .95f, .25f);
            input.SetTextWithoutNotify(value ?? "");
            input.onValueChanged.AddListener(next => changed(next));
            return input;
        }

        void SecOriginal(string command)
        {
            bool connected = CommandRequested != null;
            // Use the parent's Emit even while unconnected so LastCommand remains reviewable.
            Emit(command);
            if (!connected)
            {
                Toast(SecT("Demo: chức năng chưa kết nối. Chưa ghi dữ liệu.",
                    "Demo: this function is not connected. No data recorded."));
            }
        }

        void SecModal(string title, string description, string primaryText, System.Action primary)
        {
            OpenModal(title, description, primaryText, primary);
            // The actual parent reserves only 76 units for modal copy. Put long reviews in
            // that same bounded region, so even a 120-character SKU/lot remains reachable.
            if (ModalGroup != null)
            {
                var card = (RectTransform)ModalGroup.transform;
                TMP_Text copy = null;
                foreach (var candidate in card.GetComponentsInChildren<TMP_Text>(true))
                    if (candidate.transform.parent == card && candidate.text == description)
                    { copy = candidate; break; }
                if (copy != null)
                {
                    copy.richText = false;
                    copy.raycastTarget = false;
                    copy.textWrappingMode = TextWrappingModes.Normal;
                    copy.alignment = TextAlignmentOptions.TopLeft;
                    float height = Mathf.Max(76, Mathf.Ceil(copy.GetPreferredValues(description, 324, 0).y) + 4);
                    var viewport = SecRect(card, "Secondary modal details viewport", 20, 174, 324, 76);
                    var hit = viewport.gameObject.AddComponent<Image>();
                    hit.color = new Color(1, 1, 1, .001f);
                    viewport.gameObject.AddComponent<RectMask2D>();
                    SecPlace(copy.rectTransform, viewport, 0, 0, 324, height);
                    var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                    scroll.viewport = viewport;
                    scroll.content = copy.rectTransform;
                    scroll.horizontal = false;
                    scroll.vertical = true;
                    scroll.inertia = !ReduceMotion;
                    scroll.movementType = ScrollRect.MovementType.Clamped;
                    scroll.scrollSensitivity = 24;
                    if (height > 76)
                        Label(card, SecT("Cuộn để đọc chi tiết", "Scroll for details"),
                            20, 156, 324, 16, 10, false, Muted).raycastTarget = false;
                }
            }
            SecRespectMotion(overlayRoot);
        }

        SecondaryBook SecSelectedBook
        { get { return SecondaryBooks[Mathf.Clamp(secBookIndex, 0, SecondaryBooks.Length - 1)]; } }

        string SecBookName(SecondaryBook book) { return English ? book.EnglishTitle : book.Title; }

        void SecSelectBook(int index, string destination)
        { secBookIndex = index; Go(destination); }

        void SecInventory()
        {
            SecBegin(SecT("Tồn kho", "Inventory"), SecT("A1 / A2 / Bản xem demo", "A1 / A2 / Demo preview"));
            var search = SecCard("Inventory search");
            float y = SecHeading(search, SecT("Tìm sách", "Find a book"));
            y = SecCopy(search, SecT("Demo A1/A2. Chưa có số liệu tồn kho hoặc lô hàng.",
                "A1/A2 demo. Stock counts and lots are unavailable."), y, 15, false, SecMuted) + 12;
            SecField(search, "Inventory search", secQuery,
                SecT("Tên, SKU, barcode hoặc A1 / A2", "Name, SKU, barcode or A1 / A2"), y,
                next => { secQuery = next; SecFilterInventory(); });
            y += 68;
            secInventoryStatus = Label(search, "", 20, y, 332, 28, 14, false, SecMuted);
            secInventoryStatus.raycastTarget = false;
            SecEndCard(search, y + 28);

            secInventoryRowsY = secY;
            secInventoryRows = new RectTransform[SecondaryBooks.Length];
            for (int index = 0; index < SecondaryBooks.Length; index++)
            {
                int selected = index;
                var book = SecondaryBooks[index];
                var row = SecCard("Book details " + book.Id);
                y = SecHeading(row, SecBookName(book));
                y = SecCopy(row, "SKU: " + book.Sku, y) + 4;
                y = SecCopy(row, SecT("Điểm giao: ", "Delivery destination: ") + book.Destination,
                    y, 17, true, SecGreen) + 6;
                y = SecCopy(row, SecT("Tồn kho: chưa có số liệu", "Stock count: unavailable"), y, 14, false, SecMuted) + 6;
                y = SecCopy(row, SecT("Chạm để xem chi tiết", "Tap to view details"), y, 14, true, SecBlue);
                SecTapCard(row, () => SecSelectBook(selected, "product"));
                SecEndCard(row, y);
                secInventoryRows[index] = row;
            }
            secInventoryEmpty = SecCard("No matching books");
            y = SecHeading(secInventoryEmpty, SecT("Không tìm thấy", "No matching book"));
            y = SecCopy(secInventoryEmpty, SecT("Thử A1, A2, tên sách hoặc mã. Demo chỉ có 2 sách.",
                "Try A1, A2, a title or a code. This demo contains only two books."), y, 15, false, SecMuted);
            SecEndCard(secInventoryEmpty, y);

            secInventoryTail = SecCard("Original inventory");
            y = SecHeading(secInventoryTail, SecT("Tra cứu thực", "Live lookup"));
            y = SecCopy(secInventoryTail, SecT("Tra cứu để xem số liệu hiện có.",
                "Look up the current stock figures."), y, 15, false, SecMuted) + 12;
            y = SecButton(secInventoryTail, SecT("Mở tra tồn kho gốc", "Open original inventory"), y,
                () => SecOriginal("inventory"), true);
            SecEndCard(secInventoryTail, y - 10);
            SecFilterInventory();
            SecFooter();
        }

        static string SecSearchKey(string value)
        {
            var result = new StringBuilder();
            foreach (char ch in (value ?? "").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                char mapped = ch == '\u0111' || ch == '\u0110' ? 'd' : char.ToLowerInvariant(ch);
                if (char.IsLetterOrDigit(mapped)) result.Append(mapped);
            }
            return result.ToString();
        }

        bool SecMatches(SecondaryBook book)
        {
            string haystack = SecSearchKey(book.Title + " " + book.EnglishTitle + " " + book.Sku
                + " " + book.Barcode + " " + book.Id + " " + book.Destination);
            foreach (var token in (secQuery ?? "").Split(new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries))
                if (haystack.IndexOf(SecSearchKey(token), StringComparison.Ordinal) < 0) return false;
            return true;
        }

        void SecFilterInventory()
        {
            if (secInventoryRows == null || secInventoryTail == null || secBody == null) return;
            float y = secInventoryRowsY;
            int count = 0;
            for (int index = 0; index < secInventoryRows.Length; index++)
            {
                var row = secInventoryRows[index];
                if (row == null) return;
                bool match = SecMatches(SecondaryBooks[index]);
                row.gameObject.SetActive(match);
                if (!match) continue;
                row.anchoredPosition = new Vector2(0, -y);
                y += row.sizeDelta.y + SecGap;
                count++;
            }
            secInventoryEmpty.gameObject.SetActive(count == 0);
            if (count == 0)
            {
                secInventoryEmpty.anchoredPosition = new Vector2(0, -y);
                y += secInventoryEmpty.sizeDelta.y + SecGap;
            }
            secInventoryTail.anchoredPosition = new Vector2(0, -y);
            secY = y + secInventoryTail.sizeDelta.y + SecGap;
            secInventoryStatus.text = count + SecT(" kết quả demo", " demo results");
            SecSizeBody(secY);
        }

        void SecProduct()
        {
            var book = SecSelectedBook;
            SecBegin(SecT("Sản phẩm", "Product"), SecT("Mã sách A1 / A2 đã duyệt", "Reviewed A1 / A2 book codes"));
            var card = SecCard("Product details");
            float y = SecHeading(card, SecBookName(book));
            y = SecCopy(card, "SKU: " + book.Sku, y) + 8;
            y = SecCopy(card, "Barcode: " + book.Barcode, y) + 8;
            y = SecCopy(card, SecT("Điểm giao trong nhiệm vụ: ", "Task delivery destination: ")
                + book.Destination, y, 17, true, SecGreen) + 10;
            y = SecCopy(card, SecT("Demo. Chưa có số liệu lô và tồn kho.",
                "Demo. Lot and stock figures are unavailable."), y, 14, false, SecMuted) + 12;
            y = SecButton(card, SecT("Xem mã / tem", "View code / label"), y, () => Go("labels"), true);
            y = SecButton(card, SecT("Chọn sách khác", "Choose another book"), y, () => Go("inventory"));
            SecEndCard(card, y - 10);

            card = SecCard("Product destinations");
            y = SecHeading(card, SecT("Tiếp tục", "Continue"));
            y = SecButton(card, SecT("Bản đồ kho", "Warehouse map"), y, () => Go("map"));
            y = SecButton(card, SecT("Báo cáo sự cố", "Report issue"), y, SecIncident);
            y = SecButton(card, SecT("Sản phẩm & tem gốc", "Original products & labels"), y,
                () => SecOriginal("product-label"));
            SecEndCard(card, y - 10);
            SecFinish();
        }

        void SecLabels()
        {
            var book = SecSelectedBook;
            SecBegin(SecT("Tem sản phẩm", "Product labels"), SecT("Giữ đúng mã trên sản phẩm", "Keep the product's existing code"));
            var card = SecCard("Original code reference");
            float y = SecHeading(card, SecBookName(book));
            y = SecCopy(card, "SKU: " + book.Sku, y) + 8;
            y = SecCopy(card, SecT("Barcode A1/A2 đã duyệt", "Reviewed A1/A2 barcode"), y, 14, false, SecMuted) + 4;
            y = SecCopy(card, book.Barcode, y, 22, true, SecBlue, 34) + 10;
            y = SecCopy(card, SecT("Demo dùng đúng mã A1/A2. Chọn sản phẩm để xem tem từ mã đã có.",
                "Demo uses the required A1/A2 codes. Select the product to view its existing label."), y, 15, false, SecMuted) + 12;
            y = SecButton(card, SecT("Xem mã gốc trong ứng dụng", "View original product code"), y,
                () => SecOriginal("product-label"), true);
            y = SecButton(card, SecT("Đổi sách mẫu", "Choose sample book"), y, () => Go("inventory"));
            SecEndCard(card, y - 10);

            card = SecCard("Label availability");
            y = SecHeading(card, SecT("Trạng thái in tem", "Label availability"));
            y = SecCopy(card, SecT("Có thể xem và sao chép mã đã có. In tem chưa khả dụng. Khi mất tem, dùng lại đúng mã sản phẩm.",
                "View and copy the existing code. Printing is unavailable. Reuse the same product code when a label is missing."), y, 15, false, SecMuted) + 12;
            var unavailable = Action(card, SecT("In tem / Chưa kết nối", "Print label / Unavailable"),
                20, y, 332, LargeText ? 64 : 56, () => { });
            unavailable.interactable = false;
            y += (LargeText ? 64 : 56) + 12;
            y = SecCopy(card, SecT("Demo chỉ xem tem; chưa có bản in.",
                "Demo label view only; nothing has been printed."), y, 14, false, SecMuted);
            SecEndCard(card, y);
            SecFinish();
        }

        void SecTransactions()
        {
            SecBegin(SecT("Nhập / Xuất kho", "Inbound / Outbound"), SecT("Chọn nghiệp vụ để xem demo", "Choose an operation to preview"));
            SecOperationCard(true);
            SecOperationCard(false);
            var card = SecCard("Transaction status");
            float y = SecHeading(card, SecT("Chỉ xem và đối chiếu", "Review and compare"));
            y = SecCopy(card, SecT("Demo chỉ xem lại số lượng. Chưa ghi giao dịch.",
                "Demo count review only. No transaction recorded."), y, 15, false, SecMuted);
            SecEndCard(card, y);
            SecFinish();
        }

        void SecOperationCard(bool inbound)
        {
            var card = SecCard(inbound ? "Inbound destination" : "Outbound destination");
            float y = SecHeading(card, inbound ? SecT("Nhập kho", "Inbound") : SecT("Xuất kho", "Outbound"));
            y = SecCopy(card, inbound
                ? SecT("Đối chiếu mã sách, lô hàng và số lượng nhận.", "Check the book code, lot and received count.")
                : SecT("Đối chiếu hàng xuất với nhiệm vụ được giao.", "Check the goods issue against your assigned task."), y, 15, false, SecMuted) + 12;
            y = SecButton(card, SecT("Mở biểu mẫu demo", "Open demo form"), y,
                () => Go(inbound ? "inbound" : "outbound"), true);
            SecEndCard(card, y - 10);
        }

        void SecCountForm(bool inbound)
        {
            string route = inbound ? "inbound" : "outbound";
            var draft = inbound ? secInbound : secOutbound;
            SecBegin(inbound ? SecT("Nhập kho", "Inbound") : SecT("Xuất kho", "Outbound"),
                SecT("Biểu mẫu demo / Không ghi giao dịch", "Demo form / No transaction recorded"));
            var card = SecCard(route + " count form");
            float y = SecHeading(card, SecT("Đối chiếu số lượng", "Check actual count"));
            y = SecCopy(card, SecT("Demo. Điền mã, lô và số lượng để xem lại; chưa ghi giao dịch.",
                "Demo. Enter a code, lot and count for review; no transaction is recorded."), y, 15, false, SecMuted) + 14;
            y = SecCopy(card, SecT("Mã sản phẩm", "Product code"), y, 15, true) + 4;
            SecField(card, route + " SKU", draft.Sku, "SKU", y, next => draft.Sku = next);
            y += 70;
            y = SecCopy(card, SecT("Mã lô", "Lot code"), y, 15, true) + 4;
            SecField(card, route + " lot", draft.Lot, SecT("Lô hàng", "Lot"), y, next => draft.Lot = next);
            y += 70;
            y = SecCopy(card, SecT("Số lượng thực tế (cuốn)", "Actual quantity (copies)"), y, 15, true) + 4;
            SecField(card, route + " quantity", draft.Quantity, "1 - 9999", y,
                next => draft.Quantity = next, true);
            y += 72;
            y = SecButton(card, inbound ? SecT("Xác nhận nhập kho demo", "Confirm demo inbound")
                : SecT("Xác nhận xuất kho demo", "Confirm demo outbound"), y,
                () => SecReviewCount(inbound, draft), true);
            y = SecButton(card, SecT("Nhập / Xuất kho", "Inbound / Outbound"), y, () => Go("transactions"));
            SecEndCard(card, y - 10);
            SecFinish();
        }

        void SecReviewCount(bool inbound, SecondaryCountDraft draft)
        {
            int quantity;
            if (string.IsNullOrWhiteSpace(draft.Sku) || string.IsNullOrWhiteSpace(draft.Lot))
            {
                Toast(SecT("Nhập mã sản phẩm và mã lô trước khi xem lại.", "Enter a product code and lot before review."));
                return;
            }
            if (!int.TryParse(draft.Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out quantity)
                || quantity < 1 || quantity > 9999)
            {
                Toast(SecT("Số lượng sách cần là số nguyên từ 1 đến 9999.", "Book quantity must be a whole number from 1 to 9999."));
                return;
            }
            string description = "DEMO\nSKU: " + draft.Sku.Trim() + "\n"
                + SecT("Lô: ", "Lot: ") + draft.Lot.Trim() + "\n"
                + SecT("Số lượng: ", "Quantity: ") + quantity + SecT(" cuốn", " copies")
                + "\n\n" + SecT("Demo: chưa ghi giao dịch. Tiếp tục ở chức năng gốc và nhập lại số liệu đã đối chiếu.",
                    "Demo: no transaction recorded. Continue in the original function and re-enter verified figures.");
            SecModal(inbound ? SecT("Xem lại nhập kho demo", "Review demo inbound")
                    : SecT("Xem lại xuất kho demo", "Review demo outbound"),
                description, SecT("Mở luồng gốc", "Open original workflow"),
                () => SecOriginal(inbound ? "inbound" : "outbound"));
        }

        void SecIncident()
        {
            // Direct calls from AR/tools are overlay-only. Go("incident") also needs a
            // landing page underneath because the actual parent already cleared its page.
            if (ScreenId == "incident")
            {
                SecBegin(SecT("Báo cáo sự cố", "Report issue"), SecT("Xem lại trước khi tiếp tục", "Review before continuing"));
                var card = SecCard("Incident review destination");
                float y = SecHeading(card, SecT("Ghi rõ vấn đề", "Describe the issue"));
                y = SecCopy(card, SecT("Demo: chưa gửi báo cáo. Chuẩn bị vị trí và mô tả vấn đề.",
                    "Demo: no report sent. Prepare the location and issue description."), y, 15, false, SecMuted) + 12;
                y = SecButton(card, SecT("Xem lại báo cáo", "Review issue report"), y, SecIncidentReview, true);
                SecEndCard(card, y - 10);
                SecFinish();
            }
            SecIncidentReview();
        }

        void SecIncidentReview()
        {
            // No Go, camera switch, AR disable or scene load.
            SecModal(SecT("Báo cáo sự cố", "Report issue"),
                SecT("Demo: chưa gửi báo cáo. Ghi vị trí và mô tả trong biểu mẫu tiếp theo. Đóng để tiếp tục màn hình hoặc AR hiện tại.",
                    "Demo: no report sent. Enter the location and description in the next form. Dismiss to continue the current screen or AR view."),
                SecT("Mở biểu mẫu gốc", "Open original issue form"), () => SecOriginal("incident"));
        }

        void SecProfile()
        {
            SecBegin(SecT("Hồ sơ", "Profile"), SecT("Bản xem thử tài khoản", "Account preview"));
            var card = SecCard("Profile preview");
            float y = SecHeading(card, SecT("Nhân viên kho / Demo", "Warehouse operator / Demo"));
            y = SecCopy(card, SecT("Demo. Chưa có thông tin tài khoản hoặc trạng thái trực tuyến.",
                "Demo. Account details and online status are unavailable."), y, 15, false, SecMuted) + 12;
            y = SecButton(card, SecT("Xem trạng thái trực tuyến", "Review online status"), y, SecPresenceReview, true);
            y = SecButton(card, SecT("Hoạt động của tôi", "My activity"), y, () => Go("activity"));
            y = SecButton(card, SecT("Cài đặt", "Settings"), y, () => Go("settings"));
            y = SecButton(card, SecT("Hồ sơ trong ứng dụng gốc", "Open original profile"), y,
                () => SecOriginal("profile"));
            SecEndCard(card, y - 10);
            card = SecCard("Sign out review");
            y = SecHeading(card, SecT("Phiên làm việc", "Work session"));
            y = SecButton(card, SecT("Xem lại đăng xuất", "Review sign out"), y, SecSignOutReview);
            SecEndCard(card, y - 10);
            SecFinish();
        }

        void SecPresenceReview()
        {
            SecModal(SecT("Trạng thái trực tuyến", "Online status"),
                SecT("Demo: trạng thái chưa thay đổi. Bật trực tuyến để đếm giờ, tắt để tạm dừng và bật lại để cộng tiếp trong ngày.",
                    "Demo: status unchanged. Online starts counting, offline pauses, and going online again continues today's total."),
                SecT("Mở điều khiển gốc", "Open original controls"), () => SecOriginal("online"));
        }

        void SecSignOutReview()
        {
            SecModal(SecT("Xem lại đăng xuất", "Review sign out"),
                SecT("Demo: chưa đăng xuất. Đăng xuất kết thúc phiên trực tuyến và giữ tổng thời gian hôm nay. Tiếp tục để xem xác nhận gốc.",
                    "Demo: still unchanged. Signing out ends the online session and retains today's total. Continue to the original confirmation."),
                SecT("Mở xác nhận gốc", "Open original confirmation"), () => SecOriginal("signout"));
        }

        void SecActivity()
        {
            SecBegin(SecT("Hoạt động của tôi", "My activity"), SecT("Thời gian trực tuyến", "Online time"));
            var card = SecCard("Activity connection status");
            float y = SecHeading(card, SecT("Hôm nay", "Today"));
            y = SecCopy(card, "--:--:--", y, 34, true, SecInk, 48) + 8;
            y = SecCopy(card, SecT("Demo: chưa có số liệu", "Demo: figures unavailable"), y, 16, true, SecMuted) + 12;
            y = SecCopy(card, SecT("Mở hoạt động để xem thời gian đã ghi nhận.",
                "Open activity to view recorded time."), y, 15, false, SecMuted) + 12;
            y = SecButton(card, SecT("Xem hoạt động gốc", "View original activity"), y,
                () => SecOriginal("activity"), true);
            y = SecButton(card, SecT("Trạng thái trực tuyến", "Online status"), y, SecPresenceReview);
            SecEndCard(card, y - 10);
            card = SecCard("Activity counting rules");
            y = SecHeading(card, SecT("Cách tính thời gian", "How time is counted"));
            y = SecCopy(card, SecT("Bật trực tuyến: bắt đầu đếm.\nTắt trực tuyến: tạm dừng.\nBật lại: cộng tiếp tổng hôm nay.\nĐặt lại lúc 00:00 Đài Loan (UTC+8).",
                "Online: start counting.\nOffline: pause.\nOnline again: continue today's total.\nReset at 00:00 Taiwan (UTC+8)."), y, 15, false, SecMuted) + 12;
            y = SecCopy(card, SecT("Hỗ trợ theo dõi hoạt động, không dùng để tính lương.",
                "Activity tracking, not payroll attendance."), y, 14, false, SecMuted);
            SecEndCard(card, y);
            SecFinish();
        }

        void SecSettings()
        {
            SecBegin(SecT("Cài đặt", "Settings"), SecT("Tùy chỉnh giao diện demo", "Demo display preferences"));
            SecPreference(SecT("Ngôn ngữ", "Language"),
                SecT("Áp dụng ngay cho các trang demo.", "Applies immediately to demo pages."),
                English ? "English / Change to Vietnamese" : "Tiếng Việt / Đổi sang English",
                () => { English = !English; Go("settings"); });
            SecPreference(SecT("Giao diện", "Appearance"),
                SecT("Nền sáng hoặc tối theo lựa chọn của bạn.", "Choose a light or dark surface."),
                Dark ? SecT("Tối / Chuyển sang sáng", "Dark / Switch to light")
                    : SecT("Sáng / Chuyển sang tối", "Light / Switch to dark"),
                () => { Dark = !Dark; Go("settings"); });
            SecPreference(SecT("Cỡ chữ", "Text size"),
                SecT("Nội dung dài vẫn có thể cuộn để đọc.", "Long content remains scrollable."),
                LargeText ? SecT("Lớn / Dùng cỡ chuẩn", "Large / Use standard")
                    : SecT("Chuẩn / Dùng chữ lớn", "Standard / Use large text"),
                () => { LargeText = !LargeText; Go("settings"); });
            SecPreference(SecT("Chuyển động", "Motion"),
                SecT("Tắt cuộn quán tính và chuyển động trên các trang phụ.", "Disables scroll inertia and motion on secondary pages."),
                ReduceMotion ? SecT("Đã giảm / Dùng mặc định", "Reduced / Use default")
                    : SecT("Mặc định / Giảm chuyển động", "Default / Reduce motion"),
                () => { ReduceMotion = !ReduceMotion; Go("settings"); });
            var card = SecCard("Original settings link");
            float y = SecCopy(card, SecT("Chỉ áp dụng cho phiên demo này. Xem thêm tùy chọn ở cài đặt gốc.",
                "Applies to this demo session only. More options are in the original settings."), 20, 15, false, SecMuted) + 12;
            y = SecButton(card, SecT("Mở cài đặt gốc", "Open original settings"), y,
                () => SecOriginal("settings"));
            SecEndCard(card, y - 10);
            SecFinish();
        }

        void SecPreference(string title, string description, string value, System.Action changed)
        {
            var card = SecCard("Preference " + title);
            float y = SecHeading(card, title);
            y = SecCopy(card, description, y, 15, false, SecMuted) + 12;
            y = SecButton(card, value, y, changed, true);
            SecEndCard(card, y - 10);
        }

        void SecTools()
        {
            SecBegin(SecT("Công cụ", "Tools"), SecT("Tất cả màn hình demo", "All demo destinations"));
            var intro = SecCard("Tools introduction");
            float y = SecHeading(intro, SecT("Bạn muốn làm gì?", "What do you need?"));
            y = SecCopy(intro, SecT("Chọn một công cụ để tiếp tục. Màn hình dùng dữ liệu mẫu được ghi rõ là demo.",
                "Choose a tool to continue. Screens with sample data are marked as demo."), y, 15, false, SecMuted);
            SecEndCard(intro, y);
            // Primary destinations are implemented by the parent; never duplicate them here.
            string[] routes = { "home", "tasks", "detail", "scanner", "scan-products", "scan-A",
                "chat", "map", "ar", "splash", "login", "inventory",
                "product", "labels", "transactions", "inbound", "outbound", "incident",
                "profile", "activity", "settings", "tools" };
            string[] vi = { "Trang chủ", "Nhiệm vụ", "Chi tiết việc", "Quét mã", "Quét sản phẩm", "Quét QR A",
                "AI JarVis", "Bản đồ", "Chỉ đường AR", "Màn bắt đầu", "Đăng nhập",
                "Tồn kho", "Sản phẩm", "Tem sản phẩm", "Nhập / Xuất", "Nhập kho", "Xuất kho",
                "Báo sự cố", "Hồ sơ", "Hoạt động", "Cài đặt", "Công cụ" };
            string[] en = { "Home", "Tasks", "Task details", "Scan code", "Product scan", "Scan QR A",
                "AI JarVis", "Warehouse map", "AR guidance", "Welcome", "Sign in",
                "Inventory", "Product", "Labels", "Transactions", "Inbound", "Outbound",
                "Report issue", "Profile", "Activity", "Settings", "Tools" };
            float tileHeight = LargeText ? 128f : 108f;
            for (int index = 0; index < routes.Length; index++)
            {
                string route = routes[index];
                var tile = SecPanel(secBody, "Tool " + route, index % 2 * 193,
                    secY + index / 2 * (tileHeight + 14), 179, tileHeight, SecSurface);
                var title = Label(tile, English ? en[index] : vi[index], 18, 18, 143,
                    tileHeight - 36, 17, true, SecInk);
                title.richText = false;
                title.textWrappingMode = TextWrappingModes.Normal;
                title.raycastTarget = false;
                title.alignment = TextAlignmentOptions.MidlineLeft;
                SecTapCard(tile, () => { if (route == "incident") SecIncident(); else Go(route); });
            }
            secY += ((routes.Length + 1) / 2) * (tileHeight + 14);
            SecFinish();
        }
    }
}
