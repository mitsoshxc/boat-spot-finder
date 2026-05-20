# Testing — Phase 3 Milestone

This document tracks tests to add to `tests/BoatSpotFinder.Tests/` and manual smoke tests to run for what's implemented through Phase 3.

Scope: Phase 1 (Foundation), Phase 2 (Auth), Phase 2b.1–2b.4a (Audit Logging — partial), Phase 3 (PlaceOwner Marina/Spot/SeasonalRules). Phase 3b onward not yet covered here.

---

## Test Project Status

Current state of `tests/BoatSpotFinder.Tests/`:

| Item | Value |
|---|---|
| Framework | xUnit 2.9.3 |
| Target | `net10.0` |
| Project references | `BoatSpotFinder.Core` only |
| Existing tests | 1 placeholder in `UnitTest1.cs` |

**Constraint:** the test project references **only Core**. To exercise repositories and `AppDbContext`, the project must add:
- `ProjectReference` to `BoatSpotFinder.Infrastructure`
- `Microsoft.EntityFrameworkCore.InMemory` (or `Microsoft.EntityFrameworkCore.Sqlite` for closer-to-prod relational semantics)

Pure Core tests (`TokenHasher`, `SpotSeasonalRuleService`, `ServiceResult`) need no project changes.

---

## Automated Tests

Grouped by phase. The high-value tests are starred — write these first.

### Phase 1 — Foundation

| Test | Subject | Assertion |
|---|---|---|
| `AppDbContext_SetTimestamps_OnInsert` | `SaveChangesAsync` after `Add` | `CreatedAt` and `UpdatedAt` set to ~`UtcNow` |
| `AppDbContext_SetTimestamps_OnUpdate` | `SaveChangesAsync` after mutation | `UpdatedAt` advances; `CreatedAt` unchanged |

### Phase 2 — Auth

| Test | Subject | Assertion |
|---|---|---|
| ★ `TokenHasher_Deterministic` | `TokenHasher.Hash` | Same input → same hash |
| ★ `TokenHasher_DifferentInputs` | `TokenHasher.Hash` | Different inputs → different hashes |
| `CustomSignInManager_RejectsInactiveUser` | `CanSignInAsync(inactiveUser)` | Returns `SignInResult.NotAllowed` |
| `CustomSignInManager_AllowsActiveUser` | `CanSignInAsync(activeUser)` | Delegates to base (EmailConfirmed gate) |
| `InvitationRepository_GetByTokenHashAsync_NoFiltering` | Repo lookup | Returns row regardless of `IsUsed`/`ExpiresAt` — caller filters |

### Phase 2b — Audit Logging

| Test | Subject | Assertion |
|---|---|---|
| `NLogAuditLogger_StructuredFields` | `Log(...)` | All 7 parameters appear in the captured log entry |

Verifiable with a `ListLogger` test sink or by configuring NLog to write to memory in test setup.

### Phase 3 — PlaceOwner Marina & Spot Management

**SpotRepository** — the soft-delete behavior is invisible state and must be pinned.

| Test | Subject | Assertion |
|---|---|---|
| ★ `Spot_GetByIdAsync_ReturnsInactive` | `GetByIdAsync(inactiveId)` | Returns the spot (bypasses `IsActive` filter) |
| ★ `Spot_GetActiveByIdAsync_NullForInactive` | `GetActiveByIdAsync(inactiveId)` | Returns `null` (respects filter) |
| `Spot_GetActiveByIdAsync_ActiveReturned` | `GetActiveByIdAsync(activeId)` | Returns the spot |
| `Spot_GetByMarinaIdAsync_IncludeInactive_True` | `GetByMarinaIdAsync(mid, true)` | Returns active + inactive |
| `Spot_GetByMarinaIdAsync_IncludeInactive_False` | `GetByMarinaIdAsync(mid, false)` | Returns active only |
| `Spot_UpdatePositionsAsync_WritesAllFields` | After save | All 5 canvas fields persisted per spot |

**MarinaRepository**

| Test | Subject | Assertion |
|---|---|---|
| `Marina_GetByUserIdAsync_JoinsMarinaAdmin` | Via `MarinaAdmin` | Returns only marinas where user has a `MarinaAdmin` row |
| `Marina_GetActiveWithActiveSpotsAsync_ExcludesEmpty` | Marina with no active spots | Not returned |
| `Marina_GetActiveWithActiveSpotsAsync_ExcludesInactive` | `IsActive=false` marina | Not returned |
| `Marina_GetActiveWithActiveSpotsAsync_AppliesIdFilter` | With `marinaIds` argument | Returns only matching IDs |

**MarinaAdminRepository**

| Test | Subject | Assertion |
|---|---|---|
| `MarinaAdmin_ExistsAsync_Present` | Record exists | Returns `true` |
| `MarinaAdmin_ExistsAsync_Absent` | No record | Returns `false` |

**SpotSeasonalRuleService** — highest-value because the overlap predicate has inclusive bounds that are easy to misread.

