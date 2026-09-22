# Warehouse AI Assistant Implementation Specification

Version: 1.1 — 2026-09-22  
Language: English, Vietnamese, Chinese
Target: Codex implementation agent  
Status: User-authorized functional scope; implementation has not started through this document.

Self-contained handoff: this Markdown incorporates the project-relevant guidance from all three professor-provided documents. Their filenames below record provenance only; the original attachments are not required to implement this specification. Do not ask the user to attach them again. Section 15 provides the consolidated integration details, while Sections 3, 9, 10, and 11 contain the shared architecture and constraints.

## 1. Purpose and source precedence

Build an Android Unity warehouse assistant with an OpenAI-backed, context-aware AI service hosted on Render. Develop and validate the AI service first, then integrate it into Unity. Deliver working operational workflows, not only scripted answers or visual mockups.

Resolve requirements in this order:

1. The user's latest clarification and revised five scenarios from 2026-09-22.
2. `file markdown for Codex.docx`, the latest warehouse requirements source.
3. The professor's `Unity_AI_Feedback_AR_Warehouse_Assistant_Teaching_Guide_English.docx` for the architecture and division of responsibilities.
4. The professor's `AIIntegration_English.docx` and `Unity_AI_Feedback_Systems.pdf` for integration and teaching patterns.
5. Earlier project documents only for compatible Unity, Android, application, and interaction details retained explicitly below.

The latest document replaces conflicting or duplicate older warehouse functions. In particular, the earlier restriction to simple put-away/picking and basic FAQ-only support is superseded. Do not require the user to answer the original forty-question questionnaire again.

The user explicitly selected implementation of the new operational functions. AI-first is the development sequence, not permission to remove cycle counting or exception workflows from the committed scope.

This specification separates confirmed product requirements, user-supplied warehouse policy, and proposed engineering defaults. Do not present proposed defaults as instructions from the professor.

## 2. Confirmed application context retained from earlier documents

- Unity version reported by the user: Unity 6.4, `6000.4.7f1`. Verify the installed Editor before making compatibility changes; do not silently migrate versions.
- Target platform: Android phone/tablet.
- Developer: one beginner Unity developer, approximately three-month project duration.
- Users: new warehouse staff and experienced workers; staff, supervisor/manager, and ICQA roles are relevant.
- Initial evaluation may use one worker, but records and access must retain user and role ownership.
- Keep staff login, assigned tasks, progress, camera scanning, manual code input, text/voice AI, and later AR navigation.
- User-selected languages: Vietnamese, English, and Chinese. Do not invent a Chinese regional variant; make locale configurable.
- Avatar presentation is desired later, with configurable appearance and voice. Allow introduction skipping and avoid repetitive interruptions.
- AI can receive an intentionally submitted image for a relevant question. Continuous video upload is not part of the design.
- Navigation supports a 2D fallback and eventual continuous AR directions to configured locations. A scanned map is future input, not an already solved positioning system.
- The previous U-flow layout may be retained as a configurable initial layout: Inbound bottom-left, Outbound top-left, Fulfillment center, storage on the right. Do not invent dimensions or real coordinates.
- On network loss, pause operations requiring server confirmation and resume after synchronization. Do not silently claim inventory changes succeeded offline.
- On AR tracking loss, show the 2D map and permit relocalization. Show an unknown/stale position explicitly if current position is unavailable.
- An information/news screen is a secondary retained application feature, not a prerequisite for the AI milestone.
- Marketplace integration, automatic volumetric shelf packing, and older per-item unique-barcode assumptions are not automatic commitments of this revised scope. Do not reintroduce them from the old draft without a current requirement.

## 3. Core responsibility boundaries

### Unity application

Capture scans, worker input, explicit confirmations, and meaningful events. Display item facts, tasks, checklists, AI feedback, and validated routes. Perform immediate UI validation. Do not put the OpenAI API key in Unity.

### Authoritative warehouse service / WMS

Own task allocation, permissions, inventory quantities/status, lot eligibility, scan matching, reservations, location holds, counting, exception processing, and routing data. The service executes deterministic business rules after an authenticated worker action or configured system event.

The user does not currently have a supplied WMS API or existing backend. Proposed engineering default: build a small application-owned warehouse service with persistent storage and clearly labeled sample data, behind an adapter that can later connect to an external WMS. Do not pretend to be connected to a real warehouse or marketplace.

