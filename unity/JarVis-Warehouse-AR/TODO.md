# JarVis Warehouse TODO

Last updated: 2026-09-24

## Completed prototype foundations

- [x] Add a JarVis splash scene and make it the first Android build scene.
- [x] Add an AR Foundation warehouse scene with camera background support.
- [x] Add a portrait safe-area task dashboard with clearly marked mock data.
- [x] Add mock task navigation, mock scanning and local task completion.
- [x] Add an Editor validator for build scenes, required components and serialized references.
- [x] Build and validate an ARM64 Android APK with the installed Unity SDK/NDK/OpenJDK toolchain.
- [x] Route the safe-area 1.8-second splash into a dedicated mobile login scene.
- [x] Add local mock login roles for Associate, Supervisor and Inventory without persisting access codes.
- [x] Add immediate runtime localization for Vietnamese and English only.
- [x] Add typed task, inventory item and inventory-result models with mock/unverified flags.
- [x] Add `IInventoryProvider`, `ITaskProvider`, `IBarcodeProvider` and configurable `IJarvisBackend` boundaries.
- [x] Upgrade the task card to the required task states, priorities and demo barcode lookup.
- [x] Add on-demand camera preview/manual barcode lookup with duplicate suppression and explicit decoder limitations.
- [x] Add label preview formatting for every required traceability field without generating a fake scannable code.
- [x] Add JarVis mock chat, voice/TTS interfaces and local warehouse exception records.
- [x] Add waypoint map models, shortest-route planning, 2D route rendering and QR/AR/beacon/UWB positioning interfaces.
- [x] Add safe-area warehouse panels for tasks, camera/manual scan, label preview, JarVis chat, exceptions and waypoint map.
- [x] Add immediate in-workspace Vietnamese/English switching without scene or app reload.

## In progress

- [ ] Install the APK on a Samsung Galaxy S20 Ultra and smoke-test camera permission, AR tracking and UI controls.
- [ ] Smoke-test all runtime workspace panels on the Samsung Galaxy S20 Ultra.

## Next priorities

- [ ] Replace `MockWarehouseRepository` with a real WMS/ERP implementation of the provider interfaces.
- [ ] Add barcode/QR scanning after selecting an Android-compatible scanning package.
- [ ] Add mapped warehouse navigation and AR direction markers.
- [ ] Add a configurable backend endpoint and authenticated JarVis API client without storing secrets in Unity assets.
- [ ] Extend Vietnamese/English localization coverage across every warehouse prototype panel.
- [ ] Add Android device tests for Samsung Galaxy S20 Ultra.

## Known limitations

- Inventory and task data are mock-only until a real warehouse source is connected.
- The prototype contains no API keys and does not call the local AI backend yet.
- `JarvisBackendConfig` intentionally has no default URL; an approved deployment URL must be configured later.
- Camera preview and manual lookup are implemented, but no camera-frame barcode decoding is present.
- Code 128/EAN-13/QR decoding and scannable QR/Data Matrix generation still require a selected Android-compatible library.
- AR plane detection is enabled, but no warehouse map or navigation anchors are available yet.
- The APK is debug-signed and has not yet been smoke-tested on the target Samsung device.
- Runtime workspace UI is compile/serialized-reference validated; physical touch, camera preview rotation and safe-area layout still require device smoke testing.
- The Unity Pipeline CLI package is not installed; validation uses Unity Editor logs and batch build methods.
- Authentication is a clearly marked local demo; a real identity provider is not connected.
