# App Development Operating System — package
## Delivered layout
```text
.agents/skills/
  app-dev-router/
    SKILL.md
    catalog.json
    references/
      skill-map.md
      skill-router.md
      master-pipeline.md
      bootstrap.md
      definition-of-done.md
      project-context-template.md
      continuous-improvement.md
    scripts/
      route.py
      install.py
      validate_system.py
      tests/test_system.py
  app-project-audit/SKILL.md
  app-architecture/SKILL.md
  app-git-safety/SKILL.md
  app-environment-security/SKILL.md
  app-backend-data/SKILL.md
  app-api-integration/SKILL.md
  app-openai-chat/SKILL.md
  app-voice-audio/SKILL.md
  app-unity-app/SKILL.md
  app-figma-ui/SKILL.md
  app-ui-navigation/SKILL.md
  app-root-cause-debug/SKILL.md
  app-testing-regression/SKILL.md
  app-render-deploy/SKILL.md
  app-release-verification/SKILL.md
  app-project-memory/SKILL.md
  app-feature-delivery/SKILL.md
Docs/DevelopmentSystem/
  JARVIS_AUDIT.md
  PROJECT_CONTEXT.md
  PACKAGING.md
  VERIFICATION.md
```

Each skill has purpose, trigger, pre-check, workflow, safety, verification, failure handling and output. Shared detail lives in router references; load selected modules only. Existing .agents skills remain untouched. No global skill installation/config override or recurring automation was created.

## How to reuse
1. Audit the new app's existing instructions/state; don't clone JarVis.
2. Plan copy with your Python runtime:
```powershell
python .agents/skills/app-dev-router/scripts/install.py --target "E:/your-existing-app"
```
3. After authorizing that exact target, add --apply. It refuses any matching skill collision before copying and never overwrites destination AGENTS. Do not blindly reuse the example directory.
4. Copy only the reusable skill tree; current audit/memory remain JarVis-specific.
5. Open a new task/session in the target app if local skill discovery hasn't refreshed, or explicitly provide the router SKILL.md path. Newly written files are not guaranteed auto-injected into an already-running conversation.
6. Invoke: `$app-dev-router Hãy setup app này theo workflow Jarvis. Audit trước, dùng đúng stack hiện có, chọn skill tối thiểu.`
7. Fill/adapt the generic project-context template into the target's canonical memory only after discovery.

## Planning helper
```powershell
python .agents/skills/app-dev-router/scripts/route.py --kind ai --frontend unity
python .agents/skills/app-dev-router/scripts/route.py --kind deploy --backend --provider render
python .agents/skills/app-dev-router/scripts/route.py --kind figma --frontend unity --prototype-first
python .agents/skills/app-dev-router/scripts/route.py --kind bug --frontend unity --voice
python .agents/skills/app-dev-router/scripts/route.py --kind ui --frontend web --level 1
```
Router is deterministic capability planning, not NLP or automatic execution. Codex reads user request/audit to select flags; it can narrow unaffected owners further. Output planningOnly=true and gates do not perform requests, deployment, commits or captures.

## Gates / permissions
Prototype-first waits for approval before production application. Publishing and provider spend/resource upgrades require their own authorization. No whole dirty-tree push, keys/client secrets, quota/account rotation, policy bypass or live DB fixture tests. Built-in/provider specialist skills may be reused when available; these modules don't require copying plugin implementations or assuming tools exist.

## Verification commands
```powershell
python .agents/skills/app-dev-router/scripts/validate_system.py
python .agents/skills/app-dev-router/scripts/tests/test_system.py
```
Also run the installed skill-creator quick_validate.py for each skill if available. Package checks prove frontmatter/references/order/copy behavior, not that a future app is complete. See VERIFICATION.md for actual results and boundaries.