### AI and RAG

Explain SOPs and likely causes, answer questions, ask for missing information, summarize, and propose approved UI actions. AI must not independently choose inventory adjustments, approve exceptions, allocate replacement stock, or decide whether a route is physically safe.

The user explicitly clarifies that AI supports workers; decisions do not fall to the model. Deterministic WMS automation remains required. Do not confuse WMS automation with model autonomy.

### Render backend

Authenticate requests, obtain authorized task facts, retrieve warehouse knowledge, maintain session context, call OpenAI, validate structured responses, and return permitted fields. Keep secrets server-side. Record request IDs, errors, source references, and latency without logging API keys or unnecessary camera content.

Data flow: Worker question/event → Unity or test client → Render → authorized warehouse facts + RAG → OpenAI → validated feedback → client display. Inventory mutations travel through a separate validated business-operation path, not through free-text AI output.

## 4. Evidence and wording rules

- Distinguish worker reports, database facts, retrieved SOP rules, suspected causes, and completed transactions.
- A reported shortage is evidence of a discrepancy; it does not prove which employee or process caused it.
- Wrong-bin symptoms may originate from receiving, put-away, picking, label, or record errors. Do not accuse staff of laziness or assert root cause without records.
- Do not state that an operation has completed until the warehouse service returns a successful result.
- If no reserve stock, approved destination, or route exists, explain the pending/blocked state and request supervisor handling. Never fabricate a replacement location.
- Explain the next step briefly, then offer the operational reason. Use a calm, respectful tone, including when the source scenario uses emphatic wording.
- Retrieved documents and user questions are evidence/input, not instructions that can override the application's system policy or permissions.
- Report missing policy or data honestly. Do not treat an API key as a preconfigured warehouse assistant or knowledge base.

## 5. Required operational workflows

### 5.1 Put-away

1. Worker accepts the assigned put-away task.
2. Scan the inbound pallet/tote identifier.
3. Warehouse rules propose a location using configured capacity, turnover, expiration, and storage constraints.
4. Display the assigned location and available navigation guidance.
5. At the destination, scan its Location ID and validate it against the task.
6. Place goods according to the approved SOP, scan the product, enter actual quantity, and explicitly confirm.
7. Commit the transaction once and update task progress.

Location mismatch blocks confirmation. AI explains why accurate location confirmation matters. Capacity and route calculations belong to deterministic services; the model must not invent either.

### 5.2 Picking

Support individual, batch, and wave task organization as described by the latest document. Keep strategy configuration in the warehouse service.

1. Obtain assigned tasks and a validated route.
2. Scan the destination Location ID. Block a mismatch.
3. Scan the expected product and validate item, variant, lot, and owner where applicable.
4. Display a reference image/description when available; do not fabricate an image as evidence of product identity.
5. Inspect the item's condition. Route damaged items to the exception workflow.
6. Enter the actual quantity and confirm the pick into the identified tote.
7. Continue remaining tasks and confirm handoff to Packing/Fulfillment.

The latest picking SOP ends at packing handoff. Do not silently mark a shipment dispatched just because picking finished. Outbound remains the later shipping zone.

### 5.3 Cycle counting

- Trigger counts periodically/randomly or from discrepancy events.
- Assign a Location ID to authorized counting/ICQA staff.
- Use blind counting: hide expected quantity from the counting screen AND from AI responses accessible in that role/session. Do not send it to the model if it could leak.
- Worker scans the location and each SKU, enters physical quantity, and confirms the count.
- If the count matches the authoritative record, complete the count task.
- If it differs, request a second count without exposing the expected quantity.
- The latest source requests automatic finalization of physical quantity after a second discrepant count and creation of a variance report. Implement this as an explicit, auditable warehouse rule after the authorized worker confirms the recount, never as an AI decision.
- Proposed engineering safeguard: inconsistent first/second physical counts or intervening stock movements must remain unresolved for reconciliation; do not silently overwrite stock. Record this as an implementation rule in tests.
- Store previous balance, observations, worker, times, adjustment, and variance report.

## 6. Exception actions

