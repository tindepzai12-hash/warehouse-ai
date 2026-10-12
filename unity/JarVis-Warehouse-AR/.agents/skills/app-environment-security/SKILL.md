---
name: app-environment-security
description: "Configure app environments, secrets and permissions without leaking client-side credentials."
---

# Environment and security

## Purpose
Configure app environments, secrets and permissions without leaking client-side credentials.

## Trigger
Backend/provider/auth setup, environment drift, release configuration or secret handling.

## Pre-check
Identify local/test/staging/production origins, variable names, secret storage, auth roles and persistence requirements without printing values. Check config precedence and ignore rules.

## Workflow
Define non-secret example configuration and required variables. Keep provider keys server-side; use least-privilege user/session authentication in the client. Separate fixture accounts/databases from production. Validate startup requirements, HTTPS/origin policy, upload bounds, rate limits and error redaction. Read current provider documentation when executing version-sensitive setup.

## Safety Rules
Never put keys/passwords/tokens in assets, screenshots, prompts, logs or copied skill bundles. Never change OS security/privacy settings or accept permission prompts through automation. No paid resource upgrade or secret rotation without authorization.

## Verification
Verify missing-config failure, auth-required/forbidden behavior and client artifact contents using bounded tests. Test secret presence by boolean only; do not echo values.

## Failure Handling
Classify missing config vs permissions vs provider quota; report safe error codes. Stop if required access is absent; do not rotate accounts or bypass policy.

## Output
Environment matrix, variable names only, permission boundaries, retention/storage risks and redacted verification.
