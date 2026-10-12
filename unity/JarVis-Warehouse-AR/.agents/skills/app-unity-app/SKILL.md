---
name: app-unity-app
description: "Modify an existing Unity app with scoped runtime edits, compile and target-specific verification."
---

# Unity implementation

## Purpose
Modify an existing Unity app with scoped runtime edits, compile and target-specific verification.

## Trigger
Unity scene/runtime/UI/client/build changes; skip for other stacks.

## Pre-check
Read project/version/manifest and current scene/entry. Identify uGUI, UITK or IMGUI owner. Inspect open Editors and dirty scene state; use current installed purpose-built Unity tooling/skills when available.

## Workflow
Prefer live-editor commands when supported; otherwise use existing Editor runners/batch methods without replacing the project. For runtime C# edits, compile rather than reimporting all design assets or rebuilding scenes. Preserve GUID/meta pairs and referenced legacy assets. Use anchored/layout-owned UI, readable TMP, safe area/IME and mobile resource constraints. Run scoped checks in the already-open Editor; batch only when it does not collide.

## Safety Rules
No second Editor on the same project, forced close/lock removal, scene rebuild for logic-only edits, or deletion before reference/GUID audit. Do not install packages or swap fonts without scope authorization.

## Verification
Record fresh compile/Play evidence, actual interaction/render checks and relevant errors. Android build requires configured modules; inspect artifact signature/manifest/ABI. Device permissions/audio/camera still need hardware evidence.

## Failure Handling
Distinguish project-in-use, import, compile, runtime, render and platform errors. Fix one identified owner; preserve unrelated changes and failed logs.

## Output
Scene/owners, files, fresh compile/runtime/build evidence, open-Editor status and device-only TODO.
