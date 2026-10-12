---
name: app-git-safety
description: "Preserve dirty work and scope Git changes, commits, pushes and pull requests."
---

# Git and source safety

## Purpose
Preserve dirty work and scope Git changes, commits, pushes and pull requests.

## Trigger
Editing a repository, reviewing a diff, preparing a release, or explicit Git operations.

## Pre-check
Inspect branch, status, staged vs unstaged diff, untracked files and relevant history. Determine auto-deploy triggers before any push. Record which changes predate this task.

## Workflow
Implement in the current authorized checkout. Review only intended files, including generated files and secret/DB exclusions. Stage explicit paths only when requested. Propose a focused commit boundary; commit, create branches, push or create PRs only with explicit authorization. Attach any created/requested PR using the host tool when available.

## Safety Rules
No reset/clean/force-push, bulk git add, account rotation, silent overwrite or source upload. A deployment request is not automatically permission to publish all dirty work.

## Verification
Check final diff and intended file set; inspect staged content if staging is authorized. Correlate a release with the exact commit rather than branch name alone.

## Failure Handling
On conflict, stop the destructive step, preserve both versions and ask only for the unresolved ownership decision. Do not hide unrelated dirty files.

## Output
Own file changes vs existing work, reviewed diff, authorized Git actions and release revision.
