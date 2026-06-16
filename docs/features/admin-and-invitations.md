# Admin Features and PlaceOwner Invitation Lifecycle

Covers the Admin role's management surface (Phase 6), the admin-console UX shell, and the invite-based flow through which new PlaceOwners are created, soft-revoked, and re-enabled.

---

## Admin Console UX

The Admin role gets a dedicated console shell layered on top of the shared `_Layout.cshtml`.

| Behaviour | Implementation |
|---|---|
| Home redirect | `HomeController.Index` (`src/BoatSpotFinder.Web/Controllers/HomeController.cs`) redirects signed-in users per role before rendering the public landing view: Admin → `RedirectToAction("Dashboard", "Admin")` (`/admin/dashboard`); PlaceOwner → `RedirectToAction("Index", "Marinas")` (`/placeowner/marinas`); BoatOwner → `Redirect("/browse")`; guests and users with no role fall through to the public landing `View()`. The brand logo links to `HomeController.Index`, so clicking it always applies this per-role redirect. |
| Top nav trim | `_Layout.cshtml`: the "Home" `<li>` was **removed entirely** — no role sees it. The redundant "Browse" (BoatOwner) and "My Marinas" (PlaceOwner) nav links were also removed. Net nav per role: Admin = Settings (`/admin/settings`) · Hangfire (`/admin/jobs`); PlaceOwner = Incoming Bookings; BoatOwner = My Bookings · My Vessels. |
| Account dropdown trim | `_LoginPartial.cshtml`: the "My Bookings" and "Favorites" placeholder items (both `href="#"`) and their divider were removed for all roles. Every signed-in user now sees **Sign out only** in the dropdown. |
| Hangfire embed | `AdminController.Jobs()` (`[HttpGet("jobs")]`) returns `Views/Admin/Jobs.cshtml`, which sets `ViewData["MainModifier"] = "site-main--flush"` and renders `<iframe class="embed__frame" src="/hangfire" title="Hangfire dashboard">`. `_Layout.cshtml`'s `<main>` tag appends `@(ViewData["MainModifier"])` to its class list, so this page renders full-bleed (`.site-main--flush { padding: 0; display: flex }`, `.embed__frame { flex: 1; width: 100%; border: 0; min-height: 0 }` in `site.css`). The Hangfire dashboard at `/hangfire` returns `X-Frame-Options: SAMEORIGIN`, which permits this same-origin iframe. |
| Section back-links | All 11 Admin section views render a `.workspace__back` link as the first child of `<header class="workspace__head">`: `Users.cshtml`, `AllBookings.cshtml`, `AllMarinas.cshtml`, `Settings.cshtml` → "Back to dashboard" (`/admin/dashboard`); `CreateMarina.cshtml`, `EditMarina.cshtml`, `MarinaSpots.cshtml`, `MarinaAdmins.cshtml`, `MarinaInvitations.cshtml`, `MarinaLayout.cshtml`, `InviteAdmin.cshtml` → "Back to marinas" (`/admin/marinas`). `Settings.cshtml` also dropped its redundant bottom "Back to console" link. Styling: `.workspace__back` in `site.css`. |
| Widened content column | `.site-header__inner`, `.site-main`, and `.site-footer` max-width raised from 1180px to 1320px in `site.css`, kept aligned across header/main/footer. |

---

## Admin Role Surface

`AdminController` (`src/BoatSpotFinder.Web/Controllers/AdminController.cs`) is decorated `[Authorize(Roles="Admin")]` and routed at `[Route("admin")]`. It is the only controller through which the Admin role acts on the application's data.

### Read actions

