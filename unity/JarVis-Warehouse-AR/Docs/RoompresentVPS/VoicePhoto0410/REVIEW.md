# JarVis 0.4.10 — ảnh đi kèm voice trong hội thoại

## Lỗi và thay đổi

- Luồng voice trước đây chỉ thêm khung ghi âm vào chat; ảnh chỉ đi theo yêu cầu lên server. Khi thử lại, nó còn lấy ảnh hiện tại của ô soạn thay vì ảnh ban đầu.
- Chụp bản sao ảnh vào chính VoiceMessage lúc gửi; dựng ảnh thật cùng khung voice trong một mục hội thoại ngay lập tức. Không tạo thêm hàng ảnh khi gửi lại.
- Upload/retry dùng ảnh gắn với bản ghi và cùng message ID. Thành công không xóa ảnh nháp mới của người dùng.
- Giới hạn giữ 60 mục hội thoại áp dụng cho cả nhóm ảnh + audio; xóa nhóm sẽ giải phóng texture, byte ảnh và tệp WAV tạm.
- Giữ font, logo, giao diện sáng/tối và cách gửi chữ/ảnh riêng. Không đổi luồng barcode, QR, cảnh AR, điểm A/B/C hoặc reset tracking.

## Kế hoạch bố cục nhỏ

- Giao diện UGUI hiện tại, sửa tại chủ sở hữu ImportedJarvisChat / ImportedChatMedia / VoiceMessage.
- Một nhóm dọc: ảnh căn phải, khoảng cách 8 đơn vị, khung voice căn phải bên dưới. Không thêm lớp cuộn; dùng Conversation ScrollRect sẵn có.
- Không dùng ảnh sinh AI. Ảnh thử là ảnh vở A1 thật đã có trong Resources/Catalogue.

## Bằng chứng đã thực hiện

- VoicePhoto0410Review.Run PASS ALL, Unity exit 0; ảnh native Unity ở hai tỷ lệ 1080×2400 và 1080×1920, VI/EN và light/dark.
- Kiểm tra ảnh sao chép không bị đổi theo composer, không được gắn lại vào retry; voice-only vẫn một mục; ảnh và voice chung một nhóm và không chồng; ảnh giữ nguyên khi mô phỏng Sending/Failed/Completed; dọn hàng giải phóng audio và ảnh.
- Kiểm tra source đường upload/retry dùng AttachedPhoto và không thêm hàng/xóa nháp ở retry.
- Hash cảnh AR, SmallRoomAR, bộ xác minh giao hàng và registration giữ nguyên trong quá trình kiểm tra.
- Đã xem ảnh Unity light/VI/Failed và dark/VI/Completed. voice-photo-review.png ghép ảnh kết quả kiểm tra; trạng thái Completed là fixture UI, không phải chứng nhận cuộc gọi AI thật.

## Giới hạn

Audio kiểm tra là PCM tổng hợp, không phải tiếng nói người dùng. Chưa chứng minh microphone, trả lời AI thật hoặc đường truyền retry trên Samsung bằng cuộc hội thoại thực. Không thay backend/provider. Handoff sau cài đặt và hash dữ liệu thiết bị được ghi riêng trong handoff.json khi có kết quả.