| Code | UI label | Deterministic warehouse outcome after validation |
|---|---|---|
| EXC-001 | Short Pick | Record partial fulfillment and shortage; create a remainder task; look for eligible reserve stock; hold the source location for investigation; create an urgent ICQA count. |
| EXC-002 | Damaged | Move the identified affected quantity from Available to Quality Hold; exclude it from eligible picking; seek an eligible replacement. |
| EXC-003 | Wrong Bin | Suspend/skip the affected pick; hold the location for investigation; create an ICQA inspection; allocate another verified location if available. |
| EXC-004 | Skip / Pass | Record a blocked location/path; recalculate a route that excludes the blocked area; revisit only after an explicit clearance event. |
| EXC-005 | Relabel | Record label failure and the identifiable context; place the item in a pending-identification/relabel workflow; notify Inbound; link a shortage if fulfillment is affected. |

All operations require validated IDs, role permissions, and explicit worker submission. UI proposals from AI may prepare a form but cannot submit operational mutations automatically.

### Quality Hold normalization

- Inventory status name: `Quality Hold`; suggested machine enum: `quality_hold`.
- `Damaged` remains the exception/button label and reason, not the canonical inventory status.
- Replace every source use of “phế phẩm,” “scrap,” or “Damaged status” in this workflow with `Quality Hold`.
- A quality hold is not a disposal decision and does not authorize destroying goods.
- Record the affected quantity separately from unaffected units of the same SKU/lot.

## 7. Five revised AI scenarios

These scenarios replace the older examples. They define expected assistance and operational effects, not claims that those effects have already occurred.

### S1 — Short Pick: five sunscreen boxes required, three present

Worker: “The app asks for five sunscreen boxes, but only three are on the shelf. What should I do?”

Explain that an unreported discrepancy can leave the order and records inconsistent. Receiving, put-away, or prior picking errors are possible causes, not established findings.

Guidance:

1. Scan and record the three eligible boxes actually picked; enter actual quantity 3.
2. Open Exception → Short Pick and submit the shortage.
3. Explain that the WMS will process the two-unit remainder, search for eligible reserve stock, and create the source-location hold and urgent ICQA count according to configured rules.
4. Show a replacement route only after a valid allocation and route are returned.
5. If no replacement exists, show the remaining shortage and supervisor/pending workflow.

Do not tell the worker “you can now follow the updated route” before receiving it. Ensure retries cannot double-count the three picked boxes.

### S2 — Damaged perfume bottle in a temperature-controlled zone

Worker: “A perfume bottle in the cool storage area has a broken cap and is leaking. Should I take it for packing?”

Guidance:

1. Do not place it with normal picked goods or confirm it as an available pick.
2. Open Exception → Damaged. In this scenario the worker must not perform the normal pick scan of the damaged product.
3. Use trusted current task/location/lot context plus affected quantity to identify the stock. If this is ambiguous, request authorized identification instead of guessing or changing all stock for the SKU.
4. After confirmed processing, report `Available → Quality Hold` and replacement allocation only if successful.
5. Contact the Team Lead and follow the site's spill-response procedure. The source mentions an appropriate spill tray/Trouble Basket; treat physical handling as conditional on authorized training, equipment, and site procedure. Do not direct an untrained worker to handle leaking material.

The source's assertions that perfume leakage necessarily disrupts temperature/humidity sensors or the “microclimate” are not established incident facts. Do not repeat them as confirmed causal explanations.

### S3 — Faded clothing barcode and manual input

Worker: “The barcode on this T-shirt is faded. Can I type the digits below it?”

Guidance:

1. Permit the manual input path for a readable, known identifier.
2. Require cross-checking the actual size, color, and brand against the retrieved product description and reference image where available.
3. Validate the entered identifier against the current task and authorized product records. A barcode value and an internal SKU may differ; resolve them through configured mappings.
4. Do not guess missing or unreadable digits.
5. If identity cannot be verified, use Relabel and the pending-identification/Trouble workflow, notify Inbound, and report the resulting shortage if applicable.
6. Link Relabel and Short Pick so the same unavailable unit is not deducted twice.

Manual entry does not prove physical identity. Do not treat an AI image interpretation as final inventory verification.

### S4 — A01 expects a 55-inch television but contains 65-inch televisions

Worker: “The app sends me to A01 for a 55-inch TV, but I only see 65-inch TVs. Should I search nearby?”

Guidance:

1. Do not substitute the wrong variant or pick from another location without an authorized task.
2. Open Exception → Wrong Bin and report the observed mismatch.
3. WMS suspends/skips the task, creates an ICQA investigation, and applies the configured location hold.
4. Navigate to another location only after the service returns an eligible allocation and approved route.

