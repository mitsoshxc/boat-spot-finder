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

(or paste the script into SSMS / Azure Data Studio against the `BoatSpotFinder` DB). It inserts two Marinas + two Invitations using fixed GUIDs, hashes the invite tokens via SHA-256 to match `TokenHasher.Hash`, and refreshes `ExpiresAt` on re-runs. Note: `AccountController.InviteRegister` is now reachable at `/account/invite-register` (hyphenated) thanks to the explicit `[Route]` attribute shipped this session — the URL in the seed script comments works as documented. Raw tokens (used in the invite-register URL) are `smoke-marinaA-2026-05-20` and `smoke-marinaB-2026-05-20`.

Then visit `/account/invite-register?token=smoke-marinaA-2026-05-20` to register PlaceOwner A, and the corresponding URL for B to enable §10 ownership checks.

If you don't want to seed, skip §6–§10 and limit smoke testing to BoatOwner + Admin auth flows (§1–§5, §11).

### 1. BoatOwner self-registration

- [x] Visit `/account/register`, submit valid registration → "check your inbox" page
- [x] Console shows confirmation email → click link → success → redirects to login with a green `.notice--success` flash ("Your email has been confirmed. You can now sign in.") that disappears on Ctrl+R (TempData consume-on-read)
- [x] Login with the new account → redirects to `/browse` (Phase 7 hasn't shipped Browse yet, so a 404 there is expected — the redirect target is what's being verified)
- [x] Top nav shows BoatOwner links (Browse / My Bookings / My Vessels — links 404, expected)
- [x] Logout via top nav → redirects to home

### 2. Email confirmation edge cases

- [x] Try to login before confirming → "You must confirm your email before logging in." warning notice with a "Resend confirmation email" link; audit log gains a `LoginFailed_EmailUnconfirmed` entry
- [x] Click resend → fresh email in console; new link confirms successfully → login again shows the green flash on success
- [x] Re-register with the same email → Identity blocks (duplicate email error in the form)

### 3. Password reset

- [x] `/account/forgot-password` with a registered email → "check your email" confirmation page
- [x] Console shows reset email → click link → reset form rendered with email + token pre-filled
- [x] Submit new password → success page; old password fails, new works

### 4. Login error paths

- [x] Wrong password → "Invalid login attempt"
- [x] Unknown email → same "Invalid login attempt" (anti-enumeration — must NOT distinguish from wrong-password)
- [x] In SQL, set `AspNetUsers.IsActive = 0` for a user → login shows "Your account has been deactivated." Re-activate (`IsActive = 1`) after.

### 5. Audit log inspection

Tail `logs/audit-YYYY-MM-DD.log` (repo root) and confirm structured JSON entries for:

- [x] Successful login: `"action":"Login"` with correct `userId` / `userEmail`
- [x] Logout: `"action":"Logout"` with same identifiers
- [x] Wrong password (real user): `"action":"LoginFailed_InvalidPassword"` with the real `userId` / `userEmail`
- [x] Unknown email: `"action":"LoginFailed_UserNotFound"` with empty `userId` and the typed email in `userEmail`
- [x] Deactivated user: `"action":"LoginFailed_Deactivated"` with the user's identifiers
- [x] Login attempt before email confirmed: `"action":"LoginFailed_EmailUnconfirmed"` with the user's identifiers
- [x] (After §6/§7) `SpotCreated` / `SpotEdited` / `SpotDeactivated` / `SpotActivated` / `SpotDeleted` / `MarinaEdited` entries (verified during §7–§8; all action strings confirmed in `src/BoatSpotFinder.Web/Controllers/SpotsController.cs` and `MarinasController.cs`)

### 6. PlaceOwner marina edit (requires seeded invitation)

- [x] Visit `/account/invite-register?token={rawToken}` → form, email pre-filled and read-only
- [x] Submit → redirects to `/account/login` showing the green `.notice--success` flash "Your account has been created. You can now sign in."; sign in as the invited PlaceOwner → lands on `/placeowner/marinas`
- [x] From the marina list, click the marina → fill description/address/region/phone/lat/long/default price → save → redirects to `/placeowner/marinas` with updated values
- [x] List shows the marina with its spot count

### 7. Marina Layout Editor + Spots

- [x] Open Layout for the marina → canvas appears (solid `#e0e0e0` fill if no background)
- [x] Upload a background image (jpg/png/webp under 5 MB) → page reloads, image visible behind canvas
- [x] Try uploading a `.txt` file renamed to `.jpg` → JS validation blocks submit, inline error "Please choose a JPG, PNG, or WebP image." (or the extension error) shown below the upload row. Server-side validation in `MarinasController.UploadBackground` is kept as defense-in-depth (returns `BadRequest` if JS is bypassed).
- [x] Try uploading a > 5 MB image → JS validation blocks submit, inline error "File size must be 5 MB or less." shown below the upload row.
- [x] Try uploading a real `.png` renamed to `.bmp` (extension fake-out) → JS blocks submit, error "File extension must be .jpg, .jpeg, .png, or .webp."
- [x] Click "Add Spot" → modal opens (bottom sheet on mobile, centered card on ≥720px)
- [x] Modal includes a `Minimum booking · days` field (default 1, range 1–365). Submit name/description/dimensions/price/min-booking → spot appears in the unplaced sidebar with an `Unplaced` pill; sidebar count badge increments live (no reload); empty-state paragraph removed automatically.
- [x] Spots on the canvas render in dock grey (`#6B7684` slate-soft fill, `#3C4654` slate stroke). Unplaced spots have a dashed stroke; placed-Active spots are solid; placed-Inactive stay neutral grey.
- [x] Drag the unplaced spot onto the canvas, resize, rotate → click Save Layout → "Saved ✓" flash appears for ~2 seconds on the button
- [x] Reload page → positions and rotation persisted; spot now rendered solid (Active) on canvas; sidebar pill flips from `Unplaced` to `Active`