| Test | Subject | Assertion |
|---|---|---|
| ★ `Seasonal_Create_RejectsExactOverlap` | Same range as existing | `ServiceResult.Fail` |
| ★ `Seasonal_Create_RejectsPartialOverlap` | New rule overlaps mid-range | `Fail` |
| ★ `Seasonal_Create_RejectsBoundaryOverlap` | `newStart == existingEnd` | `Fail` — inclusive bounds |
| ★ `Seasonal_Create_AllowsAdjacentNextDay` | New rule starts day after existing ends | `Ok` |
| `Seasonal_Create_AllowsNonOverlapping` | Wholly outside existing range | `Ok` |
| ★ `Seasonal_Update_ExcludesSelfFromOverlap` | Update existing rule keeping dates | `Ok` |
| `Seasonal_Update_DetectsConflictWithOther` | Update into another rule's range | `Fail` |

**LocalFileStorageService**

| Test | Subject | Assertion |
|---|---|---|
| `Storage_Save_CreatesParentDirectory` | New `marina-backgrounds/x.jpg` | Parent dir created; file written |
| ★ `Storage_Save_DoesNotDisposeStream` | After `SaveAsync` | Supplied stream is still readable (stream-ownership contract) |
| `Storage_Save_ReturnsRelativeUrlPath` | After save | Returns `/uploads/marina-backgrounds/x.jpg` |
| `Storage_Delete_RemovesFile` | After `DeleteAsync` | File no longer exists |
| `Storage_Delete_MissingFileNoThrow` | `DeleteAsync(nonExistent)` | Completes without throwing |

### Test Infrastructure

- One shared `TestDbContextFactory` that builds an `AppDbContext` against EF in-memory or SQLite-in-memory. Reuse across repository tests.
- EF Core in-memory provider honors global query filters — fine for soft-delete tests.
- For tests that must observe `HasDefaultValueSql("GETUTCDATE()")` or other SQL-specific behavior, use SQLite in-memory (closer to relational semantics) and skip the rest.
- `SpotSeasonalRuleService` tests need only a stub `ISpotSeasonalRuleRepository` (in-memory list) — no DbContext required.

---

## Smoke Tests (Manual)

Run against `dotnet run --project src/BoatSpotFinder.Web`. Email confirmation/reset/invite links print to the **console** (`ConsoleEmailSender` is registered in Development).

Audit log file: `logs/audit-YYYY-MM-DD.log` (at repo root).

### Prerequisite for PlaceOwner flows

`AdminController` is not yet implemented (Phase 6), so there is no UI to create marinas or send PlaceOwner invitations. Run the idempotent seed script:

```
sqlcmd -S localhost\SQLEXPRESS -E -i scripts\seed-placeowners.sql
```

(or paste the script into SSMS / Azure Data Studio against the `BoatSpotFinder` DB). It inserts two Marinas + two Invitations using fixed GUIDs, hashes the invite tokens via SHA-256 to match `TokenHasher.Hash`, and refreshes `ExpiresAt` on re-runs. Raw tokens (used in the invite-register URL) are `smoke-marinaA-2026-05-20` and `smoke-marinaB-2026-05-20`.

Then visit `/account/invite-register?token=smoke-marinaA-2026-05-20` to register PlaceOwner A, and the corresponding URL for B to enable §10 ownership checks.

If you don't want to seed, skip §6–§10 and limit smoke testing to BoatOwner + Admin auth flows (§1–§5, §11).

### 1. BoatOwner self-registration

- [ ] Visit `/account/register`, submit valid registration → "check your inbox" page
- [ ] Console shows confirmation email → click link → success → redirects to login with a green `.notice--success` flash ("Your email has been confirmed. You can now sign in.") that disappears on Ctrl+R (TempData consume-on-read)
- [ ] Login with the new account → redirects to `/browse` (Phase 7 hasn't shipped Browse yet, so a 404 there is expected — the redirect target is what's being verified)
- [ ] Top nav shows BoatOwner links (Browse / My Bookings / My Vessels — links 404, expected)
- [ ] Logout via top nav → redirects to home

### 2. Email confirmation edge cases

- [ ] Try to login before confirming → "You must confirm your email before logging in." warning notice with a "Resend confirmation email" link; audit log gains a `LoginFailed_EmailUnconfirmed` entry
- [ ] Click resend → fresh email in console; new link confirms successfully → login again shows the green flash on success
- [ ] Re-register with the same email → Identity blocks (duplicate email error in the form)

### 3. Password reset

- [ ] `/account/forgot-password` with a registered email → "check your email" confirmation page
- [ ] Console shows reset email → click link → reset form rendered with email + token pre-filled
- [ ] Submit new password → success page; old password fails, new works

### 4. Login error paths

