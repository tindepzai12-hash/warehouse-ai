# JarVis Warehouse — Figma handoff

**Current row rule (2026-10-02):** edit the lower row only. The owner's newer instruction supersedes the older upper-row/reference language in this file. See `E:\warehouse-ai\docs\JARVIS_CHANGE_MAP.md` for the compact cross-platform contract.

Updated: 2026-09-27 (Asia/Saigon)

## Figma source

- File: https://www.figma.com/design/GGBvTqIQRqLP4cZv1LHAfT/Untitled
- File key: `GGBvTqIQRqLP4cZv1LHAfT`
- Main page: `Page 1`
- Mobile frame size: `412 x 915`
- Visual direction: clean warehouse mobile UI, primary blue + white, consistent with the Vinamilk-like blue/white direction requested by the owner.

## Required reading for the next Codex

1. Read `AGENTS.md` and `CODEX_CONTEXT.md` in this project.
2. Open the Figma file above and inspect the current visible frames before editing.
3. Preserve all existing user content. Prefer hiding obsolete frames/layers instead of deleting them.
4. Keep every mobile screen at 412 x 915 unless the content is intentionally scrollable.
5. Keep the bottom navigation and its center scan button in the same position as `home-dashboard`.

## Completed in the latest Figma pass

- Reordered the major frames into a logical product flow: Splash, Login, Offline Home, Home, Task List, Task Detail Steps 1–4, Barcode Scanner, Product Detail, Label Creation, Inventory, Import, Export, Warehouse Map, Incident Report, AI Chat, Settings.
- Standardized 16 `center-scan-btn` instances to the Home position: X 174, Y -24, W 64, H 64 inside `nav-row`.
- Fixed CTA/scan-button overlap on task detail screens; Step 4 now shows `Hoàn tất & về danh sách` above the bottom navigation.
- AI Chat uses the same JarVis hard-hat robot mascot as Login, scaled to approximately 42 x 42 and aligned with the chat title.
- Hid the stray JarVis mascot that appeared under `import-export-export`.
- Warehouse map: made the two rightmost rack columns match the other columns (X 314, W 60) and restored even spacing.
- Settings frame height restored so Security / Change password and the lower system options are not cut off.
- Created a corrected Offline Home by duplicating Home:
  - New frame: `home-dashboard-offline-new`, node `32:2`, placed at X 717, Y -458.
  - Old `home-dashboard-offline` is hidden and remains as a rollback copy.
  - Status button text changed to `BẬT TRỰC TUYẾN`.
  - `dashboard-body` is hidden, leaving a clean pale-grey empty work area with no missions while offline.
  - Bottom navigation and center scan button remain aligned with Home.
- Finished the Task List update:
  - Split the accidental 412 x 1773 combined `home-dashboard` into two independent 412 x 915 frames: `task-list` and `home-dashboard`.
  - `task-list` is placed separately at X 1168, Y -1450 and uses Clip content so it no longer appears physically attached to Home.
  - Filter order is `Tất cả` -> `Đang làm` -> `Đã nhận` -> `Đã hoàn thành`.
  - The final filter remains partially visible so the user can swipe horizontally to reveal it.
  - Added the completed task `Kiểm kê khu vực Kệ D` with the `Xem lại` action.
  - Removed two accidental top-level `Khu D` text layers that overlapped the Kệ C card.
- Added the completed-task review screen `task-review-completed-d`:
  - Title: `Xem lại kiểm kê Kệ D`.
  - Removed the task stepper, product list, quantities, expiry information, discrepancy data, extra success icon, and review CTA.
  - The screen now shows only the three requested fields: `Mã nhiệm vụ`, `Thời gian làm nhiệm vụ`, and `Nội dung nhiệm vụ`.
  - Current demo content uses task ID `INV-D-0927`, time `27/09/2026 • 14:05–14:35`, and a concise Kệ D stock-count description.
- Finished the AI Chat lower layout:
  - `body-chat` height is 690 so suggestion chips, message input, send button, and bottom navigation align near the bottom of the 412 x 915 screen.
  - Added a visible white vector arrow pointing left-to-right inside the blue circular send button (`send-arrow-ai-chat`).
  - Hid stray experimental arrow layers that appeared in the chat body.

