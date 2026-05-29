# Boat Spot Finder

Web application for booking marina/harbour boat spots. Three roles: **BoatOwner**, **PlaceOwner**, **Admin**. `ApplicationUser.IsSuperAdmin` is a bool flag (not an Identity role) that blocks deletion of the seeded admin account at the service layer.

Stack: ASP.NET Core 10 MVC · Entity Framework Core 10 · SQL Server · ASP.NET Core Identity

## Team — READ FIRST

| Agent | Role | Color | Model |
|---|---|---|---|
| **Dev** | Full-stack developer. Implements task briefs from the tech lead across C#, Razor, and EF Core. Enforces conventions from `docs/`. Runs `dotnet build` to verify. **All code goes through Dev.** | Green | Sonnet (default) · Haiku for narrow mechanical briefs |
| **Doc** | Documentation specialist. Exclusive owner of `docs/*.md`. Updates or creates docs once per phase, after every task in the phase is approved. Never touches production code. **All doc updates go through Doc.** | Blue | Sonnet (default) · Haiku for fully-specified row-level briefs |

**Dev model selection.** Default Sonnet. Tech lead may override to Haiku for narrow, mechanical briefs — small refactors, view tweaks, single-method repo additions, rename-only changes. Stay on Sonnet for anything touching the domain model, controllers, multi-file changes, or convention-sensitive work.

**Doc model selection.** Default Sonnet. Tech lead may override to Haiku only when the brief is fully specified at the row level — exact section placement, exact rule rewrite, what to preserve, what to drop, no synthesis required from Doc. Stay on Sonnet for open-ended briefs, multi-doc synthesis, or feature docs that require judging tone and structure.

### Workflow — MANDATORY, NO EXCEPTIONS

**You = tech lead. You plan, brief, review, and approve. Dev executes. NEVER write production code directly.**

| Step | Action |
|---|---|
| New feature or entity | Tech lead designs the approach → writes a concrete brief → `/dev <brief>` → review Dev's report → `/verify <brief>` → if approved: flip `PLAN.md` task `[ ]` → `[x]` → commit |
| Bug fix | Tech lead identifies root cause → writes a scoped brief → `/dev <brief>` → `/verify <brief>` → if approved: flip `PLAN.md` task `[ ]` → `[x]` → commit |
| Rework | Tech lead writes a correction brief → `/dev <brief>` → `/verify <brief>` → if approved: flip `PLAN.md` task `[ ]` → `[x]` → commit |
| Phase complete | When every task in a phase is `[x]`: `/doc <phase summary>` — one Doc call covering the whole phase |

**NEVER skip Dev for code. NEVER skip Doc after a phase is fully approved. NEVER make architecture decisions inside Dev — bring them back to the tech lead.**

## Skills

| Skill | Invoked by | When to use |
|---|---|---|
| `/frontend-design <purpose + fields>` | Tech lead | Generate production-grade HTML/CSS markup for any new or substantially changed `.cshtml` view. Output is passed verbatim into the `/dev` brief — Dev writes the actual files. JS is not in scope for this skill — describe JS behaviour in the `/dev` brief and Dev implements it directly. |
| `/dev <brief>` | Tech lead | Delegate an implementation task to the Dev agent |
| `/verify <brief>` | Tech lead | Verify Dev's output against the brief, conventions, build, and tests |
| `/doc <phase summary>` | Tech lead | Delegate phase-level documentation updates to the Doc agent after every task in the phase is approved |

## Implementation Plan

**`PLAN.md` is the single source of truth for what to build and in what order.** All phases, tasks, file lists, and dev brief groupings are defined there. Follow it step by step.

The tech lead reads `PLAN.md` to write `/dev` briefs. Dev reads `PLAN.md` to understand context and file targets. After each approved task, update the task status in `PLAN.md` from `[ ]` to `[x]`.

## Testing

**[`TESTING.md`](TESTING.md)** tracks automated test cases to add to `tests/BoatSpotFinder.Tests/` and manual smoke tests to run, organized by phase. Updated at phase milestones — not after every task.

## Docs

| Doc | Contents |
|---|---|
| [`docs/architecture.md`](docs/architecture.md) | Tech stack, solution structure, layer responsibilities, Program.cs wiring, key commands |
| [`docs/domain-model.md`](docs/domain-model.md) | Entities, fields, relationships, FK delete rules, enums, EF notes |
| [`docs/conventions.md`](docs/conventions.md) | Layering rules, naming, money/date conventions, soft-delete, controller rules, seed data rules |
| [`docs/workflow.md`](docs/workflow.md) | Team structure, mandatory step sequence, skills, branch strategy, doc cadence |
| [`docs/features/booking-lifecycle.md`](docs/features/booking-lifecycle.md) | Booking status flow, pricing cascade, role-resolution in CancelAsync, Hangfire recurring jobs, email failure policy |
| [`docs/features/reviews-and-ratings.md`](docs/features/reviews-and-ratings.md) | Bidirectional post-stay reviews, eligibility rules, rating recompute, ES sync, review-invite email fan-out |
