# JarVis Android scanner — 2026-10-13

## Release and evidence

- JarVis Warehouse only. The separate Survey v8/Immersal project is unchanged by this task.
- APK: `E:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse.apk`.
- Package `com.teep.jarviswarehousear`, version 0.1.1 / code 2; ARM64 IL2CPP, min API 29, target/compile API 36.
- Build succeeded at 13:33 on 2026-10-13 (Asia/Saigon); actual APK 49,685,471 bytes. BuildReport totalSize is not APK file size.
- APK SHA256 `48FBAEFBC40461835175BC9490E49D8B79B9203D52951F5EEFC487E440A5AF8A`.
- APK signature verifies and matches the previous APK certificate. This is Android Debug signing, not a store-ready production keystore.
- Copied through Windows MTP to Samsung Galaxy S20 Ultra 5G / Internal storage / Download: `JarVis-Warehouse.apk` and `Milk-Case-Label.png`. Both files were copied back and SHA256 matched their originals. ADB currently lists no device, so installation, launch, logcat and physical camera acceptance are NOT VERIFIED.
- Existing APK preserved as `Builds/Android/JarVis-Warehouse.before-scanner.apk`.

## Inspection and decision

The existing entry is `Assets/Scenes/JarVisFigmaApp.unity`, Unity 6000.4.7f1, native uGUI/TMP. Scan route remains `5:331`. The raised dock action and Tools entry already route there. Existing decoder is ZXing.Net 0.16.10, netstandard2.0 DLL with Apache-2.0 license. Existing inventory DTO/API is reused; no new database, product repository or backend deployment.

Installed packages include AR Foundation 6.4.3, ARCore 6.6.2, AI Navigation 2.0.12, URP 17.4.0 and uGUI 2.0.0. This task does not upgrade XR packages or change the established graphics API. Current scene contains ARCameraManager/XR Origin; legacy scenes/prefabs remain intact. Existing modules provide inventory reading, assigned tasks, checkpoint-map routing and incomplete stock-operation/label interfaces. There are no verified mobile stock-write, returns or printer capabilities to connect to safely.

| Option | Benefits documented upstream | Cost/risk here | Decision |
| --- | --- | --- | --- |
| ML Kit with Unity camera frames/JNI | Bundled model can decode offline immediately; Android barcode formats and input rotation APIs | Adds Android dependency/JNI bridge, frame conversion and task-lifecycle handling; not a ready Unity camera UI | Valid future decoder alternative; no measured performance advantage established on this Samsung |
| Existing ZXing.Net + WebCamTexture | C# decoder/generator, no model download, current Unity app already uses it; worker decoding and existing native UI | No portable torch/zoom API; WebCamTexture/IL2CPP/low light need physical acceptance | Retain and repair, avoiding a second scanner/camera stack |
| CameraX + ML Kit | Lifecycle-aware native preview/analysis, camera controls, keep-only-latest backpressure | Native activity/preview composition and Unity/AR ownership integration, AndroidX/Gradle complexity | Consider if measured WebCamTexture acceptance is inadequate; not silently introduced now |

