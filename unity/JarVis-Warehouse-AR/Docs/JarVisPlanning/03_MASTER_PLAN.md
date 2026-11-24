# Master plan phát triển Jarvis qua bốn chat

## Mục tiêu

Tiếp tục **một** ứng dụng JarVis Warehouse Android-first, có Unity UI/AR, OMS backend và AI assistant, dựa trên hiện trạng đã tồn tại. Chat `planning` chuyển yêu cầu thành phiếu có owner, giới hạn, bằng chứng và nghiệm thu. Ba chat còn lại thực thi theo chuyên môn; không dựng project song song hoặc báo thành công từ mock.

## Kiến trúc làm việc được quan sát

- Unity: `D:\unity of teep\JarVis-Warehouse-AR`, scene chính `Assets/Scenes/JarVisFigmaApp.unity`, Unity `6000.4.7f1`. Đọc `AGENTS.md`, `CODEX_CONTEXT.md`, `UNITY_UI_WORKFLOW.md` trước sửa liên quan.
- OMS backend và live DB: `E:\warehouse-ai`; E: đang không hiện. Khi ổ chưa ổn định, không thử phục hồi bằng seed/mock hay copy một database mới vào D:.
- Routes thử trong phòng: QR A/B, một map ghi, tuyến AR, APK 0.3.x. Tách rõ module này với bản đồ kho 2D, UI chính và backend OMS; chỉ tích hợp khi phiếu yêu cầu và contract đã rõ.
- UI hiện tại dùng prototype B, uGUI/native, không tự rebuild scene/Figma cho sửa logic C#. Ảnh/mô phỏng phải được đối chiếu với render Unity và kiểm tra máy thật nếu liên quan camera/AR.

## Cổng 0 — ổn định nguồn và bảo toàn dữ liệu

1. Kiểm tra E: có được Windows nhìn thấy ở tầng disk hay không, backup live DB và mã backend trước mọi thay đổi backend. Nếu E: vẫn ngắt, **dừng phiếu backend**; tiếp tục công việc chỉ đọc hoặc Unity D: không phụ thuộc nó.
2. Kiểm tra D project và Editor đang mở. Không chạy Editor batch thứ hai cùng project; không đóng cửa sổ có scene chưa lưu.
3. Ghi `current-state` trong phiếu: đường dẫn, file đổi sẵn, APK/version/hash, backend endpoint, điểm chưa xác nhận. Không dùng trạng thái chat cũ như trạng thái hiện tại.

**Ra cổng:** nguồn cần thiết truy cập được và đã phân loại `live / mock / không có`; không có nguy cơ ghi nhầm dữ liệu thật.

## Cổng 1 — lập backlog và chọn một outcome

Planning nhận yêu cầu người dùng, đối chiếu chat và source, nêu kết quả người dùng thấy được. Chọn một phiếu đủ nhỏ để hoàn thành trong một lượt. Phiếu ghi ID, ưu tiên, owner chat, phạm vi file, thứ không được đụng, phụ thuộc, tiêu chí chấp nhận và bằng chứng tối thiểu. Với yêu cầu mới mâu thuẫn kế hoạch cũ, chỉ cập nhật phiếu liên quan và ghi lý do.

**Ưu tiên hiện thấy (chưa tự giao):**

1. Hoàn tất/chứng thực Routes 0.3.2 trên Samsung: trạng thái QR, khóa A, đường thẳng không rẽ giả, B không biến mất khi lệch thoáng qua, tracking và xử lý pause. Có thể cần clip và log máy thật.
2. Giữ login/AI cùng backend thật, không quay lại preview; xác nhận API và flow khi E: trở lại. Không đánh đồng backend Render với local.
3. UI/ảnh Jarvis: chỉ nhận brief có vai trò trong app; chat hình ảnh hiện có nhiệm vụ Gao Big Lion riêng.
4. Voice VI/EN, khả năng ZH theo spec, media permission và STT/TTS: phải phân tách code fixture, API đã publish và Samsung thật.
5. Model runtime Jarvis: nghiên cứu/eval riêng, sau khi backend ổn định; không thay cùng phiếu UI hoặc AR.