### 8. Spot CRUD

- [x] Visit `/placeowner/marinas/{marinaId}/spots` → list shows all spots including inactive
- [x] Edit a spot → toggle vessel-type checkboxes, change price → save → updated
- [x] Re-open Edit → the previously-checked vessel-type checkboxes are checked again (flag round-trip)
- [x] Deactivate a spot → list now shows it as inactive (gray)
- [x] On the Spot Edit page, when a spot is inactive, an "Activate this spot" primary button (`btn--primary btn--sm`, `class="activate-form"`) replaces the Deactivate button. Click → POSTs to `/placeowner/marinas/{marinaId}/spots/{id}/activate` → spot becomes active; audit log gains a `SpotActivated` entry. (`src/BoatSpotFinder.Web/Views/Spots/Edit.cshtml` if/else block; `SpotsController.Activate` already existed — commit `c96bd60` wires up the view branch.)
- [x] On the Edit page, scroll past the Save/Deactivate buttons to find "Delete this spot permanently" (red `.btn--danger`). For a spot with **no bookings**: click → browser `confirm()` dialog → OK → redirects to `/placeowner/marinas/{marinaId}/spots`, spot is gone from the DB (verify with `SELECT * FROM Spots WHERE Id = '<id>'`).
- [x] For a spot **with bookings** (hand-insert a Booking row via SQL if needed to test): click Delete → redirects back to Edit, `.notice notice--warning` shows "Cannot delete a spot that has bookings. Deactivate it instead..."; spot remains in DB.
- [x] Inactive spots are also deletable: Deactivate first, then Delete works the same as active. Confirm both the Deactivate and Delete forms POST to `/placeowner/marinas/{marinaId}/spots/{id}/{deactivate|delete}` (the explicit `asp-route-marinaId` ensures URL generation doesn't fall back to the conventional `/Spots/{action}/{id}` 404).
- [x] Audit log shows `SpotCreated` / `SpotEdited` / `SpotDeactivated` / `SpotActivated` / `SpotDeleted` entries with correct `marinaId` and `entityId`

### 9. Seasonal rules

Note: `SpotSeasonalRuleCreateViewModel.StartDate` and `EndDate` are `DateOnly?` (commit `e3fbf3d`, `src/BoatSpotFinder.Web/Models/SpotSeasonalRuleCreateViewModel.cs`). The Create form's `<input type="date">` fields render blank (no `value` attribute), so the native browser picker opens on today's date instead of `0001-01-01`. `[Required]` still rejects null on submit; `SpotSeasonalRulesController.Create` POST unwraps with `.Value` after `ModelState.IsValid` (`src/BoatSpotFinder.Web/Controllers/SpotSeasonalRulesController.cs` line 102). The Edit ViewModel uses non-nullable `DateOnly` (dates are always populated from the DB) — unchanged.

- [x] `/placeowner/marinas/{marinaId}/spots/{spotId}/seasonal-rules` → empty list
- [x] Create a rule (Summer 2026 — 2026-06-01 → 2026-09-01, €100, min 3 days) → success
- [x] Try to create an overlapping rule (2026-08-01 → 2026-10-01) → ModelState error "Date range overlaps with an existing rule."
- [x] Try a boundary case (2026-09-01 → 2026-10-01 — shares 09-01 with existing) → also rejected (inclusive bounds)
- [x] Try adjacent (2026-09-02 → 2026-12-31) → accepted
- [x] Edit the existing rule keeping its dates → no false-positive overlap error
- [x] Delete a rule → gone

### 10. Ownership enforcement

Requires a second seeded PlaceOwner (Option A above, second invitation for a different marina).

- [x] As PlaceOwner B, visit `/placeowner/marinas/{marinaA-id}/edit` → 403 Forbidden
- [x] As PlaceOwner B, visit `/placeowner/marinas/{marinaA-id}/spots` → 403
- [x] As PlaceOwner B, visit `/placeowner/marinas/{marinaA-id}/spots/{spotA-id}/edit` → 403
- [x] As PlaceOwner B, visit `/placeowner/marinas/{marinaA-id}/spots/{spotA-id}/seasonal-rules` → 403

### 11. CSRF

- [x] View source on any form page — confirm `<input name="__RequestVerificationToken" ...>` is present in every `<form>` (tag helper auto-injects it)
- [x] (Optional) Curl `POST /placeowner/marinas/{id}/edit` without a token returned **HTTP/2 302** redirecting to `/account/login` — the authentication filter fires before the antiforgery filter in the pipeline, so the redirect is expected and correct. CSRF defence is confirmed via §11.1 (token field present in every form) combined with the globally registered `AutoValidateAntiforgeryTokenAttribute` in `Program.cs` (`src/BoatSpotFinder.Web/Program.cs` line 36–37), which validates on all state-changing verbs.

---

## Out of Scope (not yet implementable)

- Browse marina list / public marina view (Phase 7)
- Elasticsearch search (Phase 3b — next)
- Vessel CRUD (Phase 4)
- Booking flow + Hangfire auto-action (Phase 5)
- Reviews & ratings (Phase 5b)
- Admin dashboard / marina creation / PlaceOwner invitations / spot deactivation by Admin (Phase 6)