Do not assert that a previous worker was negligent, that the app has physically lost the worker, or that an alternative route is automatically safe merely because it is different. No alternate destination is invented if none exists.

### S5 — Broken pallet obstructs an FMCG aisle

Worker: “A broken wooden pallet blocks the aisle and my tote cart cannot pass.”

Guidance:

1. Stop before entering the obstruction. Position the cart only where site rules permit and without blocking pedestrian routes, intersections, or visibility.
2. Notify the shift supervisor or maintenance through available communication channels. A radio can be external; do not claim an in-app calling feature exists before implementation.
3. Do not encourage untrained manual removal or stepping over the pallet. Authorized personnel decide how the obstruction is cleared.
4. Submit Skip / Pass with the obstruction location. WMS marks the affected path/location blocked and seeks an approved alternative route.
5. Resume only if a valid alternative is available. Productivity must not override a stop condition.
6. Return later only after the service records a clearance event from an authorized source. Elapsed time alone is not proof of clearance.

## 8. Knowledge topics and site rules

### FIFO and FEFO

- FIFO prioritizes configured receipt/entry date for the relevant stock categories, including apparel and appliances/electronics in this project.
- FEFO prioritizes expiration for relevant date-controlled categories, including cosmetics and food.
- WMS selects the eligible lot; AI explains that selection using supplied dates and the applicable policy.
- A wrong-lot scan is rejected by deterministic validation, not by an AI judgment.
- Parse lot/expiry from supported GS1 data when actually present. Otherwise obtain them from trusted records; never claim an arbitrary product barcode contains them.
- Do not infer pick priority from “front” or “deep inside” placement alone.

### Functional zones

- Electronics: configured additional authentication/scan checks.
- Heavy/Bulky: pedestrian route discipline, vehicle separation, and heavy-item handling rules.
- Apparel: explicit size/color/brand verification; do not infer variant through packaging alone.
- FMCG: report spill hazards and apply site emergency procedures.
- Temperature-controlled storage: door discipline, configured environmental requirements, applicable FEFO handling.

### 5S

Teach Sort, Set in Order, Shine, Standardize, and Sustain using the user's scan discipline, orderly locations, clean camera/labels, and clear work areas. Present these as the project's operational interpretation, not a claim that 5S universally equals a database governance standard.

### User-supplied numeric safety settings and source status

The user requested retaining the following values and described them as general rules found online. No named applicable standard, equipment manual, or site-approved source was provided. Preserve them as user-supplied configuration, not verified law or universally applicable guidance:

| Setting | Supplied value |
|---|---|
| Pedestrian separation from forklift | At least 3 m |
| Loaded forklift speed cap | 10 km/h |
| Unloaded forklift speed cap | 20 km/h |
| Fork height when traveling on level ground | No more than 20 cm |
| Goods protrusion beyond shelf edge | No more than 5 cm |

Keep provenance fields such as `source_status: user_supplied_unverified` and `site_approved: false`. Do not silently convert the user's acceptance into external verification. These values may appear in labeled project/test content, but must not authorize real vehicle operation or substitute for applicable site/equipment rules.

The source also says to travel forward uphill and reverse downhill. Retain it as an unverified source statement, not an unconditional instruction: load, equipment, grade, and applicable procedures matter. Restrict operational vehicle guidance to approved materials and authorized operators.

An implementation agent may research authoritative sources to validate safety claims, but must identify jurisdiction and applicability rather than invent a universal rule. This does not block software development with labeled fixtures.

## 9. AI knowledge and behavior design

Separate three assets:

1. This implementation specification for Codex.
2. Runtime SOP/FAQ/scenario documents for retrieval, with stable IDs, version, source, and approval metadata.
3. Live warehouse facts accessed through the authorized service.

Do not index this entire specification as operational truth. It includes engineering proposals, test fixtures, and source caveats.

RAG answers must link to actual retrieved document/section identifiers. Do not fabricate citations. Missing source coverage leads to an explicit limitation or a clarifying question. A question about unsupported topics receives a brief scope explanation in the selected language.

Create a continuous assistance session scoped to authenticated worker/task. Carry relevant confirmed context and previous advice across turns, but refresh changing inventory facts. Never expose another worker's or owner's restricted data.

