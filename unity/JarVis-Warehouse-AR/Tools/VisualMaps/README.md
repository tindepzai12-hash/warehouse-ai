# JarVis Maps — COLMAP + hloc room prototype

## Current: 0.2.0 / code 3

Use `HUONG-DAN-0.2.md` and `MASTER-PLAN-0.2.md` for the current multi-map capture workflow. The 0.1.1 instructions below are historical, not the current home screen or navigation acceptance.

0.2.0 adds named offline captures, persistent checkpoints, multiple PC maps/revisions, ZIP import, RGB capture with optional synchronized raw depth/confidence, PC surface previews, and real NanoDet 2D object inference. Capture starts without QR, PC connection or a measured floor. Contextual Vietnamese hints use sampled image brightness, motion, ARCore tracking reasons, and nearby saved view directions. They do not claim measured room coverage.

This release is a **capture/analysis test build**, not completed obstacle-aware navigation. Generated geometry remains `navigationAllowed=false`: floor evidence is not full-body clearance, arbitrary objects are not all detectable, and physical room calibration has not passed. Do not bypass this gate by editing JSON to make the button work. Existing 0.1.1 APKs and captures remain untouched.

PC: `START-PC.cmd` in this ready environment, or run `SETUP-PC.ps1` then `SETUP-OBJECTS.ps1` in a new checkout. Visit the local Maps Studio page to import ZIPs, inspect reports and rename maps. `IMPORT-MAP.py` is a standalone fallback; stop the server first to avoid two writers. Object weights/wrapper are pinned and verified, Apache-2.0, CPU inference; no Torch or CUDA needed.

Current focused checks: `python -m pytest test_visualmaps.py test_workbench.py -q`, `http_review.py`, `ProjectBootstrap.VisualMapReview.Run()`, `ProjectBootstrap.VisualMapUiReview.Run()`. APK output is `Builds/Android/JarVis-Maps-0.2.0.apk`. See `QA-0.2.md` for measured evidence and unverified hardware behavior.

## Historical 0.1.1 notes

Read `HUONG-DAN.md` for Vietnamese capture and navigation steps. This is a separate Android package (`com.teep.jarvismaps`, version 0.1.1/code 2), not an update that overwrites JarVis Routes or the warehouse app. Install over Maps 0.1.0 without uninstalling to keep local data.

## Architecture

Android records grayscale JPEGs, CPU-camera intrinsics, frame timestamps, AR camera poses, a measured floor and one A-to-B walk in the same AR session. QR A/B select the route endpoints; QR decoding alone does not provide a world pose. Do not import arbitrary phone videos in place of the capture bundle.

The PC validates the bundle, reconstructs sparse landmarks with PyCOLMAP, and aligns camera centers to the captured metric coordinate system. A constant raw-sensor-to-portrait-camera rotation is estimated and checked. Only a sufficiently reconstructed and aligned map is published. One map is active; previous capture folders are retained rather than deleted automatically.

Query images use COLMAP SIFT, FLANN 2D–3D matching and the official `hloc.localize_sfm.QueryLocalizer` absolute-pose estimator. This first version does **not** include SuperPoint, LightGlue, neural retrieval, LiDAR meshing, or hloc processing on Android. Images are processed on the trusted LAN PC; the PC must remain on during localization/navigation.

The app accepts only fresh, quality-gated image poses. Two agreeing estimates from different frames and viewpoints are required before creating an AR anchor and showing one cyan arrow and a blue B marker. Version 0.1.1 recomputes translation after gravity-aligned yaw projection, preserving the camera correspondence instead of shifting the entire route. Gravity discrepancies over 3 degrees are rejected. These engineering gates are not measured room-accuracy guarantees. Tracking loss, large correction, stale localization, or leaving the recorded path hides guidance. This is not obstacle avoidance or a certified safety system.

## Run and reproduce

- Windows Python 3.12 + Git: `SETUP-PC.ps1 -Python <python.exe>`, then `START-PC.cmd`. Dependencies remain local in `.venv`; hloc is checked out at the pinned commit in the setup script. No system Python, GPU driver, or firewall settings are changed automatically.
- Open `http://127.0.0.1:8765` locally. Scan its private connection QR in the app. Allow the Python app on a trusted Private network only if Windows asks. The setup page/token must not be published.
- HTTP on the private LAN is **not encrypted**. Never forward port 8765 to the internet. Tokens, maps, images and local environments are ignored by `.gitignore`.
- Stop with Ctrl+C, or `STOP-PC.ps1` from this folder. No Windows startup task is installed.
- Run `python -X utf8 -m pytest test_visualmaps.py -q` from this folder. `synthetic_review.py` reconstructs a generated scene and localizes a held-out image; `http_review.py` tests the same model over an isolated loopback server. Synthetic accuracy is not a claim about the user's room.

## Unity

Runtime: `Assets/JarVis/Scripts/VisualMaps/`. Scene: `Assets/Scenes/JarVisVisualMaps.unity`. Build from **Tools → JarVis → Visual Maps → Build Android APK**. Android SDK/NDK modules are required. Output: `Builds/Android/JarVis-Maps-0.1.1.apk`.

`ProjectBootstrap.VisualMapReview.Run()` checks pure logic. `ProjectBootstrap.VisualMapUiReview.Run()` checks and renders 12 UI states at two portrait aspect ratios. Editor rendering is not a substitute for device camera/ARCore tests. When invoking a long build through the Unity Pipeline CLI, use a detached job to avoid its synchronous main-thread timeout.

The ready-to-run environment on the current PC is alongside the delivered APK in the Codex artifact folder. Source copied into `Tools/VisualMaps` deliberately excludes that environment, vendor checkout, private data and generated test images to avoid duplicating large files. Run setup if using a new copy.

## Limits and evidence

Use the same supported Android phone for capture and navigation, in portrait, with similar lighting. Capture 80–150 overlapping views from varied positions before recording the actual walk. Maps with poor coverage, missing A/B images, near-collinear calibration, inconsistent camera parameters or high alignment error are rejected.

Physical Samsung capture, real-room reconstruction, three repeat walks, floor alignment, turn direction and destination error must still be measured. Until those pass, this is an engineering test APK, not a completed deployment.

Upstream: [COLMAP](https://github.com/colmap/colmap) (BSD; review dependency licenses), [hloc](https://github.com/cvg/Hierarchical-Localization) (Apache-2.0). Keep upstream licenses when distributing dependencies. No credentials or server login passwords belong in this folder.
