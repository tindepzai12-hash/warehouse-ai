# JarVis 0.4.0 / code 24 — two-book delivery verification

## Artifact

- Full application: `D:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse-0.4.0.apk`
- Final build log: workspace `tracking-review-20261120/unity-040-build-final.log`
- StrictMode result Succeeded, 0 errors, 68.3648152 seconds.
- 90,328,143 bytes. SHA256 `D4E7E8CFC132000C8049388D5DD73F01AA18679973AC57DF5D616B72BF6B596B`.
- APK v2 signature verified with Unity's bundled Java and Android apksigner.
- Intermediate APKs with this same version were superseded by the final hash above; compare hash, not version alone.

## Implemented workflow

1. Accept isolated Roompresent delivery trial; freshly scan rear-camera book A1 `4714551570610` and A2 `4714551570603` in either order.
2. Each unique correct book opens a blocking success popup with bullet facts and a single OK action. No timed auto-next. Final OK advances to QR A. Wrong/duplicate/manual input cannot count as the other book. A separate acknowledged gate prevents leaving a popup from bypassing OK.
3. AR camera initializes for metric measurement, with an explicit QR A card and no spatial directions yet. Only trusted measured fixed QR A (`JWM:MAIN:E`) and the saved operator-checked registration unlock pins/arrows. QR identity alone and product barcode never fabricate pose.
4. Compact bottom dock contains two rows: navigate B / confirm A1 at B; navigate C / confirm A2 at C. One active floor route and both destination pins. Default remains shortest computed route; user may choose B or C.
5. Confirm enters an ephemeral two-step proof using the same AR camera: destination QR first, then matching book barcode. Wrong QR/product leaves the correct step pending; cancellation, pause, tracking loss or invalid proximity discard unfinished QR proof.
6. Correct ordered pair commits local trial progress, greys/disables completed controls, and automatically routes to remaining destination. Both deliveries complete the trial. No inventory or OMS transaction is posted.

## UI / change scope

Existing UGUI/PrototypeUi surfaces, Be Vietnam Pro font, CanvasScaler 412x915 and safe area retained. Repair scoped to ImportedCameraScanner, RoompresentDeliveryMission and SmallRoomAR; new RoompresentScanPopup, RoompresentDeliveryHud and RoompresentDeliveryVerification owners. Shared PressFeedback is disabled only on HUD-owned buttons to prevent opacity conflict. No art generation or scene reconstruction.

Owner's compact-UI correction applied: normal dock 378x168 logical units, two 48-high button rows with 8-unit gaps. Verification 378x188 replaces the dock instead of stacking above it; roughly 80% of portrait height remains clear. Startup card 378x126; success card 360x308. Completed state uses an explicitly gray surface and clear text, not color alone. No setup B/C/focus/back controls in normal AR.

## Evidence

- `Logs/RoompresentVPS/Delivery040/VI/result.txt` and `EN/result.txt`: PASS after final runtime source changes.
- `unity-040-final-bilingual.log`: batch exit 0. Both languages rendered at 1080x2400 and 1080x1920; active text overflow assertions passed.
- UI screenshots: `VI|EN/{start,delivery,completed-B,proof-QR,proof-product,popup}-{1920,2400}.png`. These are UI-only Editor renders over a plain background, not physical camera alignment evidence. Shadow/surface layout callbacks executed explicitly before captures. Visual review found compact layout, legible labels, no card overlaps, gray locked completed state and unclipped step instructions.
- State checks: both B-C/C-B orders; fresh scans vs persisted flags; duplicate/manual rejection; OK acknowledgement; repeated OK exactly once; product-before-QR, wrong destination/product, QR-only legacy completion, cancel, tracking loss, distance NaN/Infinity/negative/far, and repeat-delivery rejection; persisted progress round trip.
- Runtime AR EAN decoder reads generated EAN13 of both actual book IDs. This is decoder verification, not real camera proof.
- Saved operator coordinates validated against exact map/scan fingerprints; no coordinate changes. Phone's pre-update registration SHA256 `6889ac8f49c1282725b97151a3f73785e62e0faedf1c9a690614b8a6a82eb2b8`.
- Source backup: `Library/RoompresentVpsBackups/Delivery040-20261120`.

## Remaining device acceptance

Physical fresh-book scans/OKs, QR A acquisition, navigation B/C tapping while walking and both ordered destination proofs require current Samsung exercise. Editor screenshots/tests do not establish these outcomes. AI chat/voice/images and server behavior were not retested or changed in this delivery/UI task. Markerless Immersal relocalization is outside this approved QR-A flow.

## Installed device evidence

Final ADB replacement install returned Success, then app launched on Samsung R3CN20R5Q2F. `pm path` + `sha256sum` of installed base.apk exactly matches final hash `d4e7e8cfc132000c8049388d5dd73f01aa18679973ac57df5d616b72bf6b596b`; `dumpsys package` shows 0.4.0/code24. Saved registration hash remains unchanged. Fresh `samsung-040-final-start.png` shows the real login screen; owner login is required before the new physical workflow can be exercised. User was asked to log in privately. No credentials were read or submitted by the agent.

Owner then logged in and exercised startup on the final APK. PID13734 device log (device clock): 15:59:15 A1 freshBoth=False awaiting OK; 15:59:23 A2 freshBoth=True awaiting OK; 15:59:27 QR A decoded; 15:59:28 metric native anchor created calibrated=True using JWM:MAIN:E; tracking recovered; 15:59:29 optimal next stop B. Fresh-book/OK-gated transition and fixed-A acquisition are therefore observed, superseding the login/startup-pending note above. Latest `samsung-040-login-ready.png` showed the recovery card while camera aimed at a low-detail surface; no claim of continuous physical tracking. Owner was asked to test manual C selection and an ordered delivery confirmation. Both physical destination completions remain pending.
