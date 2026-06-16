# Testing

Tracks automated tests in `tests/BoatSpotFinder.Tests/` and manual smoke tests, organized by phase. Refreshed at phase milestones — not after every task.

**Scope (refreshed 2026-06-03).** Covers everything implemented through Phase 6 + Phase 5c:
Phase 1 (Foundation), Phase 2 + 2b (Auth + Audit Logging, incl. Admin actions 2b.5), Phase 3 + 3b (PlaceOwner Marina/Spot/SeasonalRules + Elasticsearch indexing), Phase 4 (Vessels), Phase 5 (Booking lifecycle + Hangfire jobs), Phase 5b (Reviews & Ratings), Phase 6 (Admin surface + invitations), Phase 5c (Booking UX & lifecycle refinements — live price preview, departure-after-arrival validation, per-role audit completion, Pending/Confirmed/Past sections + per-side Dismiss).

**Not covered — not yet implemented:** the remainder of Phase 7 (public Browse marina list + ES search UI) and Phase 2c (Audit Log Search & Admin Viewer). The read-only Konva layout viewer slice of Phase 7 (`marina-viewer.js` + `GET /browse/marina/{id}/spot-statuses`) shipped this session, so the Admin read-only canvas in §16 now renders. See § Out of Scope.

**Shipped this session (2026-06-12), verified manually:** admin-console UX (home→dashboard redirect, nav trim, Hangfire embedded at `/admin/jobs`, section back-links, wider layout), login redirect fixes (honor `ReturnUrl`; roleless users → Home not `/browse`), the read-only layout viewer slice, soft-revoke + re-enable marina admins (migration `AddMarinaAdminRevokedAt`), and AJAX dismiss on the booking lists. Unit suite green after the change.

---

## Test Project Status

Current state of `tests/BoatSpotFinder.Tests/` — **116 tests, all green as of 2026-06-16**:

| Item | Value |
|---|---|
| Framework | xUnit |
| Target | `net10.0` |
| Project references | `BoatSpotFinder.Core` + `BoatSpotFinder.Infrastructure` |
| Data layer in tests | SQLite in-memory via `TestDbContextFactory.CreateSqliteInMemory()` — relational semantics; honours global query filters and FK constraints |
| Mocking | `NSubstitute` (search services, `IEmailSender`, `UserManager`, `ILogger`) |

**`TestDbContextFactory`** (`Infrastructure/TestDbContextFactory.cs`) opens a shared `:memory:` SQLite connection, builds an `AppDbContext`, and calls `EnsureCreated()`. Each test gets a fresh isolated DB via `using var db = TestDbContextFactory.CreateSqliteInMemory();`.

**Gotcha pinned by the suite:** `AppDbContext.SetTimestamps()` overwrites `CreatedAt`/`UpdatedAt` on every `SaveChangesAsync`, so any test that depends on distinct `CreatedAt` values must save rows in separate `SaveChangesAsync` calls with a small delay between them (see `ReviewRepositoryTests.GetRecentByMarinaIdAsync_OrdersByCreatedAtDescAndRespectsCount`).

---

## Automated Tests

### Implemented (116 tests)

Run all: `dotnet test BoatSpotFinder.slnx`.