Official sources (checked 2026-10-13): [ML Kit Android barcode scanning](https://developers.google.com/ml-kit/vision/barcode-scanning/android), [CameraX analysis](https://developer.android.com/media/camera/camerax/analyze), [ZXing.Net upstream](https://github.com/micjahn/ZXing.Net), [Unity 6000.4 WebCamTexture](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/WebCamTexture.html). These support API/design choices, not claims about measured FPS, thermal behavior or recognition speed. No such device measurements were made.

## Architecture and changed owners

- `ImportedCameraScanner.cs`: camera/permission lifecycle, worker decode, result lock, manual retry, safe OMS lookup and translated result feedback.
- `ScannerCameraLease.cs`: single scanner owner; records and disables only enabled AR backgrounds/managers/sessions, waits for camera subsystem stop before opening the webcam, restores recorded enabled state on release. Does not reset ARSession or modify saved scene configuration.
- `BarcodeScanResult.cs`: raw/normalized string identifier, source, format, UTC timestamp, validation, context, matched product ID and error code; exact product-before-lot matching and session identity check.
- `PrototypeScannerPresentation.cs`: retained white/blue shell/dock, wider barcode frame, 48-unit retry action, hidden obsolete tiny manual link, expanding/scrollable result and large-text support. Camera Settings appears only after permission failure.
- `ScannerSweep.cs`: scan line hidden when not scanning or reduced motion is enabled.
- `OmsMobileApi.cs`: GET JSON parsing failure/oversized response returns an error instead of leaving a coroutine stuck. Existing authentication and read endpoint unchanged.
- `AndroidScannerReview.cs`: isolated Editor decoder/UI fixture review and release build/QR verification helper. No production warehouse writes or model requests.
- `Tools/Make-MilkQrLabel.py`, `Docs/ScannerSamples/*`: deterministic QR label, not an AI-drawn or pretend GTIN barcode.
- `ProjectSettings/ProjectSettings.asset`: APK version bumped to 0.1.1/code 2; no camera/XR/graphics reconfiguration.
- `CODEX_CONTEXT.md` and this report: owner/status documentation.

## Pipeline and state handling

Rear WebCamTexture (requested 1280×720/30, actual device resolution may differ) → copy Color32 only when frame updated and worker idle → reusable grayscale buffer / ZXing worker → validate raw string → lock recognition → exact OMS lookup → translated read-only result.

Detection is throttled to at most one launch per 0.3 seconds, not a measured scan latency. Frames are never uploaded or saved. Decoder luminance buffer/reader are reused; library internals and result creation may still allocate. No main-thread decoder work. Background/screen exit increments a generation, stops camera/coroutines, rejects old worker results and clears the previous identifier. Pausing an in-flight lookup offers explicit retry rather than automatic repeated requests. Changing/expiring account/token/endpoint clears visible result and rejects late response.

Lifecycle states are explicit: idle, requesting permission, starting, scanning, looking up, result, paused, error, released. The enum also reserves Decoding for future finer state presentation; worker activity is currently part of Scanning.

Permission denial leaves manual entry usable, offers Android app Settings, and never requests microphone/location for scanning. Other app features still account for existing APK microphone/Bluetooth permissions; those are not newly requested by scanner. A rear camera is required; no silent front-camera fallback. Startup/AR-release waits are bounded. Preview uses cover sizing, video rotation and vertical-mirror metadata, without stretching. Recovery is explicit retry or manual entry. Unsupported torch/zoom buttons are not shown as fake controls.

## Data integrity and integration

`ScannerValidation` preserves identifiers as strings, including leading zeros. It does not trim/change decoded payload, parse it as an integer, open URLs or execute QR JSON. Outer whitespace/control characters, empty/overlong payload, supported URL schemes, Wi-Fi and unsupported JSON content are rejected. Max identifier length is 160, matching the existing form contract.

Recognized `JWM:` checkpoints reuse the current map marker flow; a marker is not a product. Other codes use authenticated GET `/api/oms/mobile/inventory/search?q=...` through existing `OmsMobileApi`. SKU/barcode exact matches take precedence over exact lot-code matches. Lots are explicitly labelled as lot lookups. Multiple product IDs for one identifier produce an ambiguity warning rather than silently selecting the first item. The result shows supplied name/SKU, location, available quantity/unit, lot and expiry; missing fields stay absent/—, not fabricated. Up to three stock rows are summarized; further rows are discoverable in Inventory. Product images/brand are not supplied by this API and are not invented.

Detection itself NEVER changes stock, accepts a task, creates products or prints labels. User must press Scan next before the next camera result; manual Look up retries are deliberate. No offline transaction queue is introduced. Missing network/auth only prevents lookup, not local decoding. No response-body/internal backend error is exposed in scanner UI. Protocol failure, missing items, no match and duplicate-product ambiguity have distinct recovery feedback.

Real connected contexts: product/stock lookup and existing warehouse checkpoint routing. Inbound, outbound, counting, putaway, returns and label recovery are NOT claimed implemented by this release. Preserve the existing form/task back stack. Add a typed return callback only when an actual corresponding write capability exists; do not expose placeholder transaction success.

## Samsung use

1. Open My Files → Internal storage → Download → `JarVis-Warehouse.apk`; approve installation from My Files if Android asks. This updates JarVis Warehouse, not Survey v8. Keep existing app/data; do not uninstall to bypass a signature conflict.
2. Sign in with the existing warehouse employee/admin account, go Online if the app's presence gate requires it, then tap the raised Scan action or Tools → Scan code.
3. Allow camera permission. Hold one readable code in good light; move closer if necessary. Lookup remains read-only.
4. Use Scan next for another code, Look up for deliberate lookup retry, or Enter manually if the camera/permission is unavailable.
5. Open the sample QR on a PC/another phone or print it. The scanning phone cannot point its rear camera at its own display.

Sample payload: `MILK-180ML-CASE24-TEST`; label describes a sample case of 24×180 ml. This is NOT a manufacturer GTIN, a stock movement or a product created in OMS. Not found is expected unless a real matching SKU/barcode already exists. For an actual milk product, replace the test payload with its registered case SKU/barcode through the established workflow; do not seed production to make a demo pass. Expiry and lot are read from warehouse records, not embedded as SKU identity.

## Build and verification

Existing Editor must not be replaced/duplicated. When it is closed, Unity batch runner: `ProjectBootstrap.AndroidScannerReview.Run` (omit `-quit`, since Play review exits itself). When saved and not playing in a connected Editor, invoke the same method via Unity CLI. No scene rebuild/import is needed.

After generating QR image in that review, run `Tools/Make-MilkQrLabel.py` using the bundled Python with Pillow; `ProjectBootstrap.AndroidScannerReview.Build` verifies the final label image, bumps version and reuses `ProductWorkspaceReview.BuildAndroid` with current scenes/settings. Output remains the stable APK path. APK tools confirm ARM64, permissions and signature independently of Unity's BuildReport.

| Verification | Status / evidence |
| --- | --- |
| C# compile, Unity build | PASS. Fresh `Logs/AndroidScanner-Compile.log`, `AndroidScanner-Review.log`, `AndroidScanner-Build.log`; no C# errors |
| Eight required formats on generated images | PASS: QR, Code128, Code39, EAN13, EAN8, UPC-A, UPC-E, ITF, plus 90/180/270-degree rotation each |
| Input/frame/exact-match checks | PASS. Leading zeros, unsafe/overlong payload rejection, invalid frame dimensions, product-before-lot match |
| Decoder suite | 42 PASS in `Logs/AndroidScanner/Decoder.txt`; synthetic images, not physical labels |
| Native UI/state regression | 61 PASS in `Logs/AndroidScanner/UI.txt`; VI/EN, light/dark, 390×844 with large text, scan lock, next/reopen, unknown and ambiguous match, ten close/reopen cycles; fixture data explicitly disclosed |
| Final labelled QR | PASS in `Logs/AndroidScanner/Label.txt`, exact payload round trip |
| APK metadata/signature | PASS, ARM64 IL2CPP, API29/36, CAMERA present, same signing certificate as previous build |
| Transfer to Samsung | PASS through MTP, byte sizes and SHA256 readback for APK and PNG |
| Physical camera/orientation/permission denial/autofocus, AR switching, background/lock, low-light/curved/damaged/multiple codes, 1D labels | NOT TESTED on device; Editor checks cannot establish these |
| Real authenticated OMS success/401/403/404/500/timeout, cross-warehouse permissions | NOT TESTED this turn; existing endpoint used without provider calls/production mutations |
| Installation/launch/logcat | BLOCKED by absent ADB device; APK delivered through MTP for user installation |
| Stock-write/returns/printing flows | BLOCKED by missing current mobile capabilities, not falsely claimed complete |
| Long-run CPU/memory/battery/heat/FPS | NOT TESTED; no numeric performance promise |

First UI inspection found and fixed an invisible retry label and an obsolete undersized manual link. Second native review passed. Mechanical design detector on the changed presentation returned no findings. Screenshots are in `Logs/AndroidScanner`, with black camera area intentionally disabled in Editor. They are not evidence of Android black-camera behavior.

### UX score (provisional, not release acceptance)

Native layout review: 13/15 for UI/UX, based on translated 48-unit controls, visible next action, large-text wrapping and one-handed flow. Result can still require scrolling with many lots; screen-reader support remains unverified. No honest total out of 100 can be assigned while camera (20), physical recognition (20), real business integration (20), device performance/stability (15) and device/security acceptance lack evidence. Unverified is not PASS and is not an invented measured failure. The product is a test APK, not certified production-ready.

## Remaining work

1. Device acceptance: install, real QR/1D scans, permission denial/retry, background/reopen and AR-camera ownership; use ADB authorization for reproducible logcat.
2. Real OMS lookup with existing employee account and registered case barcode, including unauthorized/offline response handling.
3. Measure real focus/lighting/thermal/memory behavior; choose CameraX/ML Kit only if evidence warrants the extra native integration.
4. Add stock-write/printing context callbacks only with real backend capabilities, explicit confirmation and idempotency. No warehouse transaction has been implemented or tested by this scanner change.
5. Resolve pre-existing AR simulation temporary-asset collision warnings after reference audit; do not delete unknown XR assets to silence them. Build succeeded despite these warnings and existing obsolete-API warnings elsewhere.

Git synchronization/commit/push evidence is recorded in `Docs/ScannerSamples/RELEASE.md`; no APK, keystore, credentials, private map or unrelated backend changes belong in that commit.

## Login network follow-up (after initial delivery)

Owner reported connection failure on Samsung after installing the initial scanner APK. A single public `/api/health` check from PC returned HTTP 200/status ok in 0.269 seconds; this does NOT verify the phone's DNS, TLS, clock, Wi-Fi or endpoint override. Phone app-private override file could not be accessed via MTP and was not modified. Windows PnP reports problem code 28 for the Samsung Android debugging interface (missing driver), which blocks ADB diagnostics but does not itself explain the phone app's Internet failure. File transfer works independently through MTP.

Confirmed client weakness: login used the generic 20-second request timeout while project Render configuration selects the free plan. [Render documents](https://render.com/docs/free#spinning-down-on-idle) idle spin-down after 15 minutes and approximately one-minute spin-up. This is a plausible timeout cause, NOT a confirmed diagnosis of this phone incident. The hotfix extends only login to 90 seconds, provides a connecting message, differentiates safe DNS/TLS/timeout/502–504 feedback from wrong credentials, and handles malformed/oversized login JSON without leaving the form stuck. TLS validation remains enabled; no certificate bypass, password change, production database mutation or automatic retry was added. Inventory and AI timeouts are unchanged.

Hotfix version is 0.1.2 / code 3. Twelve isolated connection-policy checks pass in `Logs/AndroidScanner/ConnectionPolicy.txt`; translation audit now has 634 entries, 212 unique imported labels and no untranslated labels. The follow-up APK/build/transfer receipt is in `Docs/ScannerSamples/RELEASE.md`. Actual successful Samsung login remains NOT VERIFIED until the owner retries and/or phone browser health response/logcat is available. Do not claim the network issue is resolved just from the longer timeout.
