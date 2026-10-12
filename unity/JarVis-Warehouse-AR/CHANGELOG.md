# Changelog

## 2026-09-24

### Added

- Added a reproducible JarVis splash scene builder for the Android-first prototype.
- Added project TODO tracking for incomplete warehouse, scanning, navigation and backend work.
- Added an AR Foundation warehouse scene with a mobile camera background and plane detection.
- Added a portrait safe-area task dashboard with mock browsing, mock scan and completion controls.
- Added a reusable project validator for scene order, components, references and URP AR support.
- Added a reusable Android APK build entry with strict build failure handling and output verification.
- Added a safe-area employee login scene with transient mock authentication and role selection.
- Added runtime Vietnamese/English localization with immediate UI refresh.
- Added typed warehouse task and inventory item models using the required demo records and status set.
- Added mock warehouse providers and backend/WMS interfaces for inventory, tasks, barcode lookup and JarVis chat.
- Added a configurable `JarvisApiClient` that reads its base URL from a ScriptableObject and stores no secret.
- Added on-demand camera preview and manual mock barcode lookup with duplicate suppression.
- Added a traceability label preview service that explicitly marks its 2D code placeholder as non-scannable.
- Added mock JarVis chat responses, microphone/TTS service boundaries and local exception report storage.
- Added waypoint map data, shortest-route planning, indoor-positioning interfaces and a reusable 2D route graphic.
- Added a safe-area operations workspace with scanner, label, chat, exception and map panels layered over the task dashboard.
- Added live camera permission/preview handling that stops the camera when the scanner panel closes.
- Added manual barcode result details, editable traceability fields, local exception submission and waypoint step guidance.
- Added an in-workspace Vietnamese/English toggle that refreshes the UI without reloading the application.

### Changed

- Hardened splash timing and scene transition behavior against missing references and same-scene reloads.
- Updated the mobile URP renderer with `ARBackgroundRendererFeature`.
- Routed the splash flow into `WarehouseScene` instead of the template sample scene.
- Reapplied and saved the Samsung S20 Ultra Android baseline before building.
- Saved Unity 6.4 URP asset migrations produced by the successful Android build.
- Updated the splash to fade in and out within approximately 1.8 seconds before opening `LoginScene`.
- Updated Android Build Settings order to Splash, Login, then Warehouse.
- Replaced the legacy task card data with provider-backed mock tasks, high-priority counts and explicit unverified inventory messaging.

### Validation

- Unity batch script compilation completed with zero C# errors and zero C# warnings.
- Rebuilt `Assets/Scenes/SplashScene.unity` and verified it is the first enabled build scene.
- Rebuilt `Assets/Scenes/WarehouseScene.unity` and passed the Android prototype validator with zero C# errors and zero C# warnings.
- Built `Builds/Android/JarVis-Warehouse.apk` successfully on the first attempt in 7 minutes 14 seconds.
- Verified package `com.teep.jarviswarehousear`, version `0.1.0`, minimum API 29, target API 36, ARM64 and APK Signature Scheme v2.
- Verified APK size `37,692,118` bytes and SHA-256 `856C519D82066D28094163185A31A00EF5CFB686701DF687DF84223A97E699B3`.
- Rebuilt all prototype scenes after adding login/localization with zero C# errors and zero C# warnings.
- Passed the expanded validator for splash timing, login references, scene order and Android settings.
- Compiled and passed validation after adding warehouse models, provider contracts and backend client with zero C# errors and warnings.
- Compiled and passed validation after adding scanner, label, chat, exception and navigation services with zero C# errors and warnings.
- Rebuilt all scenes and passed the final workspace validator with zero C# errors, zero C# warnings and zero logged assertions/exceptions.
- Passed runtime model self-tests for language scope, barcode lookup, unverified inventory, label trace payload and waypoint routing.
