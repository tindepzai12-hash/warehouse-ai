# JarVis Visual Maps 0.1.0

## Revision requested 2026-10-25
- The owner now requires room geometry and a checked walkable route, not localization plus a recorded centerline alone.
- See `REDESIGN-MAP-FIRST.md` for the replacement architecture, implementation prompt and acceptance gates. This original plan is retained as history, not evidence of obstacle detection.
- Status: source audit and replacement design completed; the new capture/geometry/workflow is not implemented or device-verified. Existing 0.1.1 APKs are unchanged.

## Contract and limitations
- User approved COLMAP + hloc and requested an APK plus capture instructions.
- Separate package `com.teep.jarvismaps`; preserve JarVis Warehouse and Routes 0.3.5, their scenes and saved routes.
- Android camera/ARCore captures JPEG, camera intrinsics and same-session camera poses. No video import with guessed intrinsics.
- PC performs sparse COLMAP reconstruction and hloc absolute-pose localization with classical SIFT correspondences. No downloaded neural weights, paid cloud, GPS or Immersal quota.
- Phone and PC must share a trusted LAN; server must be running. This is not an offline-on-phone hloc port.
- No room images exist yet. Device capture and walking accuracy cannot be accepted from synthetic tests or an APK build.

## Implementation plan
1. Validate capture format, metric alignment and coordinate conversions; install isolated CPU dependencies, pin hloc source.
2. Implement authenticated LAN service: upload capture, reconstruct, gate quality, publish one active map, localize query photographs.
3. Create a separate native uGUI AR app: connect by setup QR, capture room, mark floor, record A to B, upload, localize and guide.
4. Gate localization on independent consistent observations. Reject stale, inconsistent or wrong-map responses. Hide guidance after tracking loss; never silently teleport content.
5. Test real COLMAP/hloc on a synthetic photogrammetry scene, math/security failure cases, Unity compile/UI and Android signing. Clearly separate physical-device acceptance.
6. Deliver versioned APK and Vietnamese instructions; retain local capture export for recovery and PC processing.

## UI plan and self-review
- Runtime uGUI, matching inspected Routes ownership, Unity 6000.4.7f1 / AR Foundation 6.4.3. No Figma rebuild.
- Portrait, 412x915 logical reference; safe-area header and bottom vertical action card, camera fills remaining screen.
- Home: server connection status, Ghi bản đồ, Chỉ đường; setup QR and help remain contextual.
- Capture: floor calibration, room coverage photographs, scan A, walk route, scan B, upload. Show valid photo count, not an invented coverage percentage.
- Build: show server stage and registered/total when known. Upload percentage is byte progress only.
- Navigation: QR A triggers map-image localization; show inlier count, explicit retry and stop. At most one nearby arrow and a distinct B destination marker; no arbitrary future turns.
- Agent review: approved for source implementation, not user approval of final screenshots. Empty raster candidate ledger: text-only request, existing brand assets reused.
- No camera images enter logs. No public exposure, firewall changes, administrator operations or OS startup tasks.
