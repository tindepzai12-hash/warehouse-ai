# App Bootstrap

Trigger: “setup app này theo workflow Jarvis” or equivalent. This invokes a workflow, not cloning Jarvis or granting publish/spend permission.

1. Locate explicitly named workspace; read scoped AGENTS and current context. Inspect status/history and determine whether an app already exists. Never initialize a replacement over it.
2. Identify product intent, target platform, stack, entrypoints, owner boundaries, existing infra, partial features and integrations. Unknown values stay unverified.
3. Separate app business/model/palette/accounts from reusable development procedure. Use the current app's chosen stack, not a default Unity/Render/OpenAI trio.
4. Build capability matrix and implementation dependency map. Classify risk Level 1–4 and select minimum skills using skill-router.md or planning helper.
5. Establish scope/acceptance and gates: prototype approval, secrets/access, data migrations, publishing/cost, hardware testing.
6. Adapt project-context-template.md only if no canonical memory exists; otherwise update existing context. Store no credentials. Prepare isolated tests and config name inventory.
7. Implement approved scope only after prerequisites. Test real affected behavior and adjacent regressions; release only when requested/authorized.
8. Report current evidence/limits and a resumable next step. Newly reusable lessons are proposed, not automatically written to global skills.

## Portable installation
Use scripts/install.py --target <existing-new-app-root> to plan; add --apply only for the explicitly authorized target. It copies the 18 app-* skill directories (including shared references/scripts), refuses collisions and path escapes, and does not copy the reference app's Docs context, credentials, code, assets or databases. It does not edit destination AGENTS or install a new app. Review the target's instructions and add a conditional router pointer only if appropriate. Discovery of new local skills may require a new task/session in the client; do not promise immediate automatic activation everywhere.

Once available: `$app-dev-router Hãy setup app này theo workflow Jarvis. Đọc project hiện có, chọn skill tối thiểu và báo prerequisite trước khi sửa.`
