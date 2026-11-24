# JarVis 0.4.1 — QR scan feedback and destination reference

## Approved scope

- Keep the accepted task → A1/A2 barcode + OK → fixed QR A → AR → select B/C → destination QR + OK → matching book barcode flow.
- Small QR/barcode frame, measured scan progress and QR success acknowledgement. Compact bottom controls, camera visible.
- Refresh camera means a short autofocus request cycle. It does not disable the AR camera, reset ARSession, erase anchors or task progress.
- The user clarified that QR B/C should establish the current delivery position, not merely validate the destination ID.

## Current device evidence before this release

Samsung R3CN20R5Q2F was connected and authorized. Local trial record: deliveredB=true, deliveredC=false, preferredStop=C. Exact current PID logs showed destination B verified and completed, then next stop C at device time 16:01:22. The app later displayed recovery. The previous implementation changed from auto to fixed focus after B. Logs do not establish that focus alone caused the physical tracking problem.

## Changes

- Keep autofocus enabled after scans and completion; provide a labeled focus refresh button with cooldown and progress feedback.
- Native anchor/session recovery stays active even when the guide is waiting for tracking. Add explicit tracking-reason / floor-route diagnostics.
- Destination QR: decode matching ID, measure three finder centers on the same tracked horizontal plane, require four stable poses over at least 0.8 seconds, create a native anchor at the measured QR, retain checked room orientation and rebase the logical fixed-A frame using the saved B/C coordinates. Reject jumps over 0.75 m. Product scanning remains blocked until QR-success OK.
- A destination measurement updates local position using the previously trusted room heading. It does not infer absolute heading from an arbitrary QR or claim recovery after an ARSession reset. QR A remains required for fresh sessions.
- Delivery completion changes only saved task progress and next stop. B→C and C→B retain spatial reference, checked coordinates and camera.
- Floor routing projects the handset to nearby walkable floor within 0.85 m in Roompresent, accommodating a phone held over a cart during scanning. Route still must be complete on the baked walkable NavMesh; pins are not invented and obstacles are not bypassed by a straight line.
- QR frame only appears while scanning/recovering, with real stabilization counts. Success popup only follows measured A or measured destination verification, not a timer.

## Validation status

Final full-app StrictMode build succeeded with 0 errors in 70.55941 seconds. APK v2 signature verified. Version 0.4.1/code25, 90,348,795 bytes, SHA256 `62F647B937FADCE83A1D7EDCFBBC759C06FAD57EACF9B93E15A278DF9BAD6A8C`.

APK: `D:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse-0.4.1.apk`.

Fresh bilingual VI/EN review results and 1080×2400 / 1080×1920 UI renders are in `Logs/RoompresentVPS/Delivery041/{VI,EN}`. Actual generated scans and QR-success popup were visually inspected. Checks passed: destination-origin correction at both stops and four headings, remaining destination invariance, large jump rejection, preservation of a calibrated reference through B→C/C→B completion, saved task handoff, shelf-side complete NavMesh paths, ordered QR/product verification and popup acknowledgement. No source map, landmark coordinates, backend deployment or OMS inventory was changed.

Final build log: workspace `tracking-review-20261120/unity-041-release.log`. Earlier `unity-041-build-final.log` contains an intermediate compile failure, fixed before this final build. Intermediate APKs with the same version are superseded by the exact final hash above.

Synthetic math/UI tests do not prove physical camera or walking accuracy. Physical native QR destination anchor creation, walking B→C and focus/recovery need current Samsung camera testing.

Saved registration SHA256 must remain 6889AC8F49C1282725B97151A3F73785E62E0FAEDF1C9A690614B8A6A82EB2B8.