| Test file | Count | Phase | What it pins |
|---|---|---|---|
| `Helpers/TokenHasherTests.cs` | 2 | 2 | SHA-256 hash is deterministic; different inputs → different hashes |
| `Repositories/InvitationRepositoryTests.cs` | 3 | 2 | `GetByTokenHashAsync` returns the row regardless of `IsUsed`/`ExpiresAt` — the caller does the filtering |
| `Repositories/SpotRepositoryTests.cs` | 3 | 3 | `GetByIdAsync` returns inactive (ignores filter); `GetActiveByIdAsync` respects it; include-inactive listing |
| `Services/SpotSeasonalRuleServiceTests.cs` | 6 | 3 | Overlap predicate — exact/partial/boundary overlap rejected, adjacent-next-day allowed, update excludes self |
| `Infrastructure/LocalFileStorageServiceTests.cs` | 1 | 3 | Storage save/delete + stream-ownership contract |
| `Services/BookingServiceTests.cs` | 45 | 5/5c | **Create** (overlap/strict-adjacency, pricing cascade spot→seasonal→marina-default, vessel fit + type flags, min-days); **Cancel** for BoatOwner/PlaceOwner/Admin (StartDate guard + Admin skip, Forbidden, terminal-status reject); **Confirm/Reject** ownership gate + transition + email + non-Pending reject; **`AutoActionAsync`** (auto-approve/reject + timeout filter) and **`CompleteOverdueAsync`** (overdue→Completed + review-invite email fan-out) — the two Hangfire jobs; **`PreviewPrice`** (no persist); **`DismissAsync`** (owner-only + past/cancelled/elapsed guard, hide-not-delete) and **`DismissByMarinaAsync`** (marina-admin authorization + past/cancelled/elapsed guard) — the Phase 5c per-side dismiss flags |
| `Services/ReviewServiceTests.cs` | 14 | 5b | `CanReviewAsync` gates (not-found / not-completed / window closed / **14-day boundary inclusive** / BoatOwner / PlaceOwner-via-MarinaAdmin / unrelated / already-reviewed); `CreateReviewAsync` persist + marina & boat-owner rating recompute + averaging + ES `IndexAsync` call + **ES-failure-is-swallowed** |
| `Repositories/ReviewRepositoryTests.cs` | 5 | 5b | `ExistsAsync` role discrimination; `GetAllByMarinaId`/`GetAllByBoatOwnerId` role+scope filtering; `GetRecentByMarinaId` ordering + count |
| `Repositories/MarinaRepositoryTests.cs` | 6 | 3/6/6b | `GetByUserIdAsync` MarinaAdmin join + **excludes revoked membership** (6b); `GetActiveWithActiveSpotsAsync` excludes empty/inactive, applies id filter, returns marina with an active spot |
| `Repositories/MarinaAdminRepositoryTests.cs` | 6 | 3/6/6b | `ExistsAsync` present/absent + **revoked-excluded** (6b); `UpdateAsync` Revoke→Reinstate round-trip toggles `ExistsAsync` (6b); `GetByUserId`/`GetByMarinaId` filtering |
| `Repositories/BookingRepositoryTests.cs` | 10 | 5/6b | `IsSpotAvailableAsync` overlap / strict-adjacency / Cancelled-ignored / exclude-self; `GetByMarinaOwnerIdAsync` Booking→Spot→MarinaAdmin join; `GetByBoatOwnerIdAsync` filter; **`GetOccupiedSpotIdsAsync`** ×4 (6b — Pending/Confirmed-overlapping returned, Cancelled/non-overlapping excluded, marina-scoped, distinct-per-spot) |
| `Repositories/VesselRepositoryTests.cs` | 3 | 4 | `GetByOwnerIdAsync` owner filtering; null-for-missing; delete |
| `Repositories/AdminSettingsRepositoryTests.cs` | 2 | 6 | Seeded-singleton fetch; `UpdateSettings` round-trip (the config the auto-action job reads) |
| `Controllers/BrowseControllerTests.cs` | 4 | 6b/7 | `SpotStatuses` Free/Booked/Unavailable mapping (active+unbooked / active+overlapping-today booking / inactive) + marina-not-found 404; `LayoutData` marina+spots mapping + 404 |
| `Controllers/AdminControllerTests.cs` | 6 | 6b | `RevokeAdmin` strips PlaceOwner role on last active membership / keeps it when another active membership remains / 404; `ReEnableAdmin` re-adds role when missing / skips when present / 404 — asserts both the `UserManager` role calls and the persisted `RevokedAt` state |

### Not yet automated (gaps — covered by smoke tests)

High-value units without direct automated coverage, exercised via the smoke sections below:

- **Phase 1** — `AppDbContext.SetTimestamps` on insert/update. No standalone test; behaviour is exercised indirectly throughout and pinned via the ordering gotcha noted above.
- **Phase 2** — `CustomSignInManager` inactive-user rejection. Needs an Identity harness; verified via smoke §4.
- **Phase 2b** — `NLogAuditLogger` structured-field output. Needs a memory/list NLog sink; verified via smoke §5 / §17 by tailing the log file.
- **Phase 5 / 6 controllers** — `BookingsController`, `SpotBookingsController`, `AdminController` action wiring + ownership / `Forbid()` paths. Verified via smoke §13 / §16.
- **Phase 3b** — Elasticsearch marina indexing + search round-trip needs a live ES node (integration test). The null-stub sentinel path and the `GetActiveWithActiveSpotsAsync` query Browse will consume are covered by the repository tests above.
- **Covered 2026-06-12 (Phase 6b):** `BookingRepository.GetOccupiedSpotIdsAsync`, `MarinaAdminRepository.ExistsAsync` active-only + `UpdateAsync`, and `MarinaRepository.GetByUserIdAsync` active-only now have repo tests (see the table above, +7 → 106).
- **Covered 2026-06-16 (first controller tests):** the project's first controller-level tests landed in a new `Controllers/` folder (+10 → 116). `BrowseController.SpotStatuses`/`LayoutData` JSON mapping + 404s, and `AdminController.RevokeAdmin`/`ReEnableAdmin` role-strip orchestration (asserts the `UserManager` role calls **and** the persisted `RevokedAt` state). A `DefaultHttpContext` + `ClaimsPrincipal` + `TempDataDictionary` harness was introduced for the Admin controller tests. Still smoke-only: `AccountController.Login` `ReturnUrl`/roleless→Home, `CustomSignInManager` inactive-user, and `NLogAuditLogger` structured output — each needs a heavier Identity/logging harness, low ROI over the smoke sections.
- **Login redirect (this session)** — `AccountController.Login` POST `ReturnUrl` (`Url.IsLocalUrl` guard) and the roleless→Home fallback are controller paths (no Identity harness in the suite); verified via smoke.

### Test infrastructure notes

- All repository/service tests share `TestDbContextFactory.CreateSqliteInMemory()` (SQLite in-memory — closer to relational/SQL-Server semantics than the EF in-memory provider; honours global query filters and FK constraints).
- `SpotSeasonalRuleServiceTests` builds the service against the real repository over SQLite; service tests (`BookingService`, `ReviewService`) wire **real** repositories + `NSubstitute` for search services, `IEmailSender`, `UserManager`, and `ILogger`.
- `UserManager<ApplicationUser>` is substituted via `Substitute.For<UserManager<…>>(store, null × 8)`; configure `FindByIdAsync` to return a held instance so post-call rating mutations are observable.

---

## Smoke Tests (Manual)

Run against `dotnet run --project src/BoatSpotFinder.Web`. Email confirmation/reset/invite links print to the **console** (`ConsoleEmailSender` is registered in Development).

Audit log file: `logs/audit-YYYY-MM-DD.log` (at repo root).

### Prerequisite for PlaceOwner flows

**Phase 6 update:** the Admin UI now provisions PlaceOwners end-to-end — log in as the seeded admin, create a marina, and send an invite (see §16). That is the preferred path. The seed script below remains a faster shortcut for setting up §6–§10, and is how those sections were originally run.

Run the idempotent seed script:

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

## Smoke Tests (Manual) — Phases 4, 5, 5b, 6 (not yet run)

Sections §12–§17 cover everything shipped since the Phase 3 milestone. **Status (recorded from 2026-06-12 session memory):** §13 (lifecycle), §14 (Hangfire jobs), §15 (reviews & ratings), and §16 (admin surface, steps 1–11) passed live and are checked off below. §12 (vessels) passed 2026-06-16. §17 fully verified 2026-06-16 via a full audit-log catalog scan across the 06-03 / 06-12 / 06-16 logs — every catalogued action present with the correct `details` payload, and `userRole` never populated. Booking/review/admin flows print email links to the **console** (`ConsoleEmailSender` in Development). Audit entries land in `logs/audit-YYYY-MM-DD.log`.