| Route | Action | Data source |
|---|---|---|
| `GET admin/dashboard` | Dashboard | Static view — no repository call |
| `GET admin/jobs` | Jobs | Static view — embeds `/hangfire` in a same-origin iframe (see § Admin Console UX above) |
| `GET admin/users` | Users | `UserManager.Users` (all users); `GetRolesAsync` per user; `AverageRatingAsBoatOwner` shown only for BoatOwner-role users |
| `GET admin/bookings` | AllBookings | `IBookingRepository.GetAllAsync()`, ordered newest-first; Cancel button rendered for Pending and Confirmed rows |
| `GET admin/marinas` | AllMarinas | `IMarinaRepository.GetAllAsync(includeInactive: true)` — includes inactive marinas; admin count (active memberships only — see § 3 below) + spot count computed per marina |
| `GET admin/marinas/{id}/spots` | MarinaSpots | `ISpotRepository.GetByMarinaIdAsync(marinaId, includeInactive: true)` — bypasses the global `IsActive` query filter without `IgnoreQueryFilters()` in the controller (the `includeInactive` flag handles it inside the repository) |
| `GET admin/marinas/{id}/admins` | MarinaAdmins | `IMarinaAdminRepository.GetByMarinaIdAsync`; each member's email resolved via `UserManager.FindByIdAsync`; rows ordered active-first (`OrderBy(a => a.IsRevoked).ThenBy(a => a.InvitedAt)`) |
| `GET admin/marinas/{id}/invitations` | MarinaInvitations | `IInvitationRepository.GetByMarinaIdAsync`; status derived in controller: `IsUsed` → Accepted, `ExpiresAt < UtcNow` → Expired, else Pending |
| `GET admin/marinas/{id}/layout` | MarinaLayout | Read-only Konva canvas via `marina-viewer.js`; `#canvas-container` wired with `data-marina-id` + `data-spot-statuses-url` pointing at `Browse.SpotStatuses`; includes inactive spots; no vessel/date inputs. Functional as of this session — see [`architecture.md`](../architecture.md) § Frontend / Design System. |
| `GET admin/settings` | Settings GET | `IAdminSettingsRepository.GetAsync()` → `AdminSettingsViewModel` |

### Write actions

| Route | Action | Effect |
|---|---|---|
| `GET/POST admin/marinas/create` | CreateMarina | Creates a `Marina` with empty/zero defaults for non-required fields; NOT indexed in Elasticsearch at creation; redirects to InviteAdmin GET for the new marina |
| `GET/POST admin/marinas/{id}/edit` | EditMarina | `marina.UpdateDetails(...)` preserving existing `LayoutWidth`/`LayoutHeight`; `UpdateAsync`; calls `IndexAsync` only when `marina.IsActive == true`; BadRequest when route id and model id differ |
| `POST admin/marinas/{id}/toggle-active` | ToggleMarinaActive | Deactivates → `Deactivate()` + `DeleteAsync(marina.Id)` from ES; activates → `Activate()` + `IndexAsync`; `UpdateAsync`; existing bookings are NOT cancelled; never hard-deletes the marina |
| `POST admin/spots/{id}/toggle-active` | ToggleSpotActive | `Activate()` / `Deactivate()` on the spot; `UpdateAsync`; redirects to MarinaSpots using `spot.MarinaId` |
| `GET/POST admin/marinas/{id}/invite` | InviteAdmin | Creates and emails an invite (see Invitation Lifecycle below) |
| `POST admin/marinas/{marinaId}/admins/{userId}/revoke` | RevokeAdmin | Soft-revokes the `MarinaAdmin` record (`record.Revoke()` + `UpdateAsync`); strips the PlaceOwner Identity role only when the user has zero remaining **active** memberships (see § 3 below) |
| `POST admin/marinas/{marinaId}/admins/{userId}/reenable` | ReEnableAdmin | Clears `RevokedAt` (`record.Reinstate()` + `UpdateAsync`) and re-grants the PlaceOwner role if the user doesn't already hold it; audit action `AdminReinstated` |
| `POST admin/bookings/{id}/cancel` | CancelBooking | Delegates to `IBookingService.CancelAsync(bookingId, currentUserId)`; the service resolves the caller as Admin via role check and applies the cancellation rules defined in `booking-lifecycle.md` |
| `POST admin/settings` | Settings POST | `settings.UpdateSettings(AutoActionType, AutoActionTimeoutHours)`; `UpdateAsync`; `TempData["Success"]` |

---

## Invitation Lifecycle

### 1. Admin sends invite — `AdminController.InviteAdmin POST`

Route: `POST admin/marinas/{marinaId}/invite`

1. Generate raw token: `Guid.NewGuid().ToString("N")`.
2. Create `Invitation` with `Token = TokenHasher.Hash(rawToken)`, `ExpiresAt = UtcNow + 48h`, `InvitedById = current admin user id`. The raw token is **never stored**.
3. `IInvitationRepository.AddAsync(invitation)`.
4. Email the link `{AppSettings.BaseUrl}/account/invite-register?token={rawToken}` to `model.Email`. The raw token travels in the email link only.
5. No uniqueness check on the recipient email — multiple pending invites for the same address are allowed.
6. Redirect to MarinaInvitations for the marina.

### 2. Recipient registers — `AccountController.InviteRegister`

Route: `GET/POST account/invite-register?token={rawToken}`

GET: hashes the token (`TokenHasher.Hash`), looks up `IInvitationRepository.GetByTokenHashAsync`. Returns 404 if not found, already used, or expired. Pre-fills the email from the invitation.

