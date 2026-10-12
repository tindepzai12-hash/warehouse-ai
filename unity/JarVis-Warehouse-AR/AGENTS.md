# JarVis Warehouse instructions

Read `CODEX_CONTEXT.md` before making changes. Preserve the Android-first scope, keep secrets out of the repository, prefer small reversible edits, and run an appropriate validation after edits. Do not assume the FlutterFlow UI is the source of truth for Unity code.

For UI/app work, follow `UNITY_UI_WORKFLOW.md`: read only the relevant owners, use focused verification, and do not rebuild/import every screen for a runtime C# change. Keep unknown legacy assets until references are audited. Report untested device/API behavior explicitly.

For reusable development-workflow packaging or a request to "setup app theo workflow Jarvis", read `.agents/skills/app-dev-router/SKILL.md` and select only the relevant modules. Keep app-specific memory separate; `Docs/DevelopmentSystem/JARVIS_AUDIT.md` records the extraction evidence, not a replacement for current project context. This workflow does not authorize deployments, billable calls or changes to another app.