Setup assumed: one BoatOwner account (§1), one active marina with at least one **active** spot owned by a PlaceOwner (§6–§8 or via §16), and the seeded admin (`admin@boatspotfinder.com`).

### 12. Vessel management (Phase 4) — BoatOwner

- [x] Log in as a BoatOwner → `/vessels` → list (empty initially)
- [x] `/vessels/create` → the `Type` dropdown lists vessel types **excluding `None`**, with integer `value` attributes (flag-enum binding). Submit name/type/dimensions → redirect to `/vessels`, vessel listed
- [x] `/vessels/edit/{id}` → change a field → save → updated values shown
- [x] Delete a vessel with **no bookings** → removed from list
- [x] Delete a vessel with a `Pending`/`Confirmed` booking → redirected to `/vessels` with `TempData["Error"]` "Cannot delete a vessel with active bookings"; vessel remains
- [x] Delete a vessel that has only `Cancelled`/`Completed` bookings → allowed (SQL `SET NULL` cascades `Booking.VesselId` to null; those rows show "Vessel deleted")

### 13. Booking creation & lifecycle (Phase 5)

Booking create is reached directly at `/bookings/create?spotId={id}` (the Browse canvas that normally links here is Phase 7, not built — use the URL).

- [x] BoatOwner with **no vessel** visits `/bookings/create?spotId={activeSpotId}` → redirect to `/vessels/create` with `TempData["Error"]` "Please register a vessel before making a booking"
- [x] `/bookings/create?spotId={activeSpotId}` with a vessel → form renders; selecting vessel + start + end shows the **price preview** block (only when vesselId+start+end are all present and the vessel belongs to the current user)
- [x] Submit a valid booking → status `Pending`; redirect to `/bookings`; row listed as Pending. Console shows a "New booking request for {spot}" email to the marina admins
- [x] Create a second booking overlapping the first → rejected, ModelState error contains "not available" (adjacent back-to-back dates, where one ends the day the next starts, are **allowed**)
- [x] Vessel larger than the spot, or a type not in `AllowedVesselTypes` → rejected with a dimensions/type error
- [x] Booking shorter than the resolved min-booking-days → rejected ("minimum")
- [x] Deactivate the spot, then `/bookings/create?spotId={inactiveSpotId}` → `NotFound` (uses `GetActiveByIdAsync`)
- [x] As PlaceOwner, `/placeowner/spot-bookings` (Incoming) lists the Pending booking → **Confirm** → status Confirmed; console email to BoatOwner. On another booking → **Reject** → Cancelled; console email to BoatOwner
- [x] BoatOwner cancels a future Pending/Confirmed booking via the Cancel button (`POST /bookings/{id}/cancel`) → Cancelled. Cancelling a booking whose `StartDate` is today/past → rejected with `TempData["Error"]`
- [x] PlaceOwner cancels a future booking via `/placeowner/spot-bookings/{id}/cancel` → Cancelled; console email to BoatOwner

### 14. Hangfire recurring jobs (Phase 5 / 5b)

- [x] `/hangfire` opens **only as Admin** (other roles redirect to login / are denied). Two recurring jobs are registered: `booking-auto-action` (`*/5 * * * *`) and `booking-complete-overdue` (`0 2 * * *`)
- [x] **Auto-action:** in Admin → Settings set `AutoActionTimeoutHours` low; create a Pending booking older than the timeout (or wait); "Trigger now" `booking-auto-action` → booking auto-Confirms or auto-Cancels per `AutoActionType`; console email fires
- [x] **Complete-overdue:** set a `Confirmed` booking's `EndDate` to yesterday (SQL), "Trigger now" `booking-complete-overdue` → status → `Completed`; console shows **two** review-invite emails — BoatOwner (`/reviews/create?bookingId=…`) and each marina admin (`/placeowner/reviews/create?bookingId=…`), each noting a 14-day deadline (`EndDate + 14`)

