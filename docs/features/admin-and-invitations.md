# Admin Features and PlaceOwner Invitation Lifecycle

Covers the Admin role's management surface (Phase 6) and the invite-based flow through which new PlaceOwners are created and later revoked.

---

## Admin Role Surface

`AdminController` (`src/BoatSpotFinder.Web/Controllers/AdminController.cs`) is decorated `[Authorize(Roles="Admin")]` and routed at `[Route("admin")]`. It is the only controller through which the Admin role acts on the application's data.

### Read actions

| Route | Action | Data source |
|---|---|---|
| `GET admin/dashboard` | Dashboard | Static view — no repository call |
| `GET admin/users` | Users | `UserManager.Users` (all users); `GetRolesAsync` per user; `AverageRatingAsBoatOwner` shown only for BoatOwner-role users |
| `GET admin/bookings` | AllBookings | `IBookingRepository.GetAllAsync()`, ordered newest-first; Cancel button rendered for Pending and Confirmed rows |
| `GET admin/marinas` | AllMarinas | `IMarinaRepository.GetAllAsync(includeInactive: true)` — includes inactive marinas; admin count + spot count computed per marina |
| `GET admin/marinas/{id}/spots` | MarinaSpots | `ISpotRepository.GetByMarinaIdAsync(marinaId, includeInactive: true)` — bypasses the global `IsActive` query filter without `IgnoreQueryFilters()` in the controller (the `includeInactive` flag handles it inside the repository) |
| `GET admin/marinas/{id}/admins` | MarinaAdmins | `IMarinaAdminRepository.GetByMarinaIdAsync`; each member's email resolved via `UserManager.FindByIdAsync` |
| `GET admin/marinas/{id}/invitations` | MarinaInvitations | `IInvitationRepository.GetByMarinaIdAsync`; status derived in controller: `IsUsed` → Accepted, `ExpiresAt < UtcNow` → Expired, else Pending |
| `GET admin/marinas/{id}/layout` | MarinaLayout | Read-only Konva canvas; `#canvas-container` wired with `data-marina-id` + `data-spot-statuses-url` pointing at `Browse.SpotStatuses`; includes inactive spots; no vessel/date inputs |
| `GET admin/settings` | Settings GET | `IAdminSettingsRepository.GetAsync()` → `AdminSettingsViewModel` |

### Write actions

| Route | Action | Effect |
|---|---|---|
| `GET/POST admin/marinas/create` | CreateMarina | Creates a `Marina` with empty/zero defaults for non-required fields; NOT indexed in Elasticsearch at creation; redirects to InviteAdmin GET for the new marina |
| `GET/POST admin/marinas/{id}/edit` | EditMarina | `marina.UpdateDetails(...)` preserving existing `LayoutWidth`/`LayoutHeight`; `UpdateAsync`; calls `IndexAsync` only when `marina.IsActive == true`; BadRequest when route id and model id differ |
| `POST admin/marinas/{id}/toggle-active` | ToggleMarinaActive | Deactivates → `Deactivate()` + `DeleteAsync(marina.Id)` from ES; activates → `Activate()` + `IndexAsync`; `UpdateAsync`; existing bookings are NOT cancelled; never hard-deletes the marina |
| `POST admin/spots/{id}/toggle-active` | ToggleSpotActive | `Activate()` / `Deactivate()` on the spot; `UpdateAsync`; redirects to MarinaSpots using `spot.MarinaId` |
| `GET/POST admin/marinas/{id}/invite` | InviteAdmin | Creates and emails an invite (see Invitation Lifecycle below) |
| `POST admin/marinas/{id}/admins/{userId}/revoke` | RevokeAdmin | Removes the `MarinaAdmin` record; if the user has zero remaining marina memberships, strips the PlaceOwner Identity role via `UserManager.RemoveFromRoleAsync` |
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

### 3. Admin revokes — `AdminController.RevokeAdmin POST`

Route: `POST admin/marinas/{marinaId}/admins/{userId}/revoke`

1. Load all `MarinaAdmin` records for the marina via `GetByMarinaIdAsync`; find the target record.
2. `IMarinaAdminRepository.RemoveAsync(record)` — hard-deletes the join row (no soft-delete on `MarinaAdmin`).
3. Load all remaining memberships for the user via `GetByUserIdAsync`.
4. If the user has **zero remaining memberships**: `UserManager.RemoveFromRoleAsync(user, "PlaceOwner")` — strips the role so the user can no longer access PlaceOwner routes.
5. If the user still has memberships for other marinas, the role is preserved.
6. Redirect to MarinaAdmins for the marina.

---

## ViewModels

Seven Admin-specific ViewModels live in `src/BoatSpotFinder.Web/Models/`. All are `record` types.

| ViewModel | Fields | Used by |
|---|---|---|
| `UserListItemViewModel` | `Id`, `Email`, `Roles` (List\<string\>), `IsActive`, `AverageRatingAsBoatOwner` (decimal?) | Users |
| `MarinaCreateViewModel` | `Name`, `Region` | CreateMarina |
| `InviteAdminViewModel` | `Email`, `MarinaId` | InviteAdmin (MarinaId is a hidden field, never a dropdown) |
| `MarinaAdminListItemViewModel` | `UserId`, `Email`, `InvitedAt`, `InvitedBy` | MarinaAdmins |
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