Trigger AI on user requests and configured events such as repeated scan failure, unresolved mismatch, task completion, or help request. Thresholds and cooldowns are configurable; the professor's examples are not fixed warehouse policy. Keep immediate mismatch errors and camera processing outside the model path.

Permitted AI-proposed UI actions include `none`, `show_station_intro`, `show_item_details`, `highlight_storage_location`, `show_navigation_route`, `request_rescan`, `show_picking_checklist`, `explain_sop_step`, `show_safety_warning`, `open_exception_report`, and `show_task_summary`.

Validate action name, target ID, permissions, and current state. Opening a prepared report is separate from submitting it. Never execute arbitrary generated code or unrestricted commands.

## 10. Proposed implementation contracts

These are engineering defaults for Codex, not pre-existing services. Implement or refine them while preserving semantics.

### API

- `GET /api/health`: service health; never expose secrets. Health does not imply a successful paid OpenAI request.
- `POST /api/feedback`: authenticated question/event → grounded structured feedback.
- Separate authenticated warehouse endpoints for tasks, scan validation, confirmed picks/put-away, exception submissions, counts, and clearances.
- Validate request size/type; enforce rate and usage limits; set timeouts and bounded retries.
- Protect the public feedback endpoint from unauthenticated paid calls. Do not bundle a privileged shared secret in a production Unity build.

Example feedback request using synthetic data:

```json
{
  "sessionId": "test-session-001",
  "eventType": "user_question",
  "taskId": "TEST-PICK-001",
  "language": "en",
  "question": "Five boxes are requested, but I only see three. What should I do?",
  "observation": {"reportedVisibleQuantity": 3}
}
```

Derive user identity and role from authentication. Fetch task facts server-side; do not trust a client-supplied role, quantity balance, or location authorization.

Example feedback shape:

```json
{
  "message": "Record the actual eligible quantity and open Short Pick to report the shortage.",
  "sources": [{"documentId": "SOP-SHORT-PICK", "sectionId": "reporting"}],
  "action": {"type": "open_exception_report", "exceptionCode": "EXC-001"},
  "needsClarification": false,
  "clarifyingQuestion": null
}
```

Actual sources must exist in retrieval results. Validate output server-side and again for Unity action execution.

### Data and persistence

Use persistent, transactional storage for users/roles, products/variants, barcode mappings, owners, lots/dates, locations/capacities, stock balances/status, totes, tasks/lines, reservations, movements, exceptions, counts, route blocks/clearances, knowledge metadata, and assistance sessions.

Use idempotency keys for business mutations so retries cannot duplicate picks, holds, shortage reports, or adjustments. An item scan count alone is not a serial-number identity system. Do not assume all units have unique manufacturer barcodes.

For counting consistency and concurrent operations, validate stock version/reservations within transactions. Preserve an audit trail for automatic WMS changes and supervisor edits. Do not store production inventory only in process memory or an ephemeral filesystem.

## 11. Delivery sequence

### Milestone A — AI service foundation

- Create a small backend and browser test interface, using a proposed Python/FastAPI stack unless existing project code requires another choice.
- Add health, validated feedback requests, mock responses, then real OpenAI Responses API integration.
- Keep `OPENAI_API_KEY` and configurable `OPENAI_MODEL` in server environment variables. The specific model is not selected by the user; verify project access and suitable current documentation before configuring it.
- Add RAG over reviewed runtime documents, real source citations, session continuity, missing-data behavior, and the five scenario tests.
- Return structured proposals but leave operational mutations to their separate workflow endpoints.
- Provide reproducible setup commands, dependency versions, `.env.example` with empty placeholders, `.gitignore`, and deployment instructions matching the actual files.

### Milestone B — Working warehouse rules and workflows

- Implement persistent task, scan, picking, put-away, exception, and cycle-count flows from this specification.
- Implement reserve allocation, holds, replacement failure paths, and route-block/clearance state with deterministic rules.
- Use visibly synthetic inventory and routes until real data and a mapped environment are supplied.
- Test the UI against actual stored transactions, not hard-coded success messages.

### Milestone C — Render deployment

- Deploy the backend as a Render Web Service, binding to `0.0.0.0` and the supplied `PORT`.
- Use a linked Git provider repository or a supported container deployment. A Markdown/Word upload alone is not a running service.
- Store secrets using Render Environment settings; select durable storage for state.
- Verify health, authentication, a real AI request, retrieval, session continuity, and persistence across restart.
- Do not claim success if only health has been tested. State whether paid API testing occurred.

