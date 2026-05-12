---
name: verify
description: Tech lead verification of Dev agent output. Checks that the implementation matches the original brief, follows all project conventions, and passes build and tests. Returns a structured Approved / Needs rework verdict.
---

# Verify Dev Agent Output

This skill is the tech lead's quality gate after every Dev invocation. It checks the implementation against the brief, the conventions, and the build — and produces a clear verdict before the tech lead accepts the work.

## Who invokes this, and when

The **tech lead** invokes `/verify <original brief>` immediately after Dev reports completion. The brief passed here must be the same brief that was passed to `/dev`.

## Verification steps

Follow every step in order. Do not skip any.

### 1. Read the brief

The brief is `$ARGUMENTS`. This is the ground truth for what was supposed to be implemented.

### 2. Inspect all changes

Run:
```bash
git diff HEAD
```
If changes are not yet committed, also run:
```bash
git status
```

Read every created, modified, or deleted file in full. Do not skim.

### 3. Check spec compliance

For each item in the brief, verify it was implemented exactly as specified. Flag any:
- Missing pieces
- Pieces implemented differently than specified
- Extra additions beyond the brief (scope creep)

### 4. Check convention compliance

Read `docs/conventions.md` and verify each rule against the implementation:

- [ ] Entities are in `src/BoatSpotFinder.Core/Entities/`
- [ ] No EF entities passed directly to views — only ViewModels
- [ ] ViewModels are in `src/BoatSpotFinder.Web/Models/` named `<Feature>ViewModel`
- [ ] Business logic is in `src/BoatSpotFinder.Core/Services/`, not in controllers
- [ ] Repository interfaces are in `src/BoatSpotFinder.Core/Interfaces/`
- [ ] Repository implementations are in `src/BoatSpotFinder.Infrastructure/Repositories/`
- [ ] EF configurations are in `src/BoatSpotFinder.Infrastructure/Data/Configurations/`
- [ ] Money fields use `decimal` with `[Column(TypeName = "decimal(18,2)")]`
- [ ] Controllers use `[Authorize(Roles = "...")]` on protected actions
- [ ] No business logic in controllers
- [ ] No direct DbContext access outside Infrastructure layer
- [ ] No unnecessary comments (no "what" comments, no task-reference comments)

### 5. Run build

```bash
dotnet build BoatSpotFinder.slnx
```

Must result in **0 errors, 0 warnings**.

### 6. Run tests

```bash
dotnet test
```

Must result in **all tests passing**.

## Report format

End every invocation with this structured report:

```
VERIFICATION REPORT
===================
Brief:               <one-line summary of what was asked>
Spec match:          Pass | Fail — <list missing or incorrect items, or "complete">
Convention checks:   Pass | Fail — <list each violation found, or "none">
Build:               Pass | Fail — <0 errors / error summary>
Tests:               Pass | Fail — <N passed, N failed>

Verdict:             APPROVED | NEEDS REWORK

<If NEEDS REWORK: bulleted list of exactly what must be fixed before re-verification.
 Be specific — file path, what is wrong, what is expected.>
```

If the verdict is **APPROVED**, end the report with:

```
Next: run `/doc <brief>` to update the project documentation before committing.
```

If the verdict is **NEEDS REWORK**, the tech lead writes a correction brief and re-invokes `/dev` with the fix scope before running `/verify` again. Do NOT run `/doc` until the implementation is approved.

## Hard bans

- **NEVER** approve work that fails the build.
- **NEVER** approve work that has failing tests.
- **NEVER** approve work with convention violations.
- **NEVER** approve incomplete spec implementation — every item in the brief must be present.
- **NEVER** let scope creep pass — flag additions beyond the brief as a violation.