POST:
1. Re-validates the invitation (same 404 conditions) — guards against replay between GET and POST.
2. Creates `ApplicationUser` with `EmailConfirmed = true`.
3. `UserManager.AddToRoleAsync(user, "PlaceOwner")`.
4. Creates `MarinaAdmin { MarinaId, UserId, InvitedAt = UtcNow, InvitedById = invitation.InvitedById }` via `IMarinaAdminRepository.AddAsync`.
5. `IInvitationRepository.MarkUsedAsync(invitation)` — sets `IsUsed = true`.
6. Redirects to Login with a `TempData` message.

Result: the new user holds the PlaceOwner Identity role and is a member of exactly one marina.

### 3. Admin revokes and re-enables (soft-revoke) — `AdminController.RevokeAdmin` / `ReEnableAdmin`

Revoke route: `POST admin/marinas/{marinaId}/admins/{userId}/revoke`

1. Load all `MarinaAdmin` records for the marina via `GetByMarinaIdAsync` (returns active **and** revoked); find the target record.
2. `record.Revoke()` sets `RevokedAt = UtcNow`; persist via `IMarinaAdminRepository.UpdateAsync`. The join row is **never deleted** — it stays in the admins panel (marked Revoked) so it can be re-enabled later.
3. Count the user's remaining **active** memberships: `(await GetByUserIdAsync(userId)).Count(a => a.RevokedAt == null)`. If **zero**, `UserManager.RemoveFromRoleAsync(user, "PlaceOwner")` strips the role. If the user still actively administers another marina, the role is preserved.
4. Redirect to MarinaAdmins. Audit: `AdminRevoked`.

Re-enable route: `POST admin/marinas/{marinaId}/admins/{userId}/reenable`

1. Find the target `MarinaAdmin` record (it still exists, revoked).
2. `record.Reinstate()` clears `RevokedAt`; persist via `UpdateAsync`.
3. Re-grant the PlaceOwner role if the user doesn't already hold it (`IsInRoleAsync` guard → `AddToRoleAsync`).
4. Redirect to MarinaAdmins. Audit: `AdminReinstated`.

**Active-only queries.** Because revoked rows persist, every "is this user an active admin" read filters `RevokedAt == null`: `IMarinaAdminRepository.ExistsAsync` (the ownership gate used by all PlaceOwner controllers and AdminController), `MarinaRepository.GetByUserIdAsync` (the PlaceOwner "My Marinas" list), and the `AllMarinas` admin count. `GetByMarinaIdAsync` deliberately returns all rows — the panel renders revoked members with a "Revoked" pill + Re-enable button, ordered active-first via `OrderBy(a => a.IsRevoked).ThenBy(a => a.InvitedAt)`.

---

## ViewModels

Seven Admin-specific ViewModels live in `src/BoatSpotFinder.Web/Models/`. All are `record` types.

| ViewModel | Fields | Used by |
|---|---|---|
| `UserListItemViewModel` | `Id`, `Email`, `Roles` (List\<string\>), `IsActive`, `AverageRatingAsBoatOwner` (decimal?) | Users |
| `MarinaCreateViewModel` | `Name`, `Region` | CreateMarina |
| `InviteAdminViewModel` | `Email`, `MarinaId` | InviteAdmin (MarinaId is a hidden field, never a dropdown) |
| `MarinaAdminListItemViewModel` | `UserId`, `Email`, `InvitedAt`, `InvitedBy`, `IsRevoked` | MarinaAdmins |
| `AdminMarinaListItemViewModel` | `Id`, `Name`, `Region`, `IsActive`, `AdminCount`, `SpotCount` | AllMarinas |
| `InvitationListItemViewModel` | `Email`, `InvitedAt`, `ExpiresAt`, `IsUsed`, `Status` (derived string) | MarinaInvitations |
| `AdminSettingsViewModel` | `AutoActionType`, `AutoActionTimeoutHours` | Settings |

`MarinaEditViewModel` and `SpotListItemViewModel` are shared with PlaceOwner controllers — Admin reuses them without modification.

---

## ES Sync Rules in Admin Context

See `conventions.md` § Search Indexing → Sync rules for the full ruleset. Admin-specific behaviour:

- `CreateMarina POST` does **not** index. New marinas are shells with empty fields and no spots; they are not useful as search results.
- `EditMarina POST` calls `IndexAsync` only when the marina is currently active.
- `ToggleMarinaActive POST` calls `DeleteAsync` on deactivation and `IndexAsync` on reactivation.
- `ToggleSpotActive POST` has no ES side effect — spot data is not a top-level ES document.
