# Audit Logging

Every significant user action in the application is written to a structured audit log. The log captures who acted, on what entity, and what the outcome was. It is used for compliance, debugging, and forensic investigation. The implementation uses NLog as the underlying logger, surfaced through a thin service abstraction (`IAuditLogger`) that controllers inject directly.

The feature was introduced across Phase 2b in five tasks: NLog setup (2b.1), service contract and implementation (2b.2), Account actions (2b.3), PlaceOwner actions (2b.4a / 2b.4b), and Admin actions (2b.5).

---

## How It Works

```
Controller action succeeds (DB write / service call complete)
         |
         v
  _auditLogger.Log(userId, userEmail, action, entityType, entityId, marinaId, details)
         |
         v
  NLogAuditLogger.Log
   - opens a logging scope with all fields as structured key/value pairs
   - calls _logger.LogInformation (backed by NLog via UseNLog())
         |
         v
  NLog rule: BoatSpotFinder.Infrastructure.Logging.NLogAuditLogger → auditFile
   - file: logs/audit-{shortdate}.log (daily rolling, JSON layout)
   - one file per calendar day at the repo root /logs/ directory
```

Calls are explicit per action — there is no global filter or middleware. Each call fires on the success path, after the DB write or service call has already completed. If the action fails before reaching the audit call (e.g., `ModelState.IsValid` is false, or the service returns a failure result), no entry is written.

---

## Key Files

| File | Purpose |
|---|---|
| `src/BoatSpotFinder.Core/Interfaces/IAuditLogger.cs` | Service contract — `void Log(userId, userEmail, action, entityType, entityId, marinaId, details)` |
| `src/BoatSpotFinder.Infrastructure/Logging/NLogAuditLogger.cs` | Implementation — opens an NLog `BeginScope` dictionary, calls `_logger.LogInformation` |
| `src/BoatSpotFinder.Web/nlog.config` | NLog configuration — two file targets (`auditFile`, `emailFile`); one routing rule per logger name |
| `src/BoatSpotFinder.Web/Program.cs` | `builder.Host.UseNLog()` (line 23); `AddScoped<IAuditLogger, NLogAuditLogger>()` (step 9 of DI registration) |
| `src/BoatSpotFinder.Web/Controllers/AccountController.cs` | Emits Login, LoginFailed_*, Logout entries |
| `src/BoatSpotFinder.Web/Controllers/SpotsController.cs` | Emits SpotCreated, SpotEdited, SpotDeleted, SpotDeactivated, SpotActivated entries |
| `src/BoatSpotFinder.Web/Controllers/MarinasController.cs` | Emits MarinaEdited entry |
| `src/BoatSpotFinder.Web/Controllers/SpotBookingsController.cs` | Emits BookingConfirmed, BookingRejected entries |
| `src/BoatSpotFinder.Web/Controllers/PlaceOwnerReviewsController.cs` | Emits ReviewCreated entry |
| `src/BoatSpotFinder.Web/Controllers/AdminController.cs` | Emits MarinaCreated, MarinaActivated/Deactivated, SpotActivated/Deactivated, AdminInvited, AdminRevoked, BookingCancelledByAdmin, SettingsUpdated entries |

---

## Log Entry Schema

Every entry written to `auditFile` has the following fields in its JSON layout. All values come from the `BeginScope` dictionary in `NLogAuditLogger`.

| Field | Source | Notes |
|---|---|---|
| `timestamp` | NLog `${longdate}` | UTC |
| `level` | NLog `${level}` | Always `Info` for audit entries |
| `userId` | `User.FindFirstValue(ClaimTypes.NameIdentifier)` or `user.Id` | Empty string for `LoginFailed_UserNotFound` (no resolved user) |
| `userEmail` | `User.Identity!.Name` or `user.Email` | The actor's email |
| `userRole` | Hardcoded `string.Empty` in `NLogAuditLogger` | Reserved; not populated |
| `action` | Per-call string constant | See audited-action catalog below |
| `entityType` | Per-call string constant | `"User"`, `"Spot"`, `"Marina"`, `"Booking"`, `"Review"`, `"Invitation"`, `"MarinaAdmin"`, `"AdminSettings"` |
| `entityId` | The affected entity's id | String/Guid; empty string for `LoginFailed_UserNotFound` |
| `marinaId` | The marina the entity belongs to, or `null` | `null` for User and AdminSettings entries |
| `details` | JSON-serialized anonymous object, or `null` | Carries business context where meaningful (see catalog) |
| `message` | NLog `${message}` | Always `"Audit: {action} on {entityType} {entityId}"` |

