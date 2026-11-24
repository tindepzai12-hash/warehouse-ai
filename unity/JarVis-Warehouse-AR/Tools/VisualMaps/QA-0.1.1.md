# Verification — 2026-10-24

## Artifact

- APK: `JarVis-Maps-0.1.1.apk`, 50,441,971 bytes.
- Package: `com.teep.jarvismaps`; versionName `0.1.1`; versionCode `2`.
- SHA-256: `324C5BC1D1E4418E48FB66243E89B597A7B6ED892DF5622F6080F993CD88808E`.
- Artifact copy hash matches the Unity build output.
- `apksigner verify`: valid APK v2 signature. Certificate SHA-256 `13163f8a75bbbdfdda62fb60ffbc9f03822b2b22fa6410fd2483339225dda49d`, matching Maps 0.1.0. This is a debug-signed engineering build, not a Play Store release.
- ARM64; Android minimum API 29; target API 36.
- Permissions: Camera, Internet and AndroidX's signature-level dynamic-receiver permission. No microphone or broad storage permission.

## Passed

- Unity compilation: no script errors.
- `ProjectBootstrap.VisualMapReview.Run()`: 30 logic checks. Includes a fixture reproducing displacement under the old transform, preserving camera correspondence after yaw projection, map-origin invariance and independent viewpoint gates.
- `ProjectBootstrap.VisualMapUiReview.Run()`: 226 layout checks, 24 synthetic renders, two portrait aspect ratios. These are not physical camera screenshots.
- Android build: Succeeded; zero build errors; 144.874375 seconds.
- Main Unity scene restored to `Assets/Scenes/JarVisFigmaApp.unity`, not dirty, Play stopped, no compilation running. Main warehouse app ID/version restored to `com.teep.jarviswarehousear` / `0.1.2`.
- Earlier PC module verification remains separate: 19 Python tests; actual PyCOLMAP reconstruction of 28 generated images and hloc localization of a held-out synthetic image; authenticated loopback HTTP success and wrong-map rejection. These synthetic metrics are not real-room accuracy measurements.

## Not passed / not established

- No installation, physical camera test, measured room walk, or Samsung acceptance was performed during this alignment-fix run.
- No original capture ZIP or newly published room map was found in the delivered PC module's local `data` folder. No server responded on local port 8765 at the diagnostic check. This does not establish whether the owner used another PC/server or previously stopped it.
- The source defect is confirmed by the regression fixture; whether it explains all reported displacement is **not confirmed** without the room data and test recording.
- Different-view agreement and reprojection/inlier gates can still accept consistent wrong geometry in a repetitive room. They are not centimeter-accuracy guarantees.
- Keep the same captured map for the first retest. Install over 0.1.0, do not clear app data. If initial alignment remains wrong, provide the original ZIP and a video from before QR A to the first rendered route; measure the offset before walking. Do not compensate by moving the virtual route manually.

No E-drive access, account changes, deployment, database reset, git commit or unrelated cleanup was performed for this fix.
