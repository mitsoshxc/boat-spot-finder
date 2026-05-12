# Boat Spot Finder

Web application for booking marina/harbour boat spots. Four roles: **BoatOwner**, **PlaceOwner**, **Admin**, **SuperAdmin**.

Stack: ASP.NET Core 10 MVC · Entity Framework Core 10 · SQL Server · ASP.NET Core Identity

## Team — READ FIRST

| Agent | Role | Color | Model |
|---|---|---|---|
| **Dev** | Full-stack developer. Implements task briefs from the tech lead across C#, Razor, and EF Core. Enforces conventions from `docs/`. Runs `dotnet build` to verify. **All code goes through Dev.** | Green | Sonnet |
| **Doc** | Documentation specialist. Exclusive owner of `docs/*.md`. Updates or creates docs after every verified implementation. Never touches production code. **All doc updates go through Doc.** | Blue | Sonnet |

### Workflow — MANDATORY, NO EXCEPTIONS

**You = tech lead. You plan, brief, review, and approve. Dev executes. NEVER write production code directly.**

| Step | Action |
|---|---|
| New feature or entity | Tech lead designs the approach → writes a concrete brief → `/dev <brief>` → review Dev's report → `/verify <brief>` → if approved: `/doc <brief>` → commit |
| Bug fix | Tech lead identifies root cause → writes a scoped brief → `/dev <brief>` → `/verify <brief>` → if approved: `/doc <brief>` → commit |
| Rework | Tech lead writes a correction brief → `/dev <brief>` → `/verify <brief>` → if approved: `/doc <brief>` → commit |

**NEVER skip Dev for code. NEVER skip Doc after approval. NEVER make architecture decisions inside Dev — bring them back to the tech lead.**

## Skills

| Skill | Invoked by | When to use |
|---|---|---|
| `/dev <brief>` | Tech lead | Delegate an implementation task to the Dev agent |
| `/verify <brief>` | Tech lead | Verify Dev's output against the brief, conventions, build, and tests |
| `/doc <brief>` | Tech lead | Delegate documentation updates to the Doc agent after an approved verification |

## Implementation Plan

**`PLAN.md` is the single source of truth for what to build and in what order.** All phases, tasks, file lists, and dev brief groupings are defined there. Follow it step by step.

The tech lead reads `PLAN.md` to write `/dev` briefs. Dev reads `PLAN.md` to understand context and file targets. After each approved task, update the task status in `PLAN.md` from `[ ]` to `[x]`.

## Docs

The `docs/` folder will be populated via `/doc` as each phase is completed. During implementation, `PLAN.md` is the only reference needed.
