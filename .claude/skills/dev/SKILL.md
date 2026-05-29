---
name: dev
description: Delegate an implementation task to the isolated Dev agent. Reads project docs, spawns a fresh claude subagent on Sonnet with full context embedded, and passes the tech lead's spec. The Dev agent runs in complete isolation — no session history, no shared context.
---

# Delegate to Dev Agent

This skill delegates a concrete, well-scoped implementation task to the Dev agent — an isolated Sonnet subagent whose only job is to write the code the tech lead specifies.

## Who invokes this, and when

The **tech lead** invokes `/dev <brief>` after deciding on the approach. The brief must be concrete and unambiguous — architecture decisions are the tech lead's responsibility, not Dev's.

## Before spawning

Follow this sequence on every invocation. Do not skip steps.

1. **Read the brief** (the skill argument `$ARGUMENTS`). If it is ambiguous about file placement, architecture, or scope, **stop and ask the tech lead** before spawning Dev.
2. **Read these files in full** — they must be embedded into the Dev agent's prompt so it starts with full context:
   - `docs/conventions.md`
   - `docs/architecture.md`
   - `docs/domain-model.md`
   - `.claude/agents/dev.md`
3. **Confirm the brief is a single, well-scoped step.** If it spans more than one logical change, stop and ask the tech lead to split it.

## Spawning the Dev agent

Use the Agent tool with:
- `subagent_type`: `"claude"`
- `model`: `"sonnet"`
- `description`: a short (5-10 word) description of the task derived from the brief
- **Do NOT pass the `isolation` parameter.** Dev edits the working tree directly so the tech lead can verify the diff in place (`git status` / `git diff`) and re-call `/dev` with a correction brief if anything is off. No worktree, no stash-transfer, no risk of stale-base merge conflicts.
- `prompt`: assembled as follows:

```
[Full contents of .claude/agents/dev.md]

---

## Project Reference (injected by /dev skill)

### Conventions
[Full contents of docs/conventions.md]

### Architecture
[Full contents of docs/architecture.md]

### Domain Model
[Full contents of docs/domain-model.md]

---

## Tech Lead Brief

[The brief from $ARGUMENTS — verbatim, no paraphrasing]
```

The subagent must receive everything it needs in the prompt. It has no access to the session history.

## After Dev completes

Dev will end its response with an `IMPLEMENTATION REPORT`. The edits are already on the working tree — review with `git status` / `git diff` directly. Present the report to the tech lead and prompt:

```
Dev has finished. Run `/verify <original brief>` to validate the implementation.
```

## Hard bans

- **NEVER** spawn Dev with an ambiguous brief. Ambiguity belongs to the tech lead, not Dev.
- **NEVER** summarize or paraphrase the project docs — embed them verbatim.
- **NEVER** skip reading `.claude/agents/dev.md` before spawning — it is the agent's instruction set.
- **NEVER** split a single brief across multiple Dev invocations — one brief, one agent.
- **NEVER** pass `isolation: "worktree"` to the Agent tool — Dev must edit master's working tree directly.