### Milestone D — Unity integration

- Inspect the installed Unity version/project first.
- Build an initial chat screen with question input, send button, answer/source display, loading/error states, and session handling.
- Connect to the deployed backend. Keep provider credentials out of Unity.
- Add task and exception screens, scanning/manual entry, and validated feedback actions.
- Add voice, selected languages, Avatar, 2D navigation, then AR placement and routes.
- Implement map alignment, positioning, walkable paths, and relocalization explicitly. Do not assume importing a scanned model provides navigation automatically.
- Test on the actual Android device before claiming camera, microphone, or AR support.

These milestones are implementation order, not a reduction of the user-approved functional scope.

## 12. Required acceptance tests

1. FIFO/FEFO explanation cites the appropriate knowledge section.
2. Missing location/lot information produces a limitation or question, not an invented shelf.
3. Follow-up questions use the same task/session without leaking other users' data.
4. Five requested / three picked creates a two-unit remainder exactly once.
5. No eligible reserve stock yields pending/escalation, not a fabricated route.
6. Damaged submission moves only the identified quantity to Quality Hold; no scrap status is introduced.
7. Ambiguous damaged-item identity prevents an incorrect stock mutation.
8. Unreadable labels cannot be guessed; Relabel and Short Pick do not double-deduct.
9. Wrong location/variant is blocked by ordinary rules even when AI is unavailable.
10. Wrong Bin creates the investigation/hold and only allocates verified stock.
11. Blocked paths are excluded; return requires a clearance event.
12. Blind-count expected quantity is absent from UI, AI inputs accessible to counters, and AI answers.
13. Recount adjustments follow the configured WMS rule and record an audit trail, never an AI decision.
14. AI cannot change stock through a prompt, suggested action, or forged client role.
15. Failed backend operations are never described as completed.
16. Lost network/retry does not duplicate business transactions.
17. Invalid API credentials/model access produce useful redacted errors.
18. Runtime retrieval does not turn unverified safety numbers into universal legal claims.
19. AR tracking loss does not display a stale location as current.
20. Database-backed records survive service restart; test fixtures are clearly labeled.

Use a human-reviewed answer set to evaluate factual support, completeness, clarity, and unsupported claims. Do not claim a numerical accuracy result before running and reviewing tests.

## 13. First instruction to the implementation agent

Read this entire document, including the integrated professor guidance in Section 15. The original Word/PDF attachments are not required. Inspect the project and report which requirements are already implemented. Start Milestone A with a runnable local backend and test UI. Prepare runtime knowledge documents from the operational sections, preserving provenance and excluding unverified source claims from authoritative guidance. Implement mock mode, then configurable real OpenAI access, RAG, session context, and meaningful tests. Provide exact launch and Render deployment instructions for the code actually created. Do not ask the user to paste their API key into chat, put it in source control, or insert it into Unity. Do not claim live API, Render, or Android verification that has not occurred. Continue into operational workflows and Unity in the stated order when the corresponding implementation work is requested.

## 14. Implementation references

- OpenAI API quickstart: https://developers.openai.com/api/docs/quickstart
- OpenAI File Search: https://developers.openai.com/api/docs/guides/tools-file-search
- Render Web Services: https://render.com/docs/web-services
- Render Environment Variables: https://render.com/docs/configure-environment-variables

These references support implementation, not the warehouse safety numbers supplied in the source.

## 15. Integrated professor guidance for this warehouse project

This section consolidates the relevant material from `AIIntegration_English.docx`, `Unity_AI_Feedback_AR_Warehouse_Assistant_Teaching_Guide_English.docx`, and the 13-slide `Unity_AI_Feedback_Systems.pdf`. General JSON tutorials, creative-art/game examples, gesture-control exercises, and unrelated student projects are intentionally omitted. Existing requirements in Sections 3 and 9–12 remain authoritative; this section adds integration detail without duplicating or replacing the user's revised warehouse workflows.

### 15.1 Required end-to-end interaction

Implement this chain explicitly:

