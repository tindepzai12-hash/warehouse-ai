# Definition of Done

Select requirements BEFORE implementation; record PASS/FAIL/SKIPPED/BLOCKED and evidence per row. SKIPPED means not applicable with reason, never missing proof.

| Condition | Minimum evidence |
|---|---|
| User scope/approval | Acceptance + prototype approval when requested |
| Owner/code | Minimal reviewed diff; existing work preserved |
| Compile/build | Current affected runtime/compiler success, or truly not applicable docs-only |
| Feature reachable | Actual navigation/action path, not a drawn button |
| Behavior | Intended outcome with real applicable boundary |
| API/auth | Actual route/client contract and unauthorized/forbidden/errors |
| UI | Render plus interaction, target bounds/theme/locale/keyboard as applicable |
| Errors/resources | Cancel/error/empty/retry, request/audio disposal, user work retained |
| Logs | No relevant critical errors; unrelated failures disclosed |
| Regression | Old affected behavior still works, tested in isolation |
| Artifact/release | Revision/hash/signature/config + current deployment ID if requested |
| Hardware/provider | Human mic/audible playback/camera/model evaluation when requirement demands it |
| Operations | Persistence/retention/backup/recovery/observability for production data |
| Handoff | Changed files, exact test layer, remaining TODO, artifact location |

## Evidence ladder
1. Static/source/config audit.
2. Compile/syntax/schema.
3. Unit/component/fixture interaction.
4. Actual client↔isolated HTTP/provider stub integration.
5. Approved live provider/service smoke.
6. Target hardware and human acceptance.
7. Release/operational acceptance with recovery evidence.

Each proves only its layer. Generated PCM + AudioSource.time advancement does not prove real mic input, recognized human speech or audible output. Health proves availability, not new routes/provider access. Stale outputs must retain dates and input revision.

DONE: all applicable mandatory requirements satisfied. PARTIAL: useful work delivered, required proof/work remains. BLOCKED: next required action cannot proceed without named access/input/state change. Don't call every local draft production-ready.

## User report
TASK: what scope.
SKILLS USED: actual subset, not all skills in bundle.
FOUND: supported facts; separate hypothesis.
ACTION: own changed files/actions.
VERIFY: commands/results/time/layer, failures and skipped checks.
STATUS: DONE / PARTIAL / BLOCKED for this task only.
NEXT: precise required work, optional for small tasks.