---

## Audited-Action Catalog

All `userId` and `userEmail` values are captured from `User.FindFirstValue(ClaimTypes.NameIdentifier)` and `User.Identity!.Name` (or from `UserManager` for account actions where the claims context is not yet set).

### AccountController

| Action value | Trigger | entityType | entityId | marinaId | details |
|---|---|---|---|---|---|
| `Login` | Successful `SignInManager.PasswordSignInAsync` | `User` | user's id | `null` | `null` |
| `LoginFailed_Deactivated` | `result.IsNotAllowed` + `!user.IsActive` | `User` | user's id | `null` | `null` |
| `LoginFailed_EmailUnconfirmed` | `result.IsNotAllowed` + email not confirmed | `User` | user's id (or empty string if user not found) | `null` | `null` |
| `LoginFailed_UserNotFound` | `result.Succeeded == false`, no user for that email | `User` | empty string | `null` | `null` |
| `LoginFailed_InvalidPassword` | `result.Succeeded == false`, user exists but password wrong | `User` | user's id | `null` | `null` |
| `Logout` | `POST /account/logout` | `User` | user's id | `null` | `null` |

**Logout ordering note.** The audit call fires before `_signInManager.SignOutAsync()` because `User.Identity` is cleared by sign-out; capturing `userId`/`userEmail` afterward would yield empty strings. `UserManager.GetUserId(User)` and `GetUserName(User)` are called before `SignOutAsync`.

### SpotsController (PlaceOwner)

| Action value | Trigger | entityType | entityId | marinaId | details |
|---|---|---|---|---|---|
| `SpotCreated` | `POST /placeowner/marinas/{marinaId}/spots` | `Spot` | new spot's id | route `marinaId` | `{ spotName }` |
| `SpotEdited` | `POST /placeowner/marinas/{marinaId}/spots/{id}/edit` | `Spot` | spot's id | route `marinaId` | `null` |
| `SpotDeleted` | `POST /placeowner/marinas/{marinaId}/spots/{id}/delete` | `Spot` | spot's id | route `marinaId` | `{ spotName }` |
| `SpotDeactivated` | `POST /placeowner/marinas/{marinaId}/spots/{id}/deactivate` | `Spot` | spot's id | route `marinaId` | `null` |
| `SpotActivated` | `POST /placeowner/marinas/{marinaId}/spots/{id}/activate` | `Spot` | spot's id | route `marinaId` | `null` |

### MarinasController (PlaceOwner)

| Action value | Trigger | entityType | entityId | marinaId | details |
|---|---|---|---|---|---|
| `MarinaEdited` | `POST /placeowner/marinas/{id}/edit` | `Marina` | marina's id | marina's id | `null` |

### SpotBookingsController (PlaceOwner)

| Action value | Trigger | entityType | entityId | marinaId | details |
|---|---|---|---|---|---|
| `BookingConfirmed` | `POST /placeowner/spot-bookings/{id}/confirm` | `Booking` | booking's id | `booking.Spot.MarinaId` | `null` |
| `BookingRejected` | `POST /placeowner/spot-bookings/{id}/reject` | `Booking` | booking's id | `booking.Spot.MarinaId` | `null` |

Audit fires only when the booking was successfully loaded after the service call (`booking != null` guard).

### PlaceOwnerReviewsController (PlaceOwner)

| Action value | Trigger | entityType | entityId | marinaId | details |
|---|---|---|---|---|---|
| `ReviewCreated` | `POST /placeowner/reviews/create` | `Review` | new review's id | `booking.Spot.MarinaId` | `{ score, bookingId }` |

Audit fires only when both the booking and the newly persisted review are non-null after `CreateReviewAsync`.

### AdminController (Admin)