## Cổng 2 — nghiên cứu và thiết kế giải pháp

Chat nhận phiếu đọc đúng owner/code/log; ghi giả thuyết và cách loại trừ. Với UI/hình ảnh, nêu nguồn tham chiếu và hợp đồng bố cục. Với lỗi, tái hiện bước tối thiểu, phân biệt symptom/root cause. Với kiến trúc, ghi phương án, ảnh hưởng hợp đồng, rollback. Không nghiên cứu rộng không cần thiết; chỉ browse tài liệu hiện hành khi phụ thuộc có thể thay đổi hoặc cần nguồn chính xác.

**Ra cổng:** hướng triển khai giải quyết đúng tiêu chí, không đòi dữ liệu/thiết bị chưa có để giả báo hoàn thành.

## Cổng 3 — thực hiện có owner

- Một phiếu có một chat thực thi chính. Planning không tự sửa code thay chat đang nhận phiếu.
- Không hai chat cùng ghi scene/prefab/owner C# hoặc live DB. Nếu cần API và Unity cùng lúc, chia `BACKEND-*` rồi `UNITY-*`, nêu contract và chốt thứ tự; tích hợp sau khi hai bên có bằng chứng.
- Sửa nhỏ, giữ các thay đổi sẵn, không reset/xóa assets/DB để đạt test. Bảo mật key/credential.
- Sau mỗi nhóm thay đổi, kiểm tra tập trung đúng module. Chỉ build APK khi compile và các kiểm tra liên quan đã đạt; không cài/chép lên máy nếu thiếu điều kiện user/thiết bị.

## Cổng 4 — nghiệm thu theo tầng

| Tầng | Bằng chứng tối thiểu | Không suy ra |
|---|---|---|
| 1. Static/compile | File, command, exit, lỗi compiler | Runtime UI đúng |
| 2. Logic/unit | Test case và kết quả mới | Camera/AR thật |
| 3. Unity Play/render | Ảnh trạng thái, raycast, console | APK/Samsung đạt |
| 4. API tích hợp | Endpoint, chế độ live, status, DB được dùng (không lộ bí mật) | App Android kết nối được |
| 5. Android package | Package/version/signature/hash, build log | Đã cài/đi thử |
| 6. Thiết bị thực | Model máy, APK, thao tác, video/log, kỳ vọng và kết quả | Bao phủ mọi điều kiện kho |

Mỗi phiếu chỉ đòi tầng phù hợp. Nếu yêu cầu người dùng là AR thực địa thì tầng 6 là bắt buộc trước câu “đã sửa xong”; nếu thiếu thiết bị, ghi `chưa nghiệm thu` và hướng thử cụ thể.

## Cổng 5 — báo cáo và bàn giao

Chat thực thi trả đúng mẫu trong `05_DISPATCH.md`: kết quả, file, test, bằng chứng, giới hạn, rollback, bước tiếp theo. Planning đối chiếu tiêu chí, chấp nhận hoặc trả lại cùng phiếu với lý do rõ. Sau khi chấp nhận, mới mở phiếu phụ thuộc tiếp theo. Mỗi phiếu kết thúc có trạng thái `DONE`, `PARTIAL`, `BLOCKED` hoặc `NEEDS_DEVICE`; không dùng “xong” khi thiếu tầng nghiệm thu bắt buộc.

## Quy tắc ưu tiên khi thiếu E:

Nếu E: mất, chỉ làm phần Unity D: độc lập với backend và tài liệu planning. Không chạy backend, không sửa DB, không báo login/AI live đạt. Nếu E: trở lại, ưu tiên sao lưu và xác minh ổn định; không chạy build/test ghi nhiều lên E: trước khi xử lý nguy cơ ngắt kết nối.
