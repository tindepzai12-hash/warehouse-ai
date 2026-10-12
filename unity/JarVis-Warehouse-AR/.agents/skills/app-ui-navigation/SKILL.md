---
name: app-ui-navigation
description: "Implement reachable, accessible UI states and navigation while preserving approved identity."
---

# UI, UX and navigation

## Purpose
Implement reachable, accessible UI states and navigation while preserving approved identity.

## Trigger
Screen/layout/control/route work, responsive repair or runtime visual defects.

## Pre-check
Inspect approved current UI, route owner, shared header/dock, layout constraints, tokens, locale/text-size/theme preferences and existing functional coverage.

## Workflow
Define success/empty/loading/error/disabled/offline states and routes/return paths. Reuse components and shared navigation; keep business data live and UI preferences local. Respect user's palette/assets and distinguish refinement from redesign. Use real icon geometry, adequate contrast/targets, no overflow, safe area/keyboard and reduced-motion behavior. For explicit prototype-first, honor approval before production mutation.

## Safety Rules
Do not remove features to fit height, replace business behavior for a prettier screen, fake controls, or run endless visual polish loops.

## Verification
Inspect related theme/locale/large-text/device-class captures together, hit/raycast and route paths; fix supported defects in one batch and confirm once. Screenshots and target-size tests do not prove glyph visibility or behavior independently.

## Failure Handling
Trace owner/layout/paint/mask/raycast/lifecycle instead of random CSS/scene tweaks. When a requested visual cannot be inspected, report unverified rather than inventing a pass.

## Output
Visible changes, retained routes/functions, bounded visual/interaction evidence and remaining accessibility/device gaps.