1. A scan, task event, or worker question occurs.
2. The client records the meaningful event, rather than uploading every camera frame.
3. The backend combines authorized current warehouse facts with the event and relevant session history.
4. RAG retrieves the applicable SOP, safety, FIFO/FEFO, or FAQ passages.
5. OpenAI returns a grounded explanation and a structured UI-action proposal.
6. The backend validates the output; Unity validates the proposed action against its allowlist, current state, and permitted targets.
7. Unity displays feedback or performs the permitted presentation action. A business mutation still requires its separate authenticated, confirmed WMS operation.
8. Record the result in the same assistance session so subsequent advice reflects what actually happened.

Render calls the OpenAI API; this is not Unity automating the ChatGPT website. Both provider credentials and knowledge-service credentials remain server-side. The warehouse-specific teaching guide explicitly excludes body-gesture control from this project.

### 15.2 Component responsibilities to preserve

The shared components named in the professor's integration document map to the warehouse implementation as follows. Equivalent names are acceptable if responsibilities remain clear.

| Component | Warehouse responsibility |
|---|---|
| `AIEvent` | Typed event with category, session/task reference, occurrence time, and relevant observations. |
| `BehaviorTracker` | Record scan successes/failures, repeated mismatches, task progress, relevant pauses, and recent results locally. |
| `AITriggerEngine` | Decide whether to request assistance using configured triggers and cooldowns. |
| `AIFeedbackClient` | Send validated requests over HTTPS; represent waiting, success, and error states. |
| `AIFeedbackPresenter` | Display the answer, sources, and feedback style; use TextMeshPro if the chosen Unity UI uses it. |
| `AIActionRouter` | Check action allowlist, parameters, target, permissions, and current task state before presentation; log rejected actions. |
| `ProjectContextProvider` | Supply warehouse-specific station, task, item, lot, step, and recent-event context. Server-derived facts override client claims. |
| `AIDebugPanel` | Developer-only view of redacted request/response data, retrieval references, action result, errors, and latency. |

These are application components, not a requirement for eight independent network services. Keep developer diagnostics out of the ordinary worker interface.

### 15.3 AI Feedback Laboratory and development order

The professor calls the teaching scene “Scene 03.” It is a test harness, not a mandated production scene name or final product scope. The AI-first browser test interface may exercise the same contracts before the Unity test scene is built.

Provide four developer-facing areas:

| Area | Contents relevant to this project |
|---|---|
| Event Simulator | Worker question, scan success/failure, shelf mismatch, route deviation, task completion, summary request. |
| Behavior and Request Data | Current task/station, recognized item, expected location where authorized, recent events, question, and outgoing JSON. |
| AI Feedback and Safe Action | Answer, retrieved sources, proposed action, and the actual permitted UI effect. |
| Status | Local mock, scanning, retrieving, processing, success, or error; include request latency. |

Progress through: fixed mock response → successful request/response transport → one real text answer → schema-constrained output → one permitted visual action → behavior tracking → one configurable automatic trigger → real scans and AR input.

The initial Unity integration check is deliberately small: simulate an event, receive one grounded hint, and highlight one configured storage location. Completing it is an integration milestone, not completion of the full warehouse application. Simulated events must be visibly labeled and must not alter operational inventory.

### 15.4 Context collection and trigger configuration

Use the request contract in Section 10 and enrich it only with relevant, authorized fields. The professor's warehouse guide identifies these categories:

- User/task: selected language, authorized role, station, task type, current step, permitted functions, and whether the session is a training/test session.
- Recognition: marker/QR ID, product/shelf ID, recognition confidence when available, failure/mismatch counts, and successful rescans. Do not fabricate a confidence value for a scanner that does not provide one.
- Operation history: route progress, visited locations, quantity confirmations, elapsed time, task completion, and unresolved exceptions.
- Knowledge context: question, applicable SOP topic, lot-selection rule, previous advice, and any pending supervisor decision.

Record `occurredAtUtc` for events. Fetch role and inventory facts from trusted services as already required; the professor's example JSON is not permission to trust arbitrary client fields.

Expose thresholds, trigger counts, intervals, permitted fields, image inclusion, and knowledge-source selection through configuration/Unity Inspector rather than hard-coding them.

Support both worker-initiated questions and system-initiated assistance. “Three consecutive failures” and “30 seconds without progress” are teaching examples, not approved production warehouse thresholds. A trigger may offer help rather than interrupting work with a long answer. Apply cooldowns to repeated advice; do not suppress immediate deterministic error messages or necessary stop conditions.

When visual evidence is necessary, send only an intentionally selected, resized/redacted image appropriate to the authorized question. Keep unrelated inventory, personal labels, faces, and continuous footage out of the request. Do not infer worker identity, health, or capability from appearance.

