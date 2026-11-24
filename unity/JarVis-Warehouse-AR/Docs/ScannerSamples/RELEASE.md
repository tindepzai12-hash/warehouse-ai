# Samsung scanner delivery / network follow-up

Date: 2026-10-13, Asia/Saigon.

Latest APK: `Builds/Android/JarVis-Warehouse.apk`, also named `JarVis-Warehouse-0.1.2.apk` for unambiguous installation on Samsung. Version 0.1.2, code 3, package `com.teep.jarviswarehousear`. Build succeeded at 13:50; actual bytes 49,685,883; signature verification passed.

SHA256: `0303AC691F423E67196A89F36F5FDEEEA53207CE9E7C6B430ACF22A4D2BCB6FF`.

Samsung Internal storage / Download contains **JarVis-Warehouse-0.1.2.apk**. MTP readback bytes/hash match the latest PC APK. The older `JarVis-Warehouse.apk` on Samsung remains the initial 0.1.1 file; MTP did not overwrite it. Install the versioned 0.1.2 file, not the old unversioned file. No phone file/app/data was deleted.

`Milk-Case-Label.png` is also in Download, 58,468 bytes, SHA256 `CD2AD2F3D6E129580316239524A29241B6F9BB791980CDBE32D868AD935E84C0`, verified by MTP readback. QR payload is `MILK-180ML-CASE24-TEST`, a sample, not a registered production product/GTIN.

Checks: 42 generated-image decoder/input checks, 61 native UI/state checks, one final labelled-QR decode, 12 connection-policy checks; all pass in their stated offline scope. Translation audit: 634 entries, 212 labels, no untranslated labels. Android build and package/signature checks pass. Physical camera, real warehouse lookup and successful phone login remain NOT VERIFIED.

PC public health check was HTTP 200/status ok. Owner reported phone login connection failure; 0.1.2 improves login timeout and actionable DNS/TLS/timeout/protocol feedback, but the actual phone cause is not yet established. User should update from the versioned APK and report the new message or phone browser health result. ADB diagnostic interface is missing its Windows driver (PnP code 28); MTP file transfer works, but installation/launch/logcat have not been verified by the agent.

Git: no commit/push performed for this scanner/network change yet. Main Unity checkout has no remote; the existing GitHub export is in `E:\warehouse-ai\unity\JarVis-Warehouse-AR`. Preserve unrelated backend changes and synchronize only reviewed Unity source/docs when resuming publication. APK, private map, keystore, tokens and device readback logs must not be published. Do not present the production-ready/device acceptance checklist as complete.

Full implementation/limitations: `Docs/JARVIS_ANDROID_SCANNER_IMPLEMENTATION.md`.
