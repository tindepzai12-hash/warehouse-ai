# JarVis 0.3.9 — fresh barcode scans, passive fixed-A alignment

## Scope

Full app entry remains JarVisFigmaApp, with Roompresent_VPS and existing other scenes. Owner selected automatic recognition of fixed QR A in the background. Product barcodes verify books only; no assumption that a barcode gives spatial pose, no camera-at-A fallback and no new AI/provider calls.

The Samsung export `exports/Roompresent-ABC-20261120/roompresent-fixed-a-v1.json` has SHA256 `6889ac8f49c1282725b97151a3f73785e62e0faedf1c9a690614b8a6a82eb2b8`. It is bundled as Resources/RoompresentCheckedRegistration.json. Existing local installer records take priority. Both sources require the existing exact map/scan fingerprints, source landmarks, QR identity and operator-checked fit. The installer remains available in Map; it is removed from normal guidance.

## Normal flow

Start/accept mission → existing barcode camera screen → scan A1 4714551570610 and A2 4714551570603 → automatically open AR. A new in-memory barcode gate is independent of saved task flags. Restored scannedA1/scannedA2 do NOT bypass the scanner. Every new visit resets this gate; duplicate A1, manual input or location QR cannot substitute for the second book. All normal launcher entry points enforce this gate; explicit installer mode remains separate. Saved delivery progress and A/B/C registration are preserved.

The existing metric QR decoder measures fixed A automatically when it is visible in AR. Native anchor creation and trusted tracking remain mandatory before arrows appear. QR A and its surrounding tabletop must still be visible briefly. There is no A confirmation button or B/C setup dialog.

The shortest complete B/C order is selected on the floor NavMesh. One active leg is rendered. At a destination the correct QR and trusted tracking within the existing distance limit are required. Completion of the first delivery advances to the remaining stop. Both delivery orders are supported; product IDs and destination IDs are unchanged. Final owner correction removes ALL normal AR buttons and text panels: camera + B/C pins + active arrows only. Android Back returns to JarVis through the existing launcher.

Temporary tracking loss hides misleading arrows, retains the mission and anchor, and continues recovery. No arrows are claimed before an actual fix. A genuinely lost coordinate frame still requires reacquiring the fixed marker.

## UI repair record

Unity 6000.4.7f1, existing uGUI runtime owner SmallRoomAR. Reference: owner's live Samsung AR screenshot and requested open camera view. Repair only; existing font, pin/path assets, colors and PrototypeUi primitives reused. No generated art or shared theme/prefab changes.

Final hierarchy: existing spatial map → B/C pins + active route; camera provides the live background. The existing controller's text-state references are kept on an inactive, non-rendered root to avoid changing mission logic. There are no AR overlay buttons or panels. Installer remains separately accessible through Map. No raster assets split/promoted. Primary screenshot evidence before edits: samsung-before-038.png. Editor selection is not applicable to this batch source repair; saved Roompresent_VPS is the verification scene.

Focused review exercises actual exported registration and mission state transitions (both barcode order, wrong product/destination, missing tracking, too far, duplicate delivery, both B→C and C→B completion). Final review asserts zero overlay buttons and no active text panels, and renders the empty UI-only overlay in VI/EN at 1080×2400 and 1080×1920. Earlier Passive038 screenshots with controls/cards and scanned geometry are superseded; Passive038Final is authoritative. Neither Editor rendering nor a hidden UI establishes physical Android localization.

## Build/device evidence

Final r3 build succeeded, StrictMode, zero errors, 81.8760317 seconds. APK v2 signature verified. APK: `Builds/Android/JarVis-Warehouse-0.3.8.apk`, version 0.3.8/code22, 90,324,571 bytes, SHA256 `5BE260BF91F5F35CC6F90E7AE80071DA10B220B274FC472E8E2C06DEFE5A17B4`.

Final build log: workspace tracking-review-20261120/unity-038-build-r3.log. r2 is superseded: owner subsequently requested no visible controls or card. r1 failed only the review font lookup before build; corrected to the existing BeVietnamPro Figma SDF font.

0.3.8 installed successfully on Samsung R3CN20R5Q2F and reported versionCode22/versionName0.3.8. Actual AR had no overlay; A/B/C registration SHA256 unchanged. Camera screenshots via ADB were black, so they are not proof of live camera failure or spatial accuracy. The user identified a retained-progress bypass of product scanning. 0.3.9 fixes that entry bug.

Final 0.3.9/code23 build succeeded, StrictMode, zero errors, 79.9606939 seconds; APK v2 signature verified. APK `Builds/Android/JarVis-Warehouse-0.3.9.apk`, 90,323,419 bytes, SHA256 `7D0244529E7E8BAA232CF825D306A15F18ADB624C9FFA1CAD878F4E5CC8BDE19`. Log: workspace `tracking-review-20261120/unity-039-build.log`. Focused review result `Logs/RoompresentVPS/Passive039Final/result.txt` passes the fresh-session gate and restored-task bypass regression, manual/QR/duplicate rejection, both delivery orders and zero overlay controls/text at two aspect ratios.

ADB install -r returned Success; actual Samsung reports versionCode23/versionName0.3.9. Fresh screenshot `tracking-review-20261120/samsung-039-start.png` shows the existing barcode camera screen and the A1/A2 mission notice, NOT AR. This verifies scanner entry despite persisted scanned flags. Waiting on the owner to perform both real scans and walk the route. No physical walking, recovery or AI voice/image claim follows from Editor checks. Status: implemented; actual two-book-to-AR and physical acceptance pending.

## Actual 0.3.9 camera verification follow-up

Samsung process 8117 log: 15:24:49 A2 freshBoth=False; 15:24:51 duplicate A2 freshBoth=False; 15:24:55 A1 freshBoth=True. This confirms real product scans and no AR bypass from one/duplicate product. At 15:25:18 fixed A QR decoded; 15:25:20 metric AR-plane anchor created, calibrated=True, payload JWM:MAIN:E; tracking recovered; 15:25:21 optimal next stop B. These are device clock times. The real two-book → AR → passive A anchor flow has now been observed. User visual alignment, walking, destination completion and recovery during motion remain pending. samsung-039-ar.png is a black ADB capture and cannot establish that the physical camera view is black or that pins are visually aligned. No overlay controls/panels appear in that capture.
