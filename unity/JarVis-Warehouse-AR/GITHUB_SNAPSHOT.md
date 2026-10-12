# JarVis Unity — source snapshot 2026-10-12

This folder is the existing JarVis Warehouse Unity app, copied from `E:\unity of teep\JarVis-Warehouse-AR`; it is not the separate Survey app. Open this folder directly in Unity Hub using Unity **6000.4.7f1**. Main scene: `Assets/Scenes/JarVisFigmaApp.unity`.

Read `AGENTS.md`, `CODEX_CONTEXT.md` and `UNITY_UI_WORKFLOW.md` before changing code. Source references to `E:\warehouse-ai` mean the root of this GitHub repository on the receiving machine; existing local paths in historical documentation have not been rewritten.

Included: Assets and their metadata, Packages, ProjectSettings, Tools, Docs, workflow skills and root project documentation. Original files were hash-checked while copying. Backend changes in the local working tree are not part of this Unity upload.

Excluded intentionally: Unity Library/Temp/build/log caches, Android APKs, local Figma connection assets that may contain a personal token, original `.figma` design archives and their metadata, recovery scenes, XR temporary assets, databases, credential files, device survey data and the independent Survey project. Original design archives remain on the source disk. Imported runtime graphics/fonts/scenes are included; re-importing Figma requires the separately retained originals and a local connection, not credentials committed here.

This is a source backup, not a newly compiled or device-tested release. No new Unity compile, APK build, live API or AI call was performed for this upload. Existing device/backend integration limitations remain as recorded in project context. Do not run production seeding or expose local backend credentials to restore the project.

Snapshot-specific Git configuration keeps the included small binary assets as ordinary Git blobs (no Git LFS dependency). Ignore exceptions preserve Unity `Scripts/Data` source and skill reference catalogs despite the backend root's `data/` exclusion. Historical source whitespace is preserved; `git diff --check` may report pre-existing trailing whitespace in copied files.
