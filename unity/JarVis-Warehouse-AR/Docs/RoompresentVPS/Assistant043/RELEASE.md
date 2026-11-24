# JarVis 0.4.3 / code 27 — combined AR update

## Changes

- Full JarVis app, including existing login, tasks and AI screens; Roompresent remains map 153613.
- C destination QR no longer waits indefinitely for four successful metric plane measurements: correct QR identity still requires repeated camera decode, trusted tracking and distance <=1.2 m. Metric reference transfer is attempted first; when unavailable, the existing trusted reference is preserved and the popup says only that the QR was verified. Product verification is still a separate second step. A barcode never localizes the phone.
- Square QR frame versus wide product barcode frame, measured progress, QR-success acknowledgement, small two-arrow Scan A, Exit and camera refresh. Existing B/C handoff retains native reference and mission progress.
- Compact JarVis widget and text/voice chat inside the same AR canvas. Task context includes A1/B, A2/C, selected stop, completion and tracking validity. The AI cannot change anchors or confirm deliveries.
- Explicit microphone permission, real <=30s recording, transcription through the existing authenticated gateway, AI answer and speech playback. Red X cancels owned recording/playback and closes the panels. No automatic retries. User language is decided from the actual question before adding task metadata. No key is bundled.
- Waveform uses recorder RMS / AudioSource output samples; eyes smoothly face the waveform and blink. Reduced-motion preference stops gaze/blink motion.
- No dedicated 2D button (owner's final choice). Tracking-help intent attaches a clickable map message after voice playback finishes; the local schematic can still be attached if the network/session is unavailable. The new 384x512 rectangular diagram removes irregular scan protrusions, shows square B/C markers, real A/B/C coordinates, complete baked NavMesh paths and a 0.5m grid. The rectangle is a presentation frame, not surveyed walls or a live user position.
- Incident description popup saves a separate LOCAL_PENDING_NOT_SENT JSON report on the device. It explicitly says that this has not been sent to an administrator; no incident delivery endpoint exists in the current client.

## Evidence

- `Logs/RoompresentVPS/Assistant043`: VI/EN renders at 1080x2400 and 1080x1920, standalone map PNG and result.txt.
- `Logs/RoompresentVPS/Delivery042`: real checked registration, ordered QR/product proof in both delivery orders, fresh two-product gate, native-reference handoff and shelf-side route regressions. These are source/math/UI checks, not physical walking proof.
- Assistant panel open/close checks preserve ARSession enabled state and map transform. Assistant does not call ARSession.Reset, disable AR managers, or change scene/anchors.
- Exact saved A/B/C record and local B-complete/C-pending mission were pulled before update for comparison. Credentials were not exported.

## Device acceptance

Pending until install/observation: actual microphone permission, human speech, live AI/TTS response, Android keyboard, physical C QR decode and walking/recovery. Editor renders and a successful build do not establish these.

APK/hash/build duration and device installation evidence are appended after final packaging.

## Final packaging and installation (2026-11-20)

- StrictMode full APK succeeded, zero errors, 70.1234146 seconds. Final log: workspace tracking-review-20261120/unity-043-no-2d-button.log.
- APK: `D:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse-0.4.3.apk`; 90,403,947 bytes; SHA256 `1B8C4C1624568268C4F46B39AF88D5C93FD706CAA0FCFA0D3298E9C690CE8A19`; APK v2 signature verified.
- ADB replacement install returned Success. Samsung R3CN20R5Q2F reports versionName 0.4.3, versionCode 27. Main app opened and the owner signed in; live camera barcode screen observed.
- Saved QR-A/B/C registration bytes unchanged, SHA256 6889AC8F49C1282725B97151A3F73785E62E0FAEDF1C9A690614B8A6A82EB2B8. Mission bytes unchanged, B delivered / C pending, preferred C.
- No intermediate 0.4.2 or earlier 0.4.3 candidate was installed. Final owner choices included rectangular map and removal of the 2D button.
- Physical C verification, fresh AR alignment, microphone/STT/AI/TTS and keyboard acceptance are still pending owner camera/voice interaction at this point. Do not call them passed from Editor evidence.

Device follow-up: final installed PID24481 logs confirm actual camera A1 freshBoth=False, then A2 freshBoth=True, each awaiting popup OK. This verifies fresh product decoding on 0.4.3. Before AR/chat/mic/C acceptance could be observed, ADB devices became empty; screenshot capture failed because USB disconnected. Do not infer AR or AI success. Owner has been asked to reconnect; the APK is already installed and registration/mission data were preserved.
