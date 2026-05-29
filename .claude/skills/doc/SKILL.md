---
name: doc
description: Delegate documentation work to the isolated Doc agent after a verified Dev implementation. Spawns a fresh claude subagent on Sonnet with the full doc agent instructions and project context embedded. Accepts an optional feature name or description as argument.
---

# Delegate to Doc Agent

This skill delegates documentation work to the Doc agent — an isolated Sonnet subagent whose only job is to update or create docs that reflect what was just implemented and verified.

## Who invokes this, and when

The **tech lead** invokes `/doc` after `/verify` returns an **APPROVED** verdict. The doc step is mandatory — no implementation is complete until the docs reflect the current state.

## Before spawning

Follow this sequence on every invocation. Do not skip steps.

1. **Confirm `/verify` returned APPROVED** for the current implementation. Do not invoke Doc before the implementation is approved.
2. **Read these files in full** — they must be embedded into the Doc agent's prompt:
   - `.claude/agents/doc.md`
   - `docs/architecture.md`
   - `docs/domain-model.md`
   - `docs/conventions.md`
   - `docs/workflow.md`
   - `CLAUDE.md`
3. **Note the implementation brief** (`$ARGUMENTS` if provided, or the brief from the preceding `/dev` and `/verify` invocations) — this is what the Doc agent uses to understand what changed.

## Spawning the Doc agent

Use the Agent tool with:
- `subagent_type`: `"claude"`
- `model`: `"sonnet"`
- `description`: a short description such as "update docs after <feature> implementation"
- **Do NOT pass the `isolation` parameter.** Doc edits the working tree directly so the tech lead can review the doc diff in place (`git diff docs/`) and re-call `/doc` with a correction brief if needed. No worktree, no stash-transfer.
- `prompt`: assembled as follows:

```
[Full contents of .claude/agents/doc.md]

---

## Current Documentation (injected by /doc skill)

### CLAUDE.md
[Full contents of CLAUDE.md]

### docs/architecture.md
[Full contents of docs/architecture.md]

### docs/domain-model.md
[Full contents of docs/domain-model.md]

### docs/conventions.md
[Full contents of docs/conventions.md]

### docs/workflow.md
[Full contents of docs/workflow.md]

---

## Implementation Brief

[The brief from $ARGUMENTS, or a summary of what was just implemented and verified]
```

The subagent must receive everything it needs in the prompt. It has no access to the session history.

## After Doc completes

Doc will end its response with a `DOCUMENTATION REPORT`. The doc edits are already on the working tree — review with `git diff docs/` directly. Present the report to the tech lead.

The implementation cycle is now complete:
```
/dev → /verify → /doc → commit
```

## Hard bans

- **NEVER** invoke Doc before `/verify` returns APPROVED.
- **NEVER** summarize or paraphrase the doc files — embed them verbatim.
- **NEVER** skip reading `.claude/agents/doc.md` before spawning.
- **NEVER** pass `isolation: "worktree"` to the Agent tool — Doc must edit master's working tree directly.
