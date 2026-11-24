# JarVis AR assistant / 0.4.3

Target: existing Roompresent_VPS scene; SmallRoomAR owns camera/session, RoompresentDeliveryHud owns scanning and delivery controls. Add one UGUI assistant owner to the same safe area. Existing app assets and 412×915 logical layout; Android portrait. No scene reload for assistant panels.

Approved scope: fix C verification, separate QR/barcode frames, rescan-A and exit controls (0.4.2), then compact task-aware text/voice assistant, local incident report popup, real audio-reactive JarVis eyes/wave, and an expandable 2D map message after voice playback. Install combined full app only after checks.

Layout: collapsed JarVis pill at upper-left below top toolbar; expanded 346×154 card. Chat/report occupy at most lower half with a single message scroll owner and 48-unit actions. Keyboard lifts/reduces chat panel; camera and AR anchors remain active. Red X cancels recording/audio and closes assistant. No settings icon. During code verification, assistant collapses to leave frame visible.

Map: owner requested a clean rectangular diagram, square B/C markers and no separate 2D button. Show actual saved A/B/C floor access points and only complete NavMesh paths in map-local coordinates, with a metric grid; remove the irregular scan footprint. The surrounding rectangle is a presentation frame, not a surveyed wall. Attach an expandable image in chat for map/tracking help, after voice playback. Never infer current position when tracking is unavailable.

Network: reuse authenticated OMS OpenAI gateway, no bundled key. Explicit mic permission; ≤30 seconds real PCM recording → transcribe → task-context chat → speech. No automatic retry or mission completion by AI. Report saved locally as pending (no incident endpoint currently exists); explicitly label this in UI.

Checks: compile, focused mission regressions, actual NavMesh map, VI/EN screenshots at 1080×2400 and 1080×1920, chat/report/map/recording layouts and long text, no AR disable/reset references in assistant; strict full APK + signature; install once on Samsung; verify installed version and current camera/mic/API behavior separately. Physical tracking and human speech remain pending until observed.
