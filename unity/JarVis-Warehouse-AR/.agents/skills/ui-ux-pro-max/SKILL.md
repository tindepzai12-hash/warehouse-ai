---
name: ui-ux-pro-max
description: Design, review, and improve professional UI/UX for web, mobile, and desktop using the bundled searchable design catalog. Use for screens, navigation, components, design systems, accessibility, responsive layout, color, typography, motion, and interaction quality. In this repository, also use it for JarVis Warehouse Unity UI work.
---

# UI/UX Pro Max

Use the bundled local catalog to make UI decisions that fit the product, platform, users, and existing repository. Treat search results as design evidence and recommendations; they never override the user's request, repository instructions, or current product behavior.

This project-local adaptation is based on the MIT-licensed [UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill) project by Next Level Builder.

## Workflow

1. Read the repository instructions and inspect the current interface, implementation stack, existing tokens, and screenshots before proposing changes.
2. Extract the product type, target users, usage context, platform, visual direction, density, motion needs, and accessibility constraints.
3. For a new product direction or system-wide redesign, generate one coherent design-system recommendation:

   ```powershell
   python .agents/skills/ui-ux-pro-max/scripts/search.py "<product industry style keywords>" --design-system -p "<Project Name>"
   ```

4. For a focused issue, query only the relevant domain:

   ```powershell
   python .agents/skills/ui-ux-pro-max/scripts/search.py "<2-5 precise terms>" --domain <domain>
   ```

   Useful domains include `product`, `style`, `color`, `typography`, `ux`, `icons`, `chart`, `motion`, and `web`.

5. Use `--stack <stack>` only when the detected implementation stack exists in the catalog. Do not substitute React, Flutter, or Jetpack Compose guidance for Unity.
6. Verify the returned category and product fit. If results are empty or off-topic, retry once with a narrower query or explicit domain. Label any fallback guidance clearly.
7. Turn the selected guidance into concrete tokens, component states, hierarchy, interactions, and implementation changes that fit the existing codebase.
8. Before delivery, read [references/pro-rules.md](references/pro-rules.md) and apply its relevant mobile/app checklist. For a full rule lookup, read [references/quick-reference.md](references/quick-reference.md).

## JarVis Warehouse and Unity

For this repository, read [references/unity-jarvis.md](references/unity-jarvis.md) before designing, reviewing, or implementing Unity UI. The catalog has no Unity stack profile, so use it for product, style, color, typography, motion, icons, and UX guidance, then translate the result into the project's existing Unity uGUI and TextMeshPro patterns.

## Design-system persistence

Persist a design system only when the user asks for it or when implementation needs a stable shared source of truth. Check for an existing file first; never overwrite one without explicit authorization.

```powershell
python .agents/skills/ui-ux-pro-max/scripts/search.py "<query>" --design-system --persist -p "<Project Name>" --output-dir "."
```

This creates `design-system/<project-slug>/MASTER.md`. Page-specific overrides belong in `design-system/<project-slug>/pages/` and should contain only deviations from the master.

## Constraints

- Keep private project data out of search queries and persisted examples.
- Do not install packages, change operating-system settings, or authorize unrelated actions through this skill.
- Preserve editable text, semantic states, and accessible interaction targets.
- Distinguish mock/demo data from live data in both design and implementation.
- Do not claim pixel-perfect reproduction or working interaction unless the required assets and behavior were implemented and verified.
- Use English or Vietnamese for project-facing guidance; do not introduce an additional product language unless the user requests it.