### 15. Reviews & ratings (Phase 5b)

Prereq: a `Completed` booking (from §14).

- [x] BoatOwner opens `/reviews/create?bookingId={id}` → star form + booking summary, "Rate the marina". Submit score 1–5 (+ optional comment) → redirect to `/bookings`; the completed row now shows the submitted score instead of the CTA
- [x] After the BoatOwner review, the marina's `AverageRating` / `ReviewCount` update (verify in SQL; the public marina page is Phase 7)
- [x] PlaceOwner opens `/placeowner/reviews/create?bookingId={id}` → "Rate the boat owner". Submit → redirect to `/placeowner/spot-bookings`; the BoatOwner's `AverageRatingAsBoatOwner` / `ReviewCountAsBoatOwner` update; Incoming rows show the BoatOwner rating
- [x] A booking whose `EndDate` is more than 14 days ago → `/reviews/create?bookingId={id}` returns **404** (window closed)
- [x] Revisit the same review link after submitting → **404** (already reviewed). A second marina admin clicking their invite after the first already reviewed → 404 (benign first-to-submit race)
- [x] `logs/audit-*.log` gains a `ReviewCreated` entry (PlaceOwner review) with `details` `{ score, bookingId }`

### 16. Admin surface (Phase 6) — Admin

Log in as the seeded admin (`admin@boatspotfinder.com`).

- [x] `/admin/dashboard` → console grid of navigation cards
- [x] `/admin/users` → all users; BoatOwner rows show `AverageRatingAsBoatOwner`
- [x] `/admin/marinas` → all marinas **including inactive** (status pill, admin count, spot count)
- [x] `/admin/marinas/create` (name + region) → on save redirects to the **InviteAdmin** form for the new marina (new marina is **not** indexed in ES at creation)
- [x] On the invite form enter an email → POST → redirect to MarinaInvitations; console shows the invite email with `/account/invite-register?token=…`. Register via that link → new PlaceOwner appears under `/admin/marinas/{id}/admins`
- [x] `/admin/marinas/{id}/edit` → change details → save (ES `IndexAsync` only when the marina is active)
- [x] `/admin/marinas/{id}/toggle-active` → deactivate (ES `DeleteAsync`) then reactivate (ES `IndexAsync`); existing bookings are **not** cancelled; marina is never hard-deleted
- [x] `/admin/marinas/{id}/spots` → lists spots **including inactive**; toggle a spot via `/admin/spots/{id}/toggle-active`
- [x] `/admin/marinas/{id}/layout` → read-only canvas now renders via `marina-viewer.js`: placed spots drawn colored by status — **Free** (green), **Booked** (amber: active spot with a Pending/Confirmed booking overlapping today), **Unavailable** (grey: inactive) — with labels + the background image; legend shows Free / Booked / Unavailable. Status from `GET /browse/marina/{id}/spot-statuses`. Verified this session.
- [x] Revoke an admin via `/admin/marinas/{marinaId}/admins/{userId}/revoke` → membership removed; if it was the user's **last** membership, the PlaceOwner role is stripped (confirm they can no longer reach `/placeowner/marinas`)
- [x] From `/admin/bookings`, Cancel a Pending/Confirmed booking → Cancelled (Admin override **skips** the StartDate guard); `TempData["Success"]` "Booking cancelled."
- [x] `/admin/settings` → change `AutoActionType` + `AutoActionTimeoutHours` → save → success flash

### 17. Audit log — Admin & state-changing actions (Phase 2b full)

Tail `logs/audit-YYYY-MM-DD.log` and confirm structured JSON entries (`action` / `entityType` / `entityId` / `marinaId` / `details`) for:

- [x] Admin: `MarinaCreated` `{ name }`, `MarinaActivated` / `MarinaDeactivated`, `SpotActivated` / `SpotDeactivated`, `AdminInvited` `{ email }`, `AdminRevoked`, `BookingCancelledByAdmin` `{ previousStatus }`, `SettingsUpdated`
- [x] PlaceOwner: `BookingConfirmed` / `BookingRejected` (with `booking.Spot.MarinaId`)
- [x] PlaceOwner: `ReviewCreated` `{ score, bookingId }`
- [x] `userRole` is always blank (reserved field — documented in `docs/features/audit-logging.md`)

---

## Smoke Tests (Manual) — Phase 5c (Booking refinements, 2026-06-03)

Covers the booking-create + My Bookings + Incoming improvements shipped this session. **Restart the app first** so the auto-migration applies `AddBookingDismissedByOwner` + `AddBookingDismissedByMarina`. Checked items were confirmed live this session; unchecked remain to run.

### 18. Booking create — live preview + date validation (BoatOwner)

- [x] On `/bookings/create?spotId=…`, selecting a vessel + arrival + departure shows a live **price estimate** with no page reload (fetched from `GET /bookings/preview-price`; server-side `PreviewPriceAsync` unchanged)
- [x] Setting departure ≤ arrival → inline error "Departure must be after the arrival date.", the estimate hides, and submit is blocked client-side
- [x] Server-side remains authoritative: `BookingCreateViewModel : IValidatableObject` + the `BookingService.CreateAsync` `end <= start` guard reject a bad range even if JS is bypassed; the form keeps `novalidate`

### 19. My Bookings — sections + Booked-on + Dismiss (BoatOwner)

- [x] Page renders three sections — **Pending → Confirmed → Past** (muted) — Pending/Confirmed sorted by arrival ascending; each card shows a "Booked on …" line
- [x] Empty sections are omitted; the big empty-state shows only when all three are empty
- [x] **Dismiss** on a Past/Cancelled card removes it from the list; the row is NOT deleted — verify `DismissedByOwner = 1` in SQL and that the booking is still visible to the PlaceOwner/Admin

### 20. Incoming — sections + marina-wide Dismiss + nav (PlaceOwner)

- [x] The PlaceOwner nav menu now shows **Incoming Bookings** (`/placeowner/spot-bookings`) alongside My Marinas
- [x] Incoming renders the same three sections (Pending → Confirmed → Past), sorted by arrival; the richer card (requester, boat-owner rating, auto-decision countdown, Confirm/Reject/Cancel) is preserved
- [x] **Dismiss** on a Past/Cancelled card → marina-wide: hidden for every admin of that marina (`DismissedByMarina = 1`); row not deleted; a non-admin of the marina cannot dismiss (service `Forbidden`)

### 21. Booking audit trail completion (ties into §13 / §17)

- [x] Creating a booking writes `BookingCreated` (`details { spotId, startDate, endDate, totalPrice }`)
- [x] BoatOwner cancel writes `BookingCancelledByBoatOwner`; PlaceOwner cancel writes `BookingCancelledByPlaceOwner` — both with `entityType:"Booking"` + `marinaId`; dismiss writes NO audit entry (it's a per-side view preference)

---

## Out of Scope (not yet implementable)

- **Phase 7 — Browse & Search (remainder):** public marina list, ES-backed search box, click-to-book. The read-only Konva canvas viewer + spot-status colors slice shipped this session (`marina-viewer.js`, `GET /browse/marina/{id}/spot-statuses`), so the Admin read-only canvas in §16 now renders. Elasticsearch marina **search** is reachable only once the rest of Phase 7 ships — indexing-on-write and the startup seed (Phase 3b) are already in place.
- **Phase 2c — Audit Log Search & Admin Viewer:** `eventId` idempotency, file→ES reindex Hangfire jobs, rolling 30-day window, `/admin/audit-log` grid. Planned only.
- **Phase 8 — Polish:** pagination, error pages, Docker, k8s.
