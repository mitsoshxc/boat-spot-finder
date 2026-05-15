# Workflow

## Team

| Agent | Role | Owner of |
|---|---|---|
| **Tech lead** (you) | Plans, writes briefs, reviews, approves | Architecture decisions, `PLAN.md` status |
| **Dev** | Implements briefs | All production code (`src/`) |
| **Doc** | Writes documentation after approval | All files in `docs/` |

**Never write production code directly as tech lead. Never skip Doc after an approved Dev implementation.**

---

## Mandatory Step Sequence

```
1. Tech lead writes brief
2. /dev <brief>          — Dev implements and runs dotnet build
3. /verify <brief>       — Tech lead verifies output; returns Approved or Needs rework
4. If Approved:
     a. Update PLAN.md task status [ ] → [x]
     b. (After all tasks in phase are done) /doc <phase summary>
5. If Needs rework:
     Tech lead writes correction brief → back to step 2
```

Do not call `/doc` after every brief. Call it once per phase, after every task in the phase has an `[x]` in `PLAN.md`.

---

## Skills

| Skill | Used by | Effect |
|---|---|---|
| `/dev <brief>` | Tech lead | Delegates implementation to Dev agent. Dev runs in isolation with no session history. |
| `/verify <brief>` | Tech lead | Runs a structured check of Dev's output against the brief, conventions, and build result. Returns Approved or Needs rework. |
| `/doc <phase summary>` | Tech lead (after Approved) | Delegates documentation updates to Doc agent. Doc reads git diff and source files; updates or creates docs. |

---

## PLAN.md

`PLAN.md` is the single source of truth for what to build. It contains phases, tasks, file targets, and brief groupings. Status is maintained by the tech lead after each approval.

- Tech lead reads `PLAN.md` to write `/dev` briefs.
- Dev reads `PLAN.md` for context and file targets.
- Doc does not modify `PLAN.md`.

Status legend: `[ ]` not started · `[x]` complete · `[~]` in progress.

---

## Branch Strategy

All work is committed to `master`. Feature branches are not used at this stage. Docker Compose / CI / deployment manifests are deferred to Phase 8.

---

## Documentation Cadence

| Phase | Doc covers |
|---|---|
| Phase 1 | Foundation — entities, EF config, DbContext, migrations, Hangfire, health checks |
| Phase 2 + 2b | Auth — registration, login, invite flow, password reset, audit logging |
| Phase 3 + 3b | PlaceOwner — marina & spot management, canvas editor, seasonal rules, ES indexing |
| Phase 4 | BoatOwner — vessel management |
| Phase 5 | Booking system — full lifecycle, recurring jobs, email notifications |
| Phase 5b | Reviews & ratings |
| Phase 6 | Admin — dashboard, invitations, marina management, settings |
| Phase 7 | Browse & Search |
| Phase 8 | Polish — validation, pagination, error pages, Docker, k8s |
