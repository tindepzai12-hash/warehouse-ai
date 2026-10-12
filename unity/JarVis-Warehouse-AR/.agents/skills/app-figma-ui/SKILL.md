---
name: app-figma-ui
description: "Translate approved Figma/reference designs into real components without confusing visuals with data."
---

# Figma and design translation

## Purpose
Translate approved Figma/reference designs into real components without confusing visuals with data.

## Trigger
Figma/reference import, prototype-first request or design approval; not routine backend changes.

## Pre-check
Identify exact file/frame/node and applicable user design authority. Inspect dimensions, components, tokens/assets and which rows/variants are reference-only. Load installed Figma tool prerequisites before using those tools.

## Workflow
Map reference → components/states → responsive layout → tokens/assets → native/web implementation. Preserve factual business behavior and source imagery. If prototype-first is requested, build an isolated clearly labeled prototype, show it and stop at approval before production changes. After approval, translate interactive components, not a screenshot-as-screen. Track feature preservation as traceability, not completed functionality.

## Safety Rules
No editing a reference frame or publishing design without authorization. No real credentials/data in prototypes. Do not seed backend with drawn quantities; latest explicit design approval overrides older reference instructions.

## Verification
Check measured layout, asset aspect/alpha/font metrics, interaction/navigation, narrow/wide/large-text and actual data states. Record source identifiers and approved scope; do not claim exact parity where renderer metrics differ.

## Failure Handling
If assets/tool access are missing, preserve existing design and mark gaps; do not invent user approval or silently change the visual authority.

## Output
Source/approval ledger, component/asset mapping, prototype boundary, changed nodes/files and visual differences.