- [ ] Wrong password → "Invalid login attempt"
- [ ] Unknown email → same "Invalid login attempt" (anti-enumeration — must NOT distinguish from wrong-password)
- [ ] In SQL, set `AspNetUsers.IsActive = 0` for a user → login shows "Your account has been deactivated." Re-activate (`IsActive = 1`) after.

### 5. Audit log inspection

Tail `logs/audit-YYYY-MM-DD.log` (repo root) and confirm structured JSON entries for:

- [ ] Successful login: `"action":"Login"` with correct `userId` / `userEmail`
- [ ] Logout: `"action":"Logout"` with same identifiers
- [ ] Wrong password (real user): `"action":"LoginFailed_InvalidPassword"` with the real `userId` / `userEmail`
- [ ] Unknown email: `"action":"LoginFailed_UserNotFound"` with empty `userId` and the typed email in `userEmail`
- [ ] Deactivated user: `"action":"LoginFailed_Deactivated"` with the user's identifiers
- [ ] Login attempt before email confirmed: `"action":"LoginFailed_EmailUnconfirmed"` with the user's identifiers
- [ ] (After §6/§7) `SpotCreated` / `SpotEdited` / `SpotDeactivated` / `MarinaEdited` entries

### 6. PlaceOwner marina edit (requires seeded invitation)

- [ ] Visit `/account/invite-register?token={rawToken}` → form, email pre-filled and read-only
- [ ] Submit → redirects to `/placeowner/marinas/{marinaId}/edit`
- [ ] Fill description/address/region/phone/lat/long/default price → save → redirects to `/placeowner/marinas`
- [ ] List shows the marina with its spot count

### 7. Marina Layout Editor + Spots

- [ ] Open Layout for the marina → canvas appears (solid `#e0e0e0` fill if no background)
- [ ] Upload a background image (jpg/png/webp under 5 MB) → page reloads, image visible behind canvas
- [ ] Try uploading a `.txt` file renamed to `.jpg` → `BadRequest("Invalid file type.")` (MIME check)
- [ ] Try uploading a 10 MB image → `BadRequest("File exceeds 5 MB.")`
- [ ] Click "Add Spot" → modal opens (bottom sheet on mobile, centered on ≥720px)
- [ ] Submit name/description/dimensions/price → spot appears in the unplaced sidebar
- [ ] Drag the unplaced spot onto the canvas, resize, rotate → click Save Layout
- [ ] Reload page → positions and rotation persisted; spot now rendered on canvas in blue

### 8. Spot CRUD

- [ ] Visit `/placeowner/marinas/{marinaId}/spots` → list shows all spots including inactive
- [ ] Edit a spot → toggle vessel-type checkboxes, change price → save → updated
- [ ] Re-open Edit → the previously-checked vessel-type checkboxes are checked again (flag round-trip)
- [ ] Deactivate a spot → list now shows it as inactive (gray)
- [ ] Audit log shows `SpotCreated` / `SpotEdited` / `SpotDeactivated` entries with correct `marinaId` and `entityId`

### 9. Seasonal rules

- [ ] `/placeowner/marinas/{marinaId}/spots/{spotId}/seasonal-rules` → empty list
- [ ] Create a rule (Summer 2026 — 2026-06-01 → 2026-09-01, €100, min 3 days) → success
- [ ] Try to create an overlapping rule (2026-08-01 → 2026-10-01) → ModelState error "Date range overlaps with an existing rule."
- [ ] Try a boundary case (2026-09-01 → 2026-10-01 — shares 09-01 with existing) → also rejected (inclusive bounds)
- [ ] Try adjacent (2026-09-02 → 2026-12-31) → accepted
- [ ] Edit the existing rule keeping its dates → no false-positive overlap error
- [ ] Delete a rule → gone

### 10. Ownership enforcement

Requires a second seeded PlaceOwner (Option A above, second invitation for a different marina).

- [ ] As PlaceOwner B, visit `/placeowner/marinas/{marinaA-id}/edit` → 403 Forbidden
- [ ] As PlaceOwner B, visit `/placeowner/marinas/{marinaA-id}/spots` → 403
- [ ] As PlaceOwner B, visit `/placeowner/marinas/{marinaA-id}/spots/{spotA-id}/edit` → 403
- [ ] As PlaceOwner B, visit `/placeowner/marinas/{marinaA-id}/spots/{spotA-id}/seasonal-rules` → 403

### 11. CSRF

- [ ] View source on any form page — confirm `<input name="__RequestVerificationToken" ...>` is present in every `<form>` (tag helper auto-injects it)
- [ ] (Optional) `curl -X POST http://localhost:5000/placeowner/marinas/{id}/edit` without the token → 400 Bad Request

---

## Out of Scope (not yet implementable)

- Browse marina list / public marina view (Phase 7)
- Elasticsearch search (Phase 3b — next)
- Vessel CRUD (Phase 4)
- Booking flow + Hangfire auto-action (Phase 5)
- Reviews & ratings (Phase 5b)
- Admin dashboard / marina creation / PlaceOwner invitations / spot deactivation by Admin (Phase 6)
