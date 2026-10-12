# JarVis Warehouse AR

Unity client for the TEEP warehouse assistant prototype. The first target device is a Samsung Galaxy S20 Ultra running Android. Windows Editor play mode is used for rapid development and testing.

## Baseline

- Unity Editor 6000.4.7f1
- Universal Render Pipeline
- Android ARM64 with IL2CPP
- Minimum Android API level 29
- Vulkan with OpenGL ES 3 fallback
- Portrait orientation
- AR Foundation and Google ARCore XR Plugin
- Adaptive Performance with Google Android provider
- Input System, AI Navigation, uGUI and Unity Test Framework

## Current prototype

- JarVis splash scene with safe-area fade-in/fade-out into the employee login
- Mobile employee login with Associate, Supervisor and Inventory demo roles
- Runtime Vietnamese/English switching without reloading the application
- AR Foundation camera background configured for the mobile URP renderer
- Portrait safe-area dashboard for warehouse tasks
- Mock task browsing, mock barcode matching and local completion progress
- Typed warehouse task/item models with Assigned, In progress, Short pick, Damaged, Completed and Needs review states
- WMS/backend interfaces plus a configurable JarVis chat client with no embedded endpoint or API key
- Honest camera preview/manual barcode lookup service with duplicate suppression and no fake decoder claims
- Label preview formatter, mock exception records and mock JarVis responses
- Waypoint map service, shortest-route planner, 2D route graphic and indoor positioning interfaces
- Safe-area operations workspace with task, scanner, label, chat, exception and warehouse-map panels
- In-app Vietnamese/English toggle that rebuilds the active workspace immediately without reloading the scene
- Editor validation for build order, scene components, serialized references and AR rendering setup

Mock task state is local-only and is not connected to a WMS/ERP or the JarVis backend.
Camera preview is real when permission is granted, but decoding remains manual until a supported barcode package is selected. Label generation intentionally produces a non-scannable preview instead of a fake QR/Data Matrix.

## Android build

Build the prototype with the installed Unity Android toolchain:

```powershell
unity build . --target Android --execute-method ProjectBootstrap.AndroidBuild.BuildApk --output-path "Builds/Android/JarVis-Warehouse.apk" --android-export-type apk
```

The prototype APK is written to `Builds/Android/JarVis-Warehouse.apk`. It uses the
Android debug keystore; configure production signing before distribution.

## Planned modules

1. Connect the existing task/provider interfaces to real WMS task intake
2. Import real shelf/station identifiers into the existing waypoint model
3. Add a supported Code 128, EAN-13 and QR decoding/encoding package
4. Connect the existing route planner and arrow placeholder to a real mapped warehouse
5. Configure and integrate the existing JarVis API client with the approved backend
6. Safe action allowlist for AI suggestions
7. Voice input and answer playback
8. Avatar presentation states

## Backend boundary

The Unity client must never contain an OpenAI API key. It communicates only with the warehouse backend API. AI actions remain proposals until the Unity allowlist accepts them; inventory-changing or other sensitive operations require explicit user confirmation.

## Source control

Generated Unity folders are ignored. Large art, audio, model and video files are tracked with Git LFS. Commit every Unity asset together with its matching `.meta` file.
## JarVis splash integration

`Assets/TutorialInfo/Scripts/JarvisSplashController.cs` provides the first
mobile intro flow: fade in **JarVis**, hold a total splash duration of about 1.8
seconds, fade out, then load `LoginScene`.
`JarvisSceneBuilder` creates and wires the Canvas, labels and build-scene order.
The controller is intentionally offline and contains no API keys; backend/API
wiring belongs in a later step.

The login is explicitly a local mock. It never writes the access code to disk,
and only passes the transient value to `IAuthenticationService` for validation.