## Current screen names

- `splash-screen`
- `login-screen`
- `home-dashboard-offline-new` (use this one)
- `home-dashboard`
- `task-list`
- `task-review-completed-d`
- `task-detail`
- `task-detail-step-2`
- `task-detail-step-3`
- `task-detail-step-4`
- `barcode-scanner`
- `product-detail`
- `label-creation`
- `inventory-management`
- `import-export`
- `import-export-export`
- `warehouse-map`
- `incident-report`
- `ai-chat`
- `settings-screen`

## Visual acceptance checklist

- Offline Home must look like Home, but show `BẬT TRỰC TUYẾN`, a grey/disabled empty body, and no tasks.
- Online Home must keep task cards, quick actions, shift progress, and `ĐANG TRỰC TUYẾN`.
- Every bottom navigation center scan button must visually line up with Home.
- Task detail flow must keep all Steps 1, 2, 3, 4; do not remove content to make frames shorter.
- Splash, Login, and AI Chat must use the same JarVis mascot source.
- Import and Export must remain separate screens; Export uses the blue export action and must not contain a floating mascot.
- Warehouse map rack widths and aisles must remain even. Destination marker must be red and listed below the legend.
- Incident Report must retain the emergency call action.
- Settings must retain the Security module and Change password action.

## Remaining work for the next Codex

1. Review every frame at 100% zoom for any text clipping introduced by Vietnamese labels. Task List, the Kệ D review screen, and AI Chat were visually checked in this pass.
2. Rebuild/verify the top header of `home-dashboard`: the Task List/Home split is correct, but the online Home header should still show the user/status area and the `ĐANG TRỰC TUYẾN` control above the next-task card.
3. Verify prototype links for Splash -> Login -> Offline Home -> Online Home -> Task List -> Kệ D review.
4. Add or verify the Splash animation using the Login mascot; keep the background white.
5. Verify all Task Detail CTA labels follow the sequence: Bắt đầu nhiệm vụ -> Chỉ đường đi -> Xác nhận số lượng -> Hoàn tất & về danh sách.
6. Check the Warehouse Map destination marker and legend in presentation mode.
7. If the AI Chat frame is moved later, nest/group `send-arrow-ai-chat` with `send-btn` so the vector follows the button.
8. Run a full visual pass in Prototype/Present mode, then update this file with the final results.

## Update 2026-09-27 — Locked Home Dashboard

- Rebuilt `home-dashboard 1` as a 412 x 915 parent frame containing the preserved `home-dashboard-1-content` and a separate `locked-state-overlay`.
- Added a 62% dark-grey scrim below the header so dashboard cards, quick actions, recent activity, scan button, and bottom navigation read as disabled while remaining visible for context.
- Added a centered white vector lock icon; no emoji or raster placeholder is used.
- Added the requested locked-state copy with deliberate mobile line breaks:
  - `Tính năng đang bị khóa.`
  - `Vui lòng bật chế độ TRỰC TUYẾN`
  - `để truy cập các tính năng.`
- Kept the header and activation control above the scrim. The control now reads `BẬT TRỰC TUYẾN` on one line.
- Applied `Be Vietnam Pro` to all 676 text layers on Page 1 so the locked dashboard and the rest of the current design use one typography family.
- Visually verified the final frame at 84% zoom: 412 x 915, no clipping, header remains readable, and the lock/message hierarchy is centered.

## Suggested prompt for Codex on another machine

Read `AGENTS.md`, `CODEX_CONTEXT.md`, and `FIGMA_HANDOFF.md` first. Continue the JarVis Warehouse Figma work in the linked file. Inspect the current frames before editing and preserve completed work. Finish the remaining acceptance checklist in `FIGMA_HANDOFF.md`, keep all screens at 412 x 915 except intentionally scrollable content, keep the center scan button aligned exactly like `home-dashboard`, and do not remove functional content. After each group of edits, visually verify the affected screens in Figma and update `FIGMA_HANDOFF.md` with what changed and what remains.