### 15.5 Structured feedback and controlled actions

Extend the Section 10 response schema with typed presentation metadata where needed:

- `feedbackType`: distinguish a hint, explanation, warning, or summary.
- `priority`: control presentation emphasis without granting operational authority.
- `cooldownSeconds`: a bounded suggestion checked against application configuration.

Use the actual warehouse action names in Section 9, including `highlight_storage_location`; omit game actions such as changing difficulty or spawning game objects. Prefer schema-constrained responses and still validate them. A syntactically valid response can contain an invalid location or unsupported claim.

The action router must verify: Is this action permitted? Are its parameters valid? Does the target exist and belong to the authorized task? Does the current state allow it? Execute the presentation action or reject it with a recorded reason.

Require explicit worker confirmation before submitting exception reports or invoking state-changing business operations. The generic slides emphasize confirmation; the warehouse guide provides the practical distinction between displaying approved information and changing records. Follow that distinction without introducing approval prompts for every ordinary text display.

### 15.6 Continuous assistance session

Create a `sessionId` for the continuous warehouse task/assistance session and map it server-side to retained conversation state or a secure summary of previous interactions. User questions and automatic summaries use the same authorized context.

For longer sessions, retain task goal, role, current step, important scans, confirmed facts, exceptions, recent questions/answers, accepted advice, and the subsequent result. Do not keep every camera frame or raw movement in the prompt. Refresh live task facts rather than treating old conversation text as the current inventory record.

Acceptance example: after helping a worker rescan a damaged shelf marker, the assistant can explain the assigned lot's FIFO/FEFO rule and later summarize confirmed steps without repeatedly giving the initial rescan instruction.

### 15.7 Professor's occupied-location example

Retain this as an additional integration test, not a replacement for the user's five scenarios:

- A put-away task for synthetic item `SKU-2048` targets `B-12-03`.
- The trusted service reports that location unavailable and supplies no approved alternative.
- The worker scans twice and asks where to put the item.
- Retrieve the put-away exception SOP; explain that the assigned location is unavailable and no alternative is authorized.
- Offer to prepare an exception report. When the worker agrees, open the form with authorized task/scan evidence for review.
- Only authorized workflow processing can approve a new location or inventory change. AI must not invent another shelf or claim goods have moved.

Mention a designated holding area only if the retrieved SOP/current site data actually identifies one. The sample IDs are test fixtures, not real warehouse coordinates.

### 15.8 Backend and integration verification

The professor's backend sequence is: validate input → check request size/frequency → assemble project instructions and context → retrieve relevant knowledge → call OpenAI → validate the returned structure → return safe fields → record request ID, latency, and errors.

Use the health and feedback endpoints already specified. Suggested configuration includes `OPENAI_API_KEY`, `OPENAI_MODEL`, and `MAX_REQUESTS_PER_MINUTE`. The shared teaching document mentions a low-privilege `UNITY_CLIENT_TOKEN` for a controlled prototype; it must not replace production user/role authorization or be confused with the OpenAI key.

Add integration checks for rejected unknown actions, invalid targets, cooldown behavior, repeated-event handling, session continuity, missing retrieval evidence, and a proposed action that becomes stale before execution. Run the occupied-location scenario and the five user scenarios with recorded expected results. Review feedback frequency and clarity with users, then adjust configuration, prompts, and knowledge documents.

### 15.9 Source coverage map

| Source | Incorporated project-specific material |
|---|---|
| `AIIntegration_English.docx`, sections 1–6 and 8–9; warehouse row in section 7 | Event pipeline, component responsibilities, structured feedback, action validation, Render responsibilities, and mock-to-live teaching sequence. |
| `Unity_AI_Feedback_AR_Warehouse_Assistant_Teaching_Guide_English.docx`, sections 2–12 | Warehouse context/RAG, scan/task events, role separation, safe AR actions, occupied-location reporting, minimum data transfer, continuous sessions, and adaptation/testing steps. |
| `Unity_AI_Feedback_Systems.pdf`, slides 2–10 and 12–13 | Observer/assistant roles, event timing, test interface, context collection, session continuity, allowlist/confirmation principles, and staged adaptation. |

The unrelated artwork, game, and gesture examples and the general introductory explanations are not requirements of this project. The original attachments are optional provenance records only.
