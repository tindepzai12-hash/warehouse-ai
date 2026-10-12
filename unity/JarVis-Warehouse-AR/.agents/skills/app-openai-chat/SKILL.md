---
name: app-openai-chat
description: "Integrate server-proxied OpenAI chat with shared context, parsing, evaluation and cost controls."
---

# OpenAI and AI chat

## Purpose
Integrate server-proxied OpenAI chat with shared context, parsing, evaluation and cost controls.

## Trigger
OpenAI inference, AI chat/context, response parsing or provider errors; not local-only recording.

## Pre-check
Confirm backend proxy, server secret, authorized model, current official OpenAI docs, existing history owner and whether a billable smoke call is approved.

## Workflow
Keep the user/session conversation in one server-owned context for text/media. Separate system policy, trusted tool data and untrusted user/retrieved content. Select the model from current capabilities and task requirements, not a hard-coded reference-project default. Parse structured outputs defensively; distinguish citations/evidence from general answers. Bound input/output, timeouts and cost. If streaming, define partial/cancel/final behavior and history commit ownership.

## Safety Rules
No provider key on the client, repeated cost-generating retries, account rotation, fabricated successful answers or treating retrieved instructions as authority. A mock model test is not model-quality evidence.

## Verification
Test auth/proxy/context/parse failures with injected providers. Only perform a bounded live call with approved content and cost; record its quality limits separately.

## Failure Handling
Classify provider configuration, model access, quota, network, prompt/context and parsing errors. Preserve terminal ambiguous outcomes rather than repeating spend.

## Output
Model/config names without secrets, context/retention policy, fixture vs live evaluation and cost/release dependencies.