| Action value | Trigger | entityType | entityId | marinaId | details |
|---|---|---|---|---|---|
| `MarinaCreated` | `POST admin/marinas/create` | `Marina` | new marina's id | new marina's id | `{ name }` |
| `MarinaActivated` | `POST admin/marinas/{id}/toggle-active` (new state: active) | `Marina` | marina's id | marina's id | `null` |
| `MarinaDeactivated` | `POST admin/marinas/{id}/toggle-active` (new state: inactive) | `Marina` | marina's id | marina's id | `null` |
| `SpotActivated` | `POST admin/spots/{id}/toggle-active` (new state: active) | `Spot` | spot's id | `spot.MarinaId` | `null` |
| `SpotDeactivated` | `POST admin/spots/{id}/toggle-active` (new state: inactive) | `Spot` | spot's id | `spot.MarinaId` | `null` |
| `AdminInvited` | `POST admin/marinas/{id}/invite` | `Invitation` | invitation's id | `invitation.MarinaId` | `{ email }` |
| `AdminRevoked` | `POST admin/marinas/{marinaId}/admins/{userId}/revoke` | `MarinaAdmin` | revoked user's `userId` (route param) | route `marinaId` | `null` |
| `BookingCancelledByAdmin` | `POST admin/bookings/{id}/cancel` | `Booking` | booking's id | `booking.Spot.MarinaId` | `{ previousStatus }` |
| `SettingsUpdated` | `POST admin/settings` | `AdminSettings` | `"10000000-0000-0000-0000-000000000001"` | `null` | `null` |

**AdminRevoked disambiguation.** The acting admin's id is captured into a local `actingUserId` before the revoke logic runs, because the route also carries a `userId` parameter for the user being revoked. The `Log` call uses `actingUserId` as the `userId` argument (who acted) and the route `userId` as `entityId` (who was affected).

**BookingCancelledByAdmin previousStatus.** The booking's `Status` is read and captured into `previousStatus` before calling `BookingService.CancelAsync`. Because the service shares the same request-scoped `DbContext`, reading `Status` after the cancel would always yield `Cancelled`. The audit fires inside the `if (result.Success)` block, with `previousStatus` serialized as a string via `.Value.ToString()`.

---

## NLog Configuration

`nlog.config` lives at `src/BoatSpotFinder.Web/nlog.config` and is copied to the output directory on build. It defines two File targets and two routing rules.

**Targets.**

| Target name | File pattern | Layout | Purpose |
|---|---|---|---|
| `auditFile` | `logs/audit-{shortdate}.log` | JSON (see schema above) | One file per calendar day; all audit entries |
| `emailFile` | `logs/email-{shortdate}.log` | JSON (timestamp, level, logger, message) | One file per calendar day; email-sender log output |

`${basedir}` resolves to the build output directory (`bin/Debug/net10.0/`). The `logPath` variable steps five directories up to reach the repo root, where `logs/` is git-ignored.

**Rules.**

| Logger name | Target | Notes |
|---|---|---|
| `BoatSpotFinder.Infrastructure.Email.ConsoleEmailSender` | `emailFile` | `final="true"` — stops further matching |
| `BoatSpotFinder.Infrastructure.Logging.NLogAuditLogger` | `auditFile` | Catches all `Info`-and-above from the audit logger |

The `<nlog>` element sets `internalLogLevel="Info"` and routes NLog's own internal diagnostics to `nlog-internal.log` in the output directory.

---

## Business Rules

- Audit calls fire on the success path only, after the DB write or service call completes. A failed validation or service error produces no audit entry.
- Calls are explicit per action in each controller — there is no global action filter, middleware, or AOP mechanism.
- `IAuditLogger.Log` is synchronous (`void`). It does not block the request with async I/O.
- NLog file writes are synchronous in the default `File` target with `keepFileOpen="false"`. Write failures surface as NLog internal errors (written to `nlog-internal.log`) and do not propagate to the caller.
- Controllers do not wrap `_auditLogger.Log` calls in `try/catch`. NLog handles internal errors internally.
- The `userRole` field is always an empty string in the current implementation — it is reserved for future population.
- `details` is either `null` or a JSON-serialized anonymous object. The serialization happens inside `NLogAuditLogger` via `System.Text.Json.JsonSerializer.Serialize(details)`.
- The `Logout` audit entry is written before `SignOutAsync` so that the user's identity is still available in the claims context.

---

## Quick Reference

| Task | How |
|---|---|
| Find all audit entries for a user | Search `logs/audit-*.log` for `"userId":"<id>"` |
| Find all actions on a marina | Search for `"marinaId":"<id>"` |
| Find all Admin actions | Search for entries in `AdminController` action values (MarinaCreated, AdminInvited, etc.) |
| Enable/disable Elasticsearch for reviews | Elasticsearch is not involved in audit logging — audit always writes to the File target only |
| Add a new audited action | Inject `IAuditLogger` into the controller; call `_auditLogger.Log(...)` on the success path after the save |
| Locate the NLog config | `src/BoatSpotFinder.Web/nlog.config` |
| Locate audit log files (development) | `logs/audit-{yyyy-MM-dd}.log` at the repo root |
