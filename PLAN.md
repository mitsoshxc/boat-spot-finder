# Boat Spot Finder — Master Implementation Plan

## Context

The project is fully documented and scaffolded but contains zero domain code. All five entities, the DbContext, Identity setup, repositories, services, controllers, and views are yet to be built. This plan is the master roadmap: it tracks every phase and task, maps the dependency graph, and specifies exactly what files each `/dev` brief must produce. Update the status of each task here as work is approved.

Status legend: `[ ]` not started · `[x]` complete · `[~]` in progress

---

## Phase Dependency Graph

```
Phase 1 (Foundation)
    └── Phase 2 (Auth)
            ├── Phase 3 (PlaceOwner — Marina & Spot)
            │       └── Phase 3b (Elasticsearch Marina Search)
            └── Phase 4 (BoatOwner — Vessel)
                    └── Phase 5 (Booking System)
                            ├── Phase 6 (Admin)
                            └── Phase 7 (Browse & Search) ← depends on Phase 3b
                                    └── Phase 8 (Polish)
```

Phases 3 and 4 are independent of each other. Phase 3b must complete before Phase 7.

---

## Phase 1 — Foundation

**Goal:** All entities persisted, DbContext registered, migrations applied, DI wired. `dotnet build` and `dotnet ef database update` both succeed. No UI yet.

| # | Task | Files | Status |
|---|------|-------|--------|
| 1.1 | Core entities | `Core/Entities/BaseEntity.cs` (abstract — `CreatedAt`, `UpdatedAt`, both `DateTime` UTC), `ApplicationUser.cs`, `Marina.cs`, `Spot.cs`, `SpotSeasonalRule.cs`, `Vessel.cs`, `Booking.cs`, `AdminSettings.cs`, `BookingStatus.cs` (enum), `AutoActionType.cs` (enum: `AutoApprove`, `AutoReject`), `VesselType.cs` (`[Flags]` enum — `None=0`, `SailBoat=1`, `MotorBoat=2`, `Catamaran=4`, `RIB=8`, `Yacht=16`, `Other=32`); add `Microsoft.Extensions.Identity.Core` to `Core.csproj`; delete `Core/Class1.cs` | [ ] |
| 1.2 | EF configurations | `Infrastructure/Data/Configurations/BaseEntityConfiguration.cs` (generic `IEntityTypeConfiguration<BaseEntity>` — configures `CreatedAt`/`UpdatedAt` for all inheriting entities), `ApplicationUserConfiguration.cs`, `MarinaConfiguration.cs`, `SpotConfiguration.cs` (global query filter `s => s.IsActive`), `SpotSeasonalRuleConfiguration.cs` (unique index on `SpotId` + `StartDate` + `EndDate`; overlap constraint enforced at service layer), `VesselConfiguration.cs`, `BookingConfiguration.cs`, `AdminSettingsConfiguration.cs` (seeds single row: `AutoActionType=AutoApprove`, `AutoActionTimeoutHours=6`) | [ ] |
| 1.3 | AppDbContext | `Infrastructure/Data/AppDbContext.cs` (extends `IdentityDbContext<ApplicationUser>` + `IDataProtectionKeyContext`, `ApplyConfigurationsFromAssembly`, seeds 3 roles + Admin user + `AdminSettings` default row via `HasData`); add `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` to `Infrastructure.csproj`; delete `Infrastructure/Class1.cs` | [ ] |
| 1.4 | Connection string + Program.cs | `Web/appsettings.json` + `appsettings.Development.json` (add `ConnectionStrings:DefaultConnection` LocalDB); `Web/Program.cs` (add `AddDbContext`, `AddIdentity` + `AddEntityFrameworkStores<AppDbContext>`, `AddDataProtection().PersistKeysToDbContext<AppDbContext>()`, `UseAuthentication`, `UseAuthorization`, `UseStaticFiles`, auto-migration in Development) | [ ] |
| 1.5 | Initial migration | Run: `dotnet ef migrations add InitialCreate --project src/BoatSpotFinder.Infrastructure --startup-project src/BoatSpotFinder.Web` then `dotnet ef database update ...` | [ ] |
| 1.6 | Hangfire setup | Add `Hangfire.Core`, `Hangfire.SqlServer`, `Hangfire.AspNetCore` to `Web.csproj`; in `Program.cs`: `AddHangfire(c => c.UseSqlServerStorage(connectionString))`, `AddHangfireServer()`, `UseHangfireDashboard("/hangfire")` (restrict to Admin role); Hangfire uses the same SQL Server DB — no extra infrastructure needed; dashboard at `/hangfire` shows job history and retries | [ ] |
| 1.7 | Health checks | Add `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` to `Web.csproj`; in `Program.cs`: `AddHealthChecks().AddDbContextCheck<AppDbContext>().AddElasticsearch(uri)` + `MapHealthChecks("/health")`; used by k8s liveness + readiness probes | [ ] |

**Entity property reference:**

`BaseEntity` *(abstract — all entities except `ApplicationUser` inherit this)*: `CreatedAt` (DateTime UTC), `UpdatedAt` (DateTime UTC)

`ApplicationUser`: extends `IdentityUser` — `IsActive` (bool, default true — checked on every sign-in; disabled user is rejected with an error), `IsSuperAdmin` (bool — blocks deletion at service layer); `EmailConfirmed` inherited from IdentityUser is the email verification gate; no `IsPlaceOwnerPending` (invite flow replaces it)

`Marina` *(BaseEntity)*: `Id`, `Name`, `Description`, `Address`, `Region` (string), `Phone` (string), `Latitude`, `Longitude`, `DefaultPricePerDay` (decimal 18,2 — fallback when `Spot.PricePerDay` is null), `IsActive` (bool, default true — soft-delete; deactivating hides marina from Browse but does not cancel existing bookings), `LayoutWidth` (int, default 1200), `LayoutHeight` (int, default 800), `BackgroundImagePath` (string?); no `PlaceOwnerId` FK — ownership is via `MarinaAdmin` join table

`MarinaAdmin` *(join table, no BaseEntity)*: `MarinaId` FK, `UserId` FK, `InvitedAt` (DateTimeOffset), `InvitedById` FK → composite PK (`MarinaId` + `UserId`)

`Invitation` *(BaseEntity)*: `Id`, `Email`, `Token` (string — store SHA-256 hash; raw token sent in email only), `MarinaId` FK, `ExpiresAt` (DateTimeOffset, 48 h from creation), `IsUsed` (bool), `InvitedById` FK

`Spot` *(BaseEntity)*: `Id`, `Name`, `Description`, `LengthMeters`, `WidthMeters`, `DepthMeters`, `PricePerDay` (decimal 18,2, **nullable** — null means fall back to `Marina.DefaultPricePerDay`), `DefaultMinBookingDays` (int), `IsActive` (bool — soft-delete; deactivating does not cancel existing bookings), `AllowedVesselTypes` (`VesselType` flags stored as `int`; `None=0` means no restriction), `MarinaId` FK; **canvas layout fields** (all nullable): `CanvasX` (double?), `CanvasY` (double?), `CanvasW` (double?), `CanvasH` (double?), `CanvasRotation` (double?, degrees); navigation: `SeasonalRules` (1:N → `SpotSeasonalRule`)

`SpotSeasonalRule` *(BaseEntity)*: `Id`, `Name` (string, e.g. "Summer 2026"), `StartDate` (DateOnly), `EndDate` (DateOnly), `PricePerDay` (decimal 18,2), `MinBookingDays` (int), `SpotId` FK → `Spot`

`Vessel` *(BaseEntity)*: `Id`, `Name`, `Description` (string?), `Type` (`VesselType` enum — single value, not flags), `LengthMeters`, `WidthMeters`, `DepthMeters`, `OwnerId` FK → `ApplicationUser`

`Booking` *(BaseEntity)*: `Id`, `SpotId` FK, `VesselId` FK → `Vessel`, `BoatOwnerId` FK → `ApplicationUser`, `StartDate` (DateOnly), `EndDate` (DateOnly), `TotalPrice` (decimal 18,2), `Status` (`BookingStatus` enum: `Pending`, `Confirmed`, `Cancelled`, `Completed`)

`AdminSettings` *(BaseEntity, single-row config table)*: `Id`, `AutoActionType` (`AutoActionType` enum: `AutoApprove` | `AutoReject`), `AutoActionTimeoutHours` (int, default 6); seeded via `HasData` — never deleted or inserted by application code, only updated

**Notes:**
- `ApplicationUser` extending `IdentityUser` pulls only `Microsoft.Extensions.Identity.Core` into Core — no EF dep, layering rule is upheld.
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` must be on Infrastructure (where AppDbContext lives), not only Web.
- Admin user seed must use `PasswordHasher<ApplicationUser>` to produce the correct hash. Use a fixed GUID for idempotency.
- EF Core 10 natively supports `DateOnly` with SQL Server — no value converter needed.
- Canvas coordinates use a fixed logical unit system (e.g. 0–1200 × 0–800). The frontend scales these to the actual rendered canvas size client-side.
- `Invitation.Token` stored as SHA-256 hash; raw token sent in the email link only.
- `SpotSeasonalRule` date ranges must not overlap for the same spot — enforced at service layer (reject save if any existing rule's range intersects the new one).

**Dev brief grouping:** Tasks 1.1–1.3 in one `/dev`; Tasks 1.4–1.7 in a second `/dev`.

---

## Phase 2 — Authentication

**Goal:** BoatOwner self-registration with email verification. PlaceOwner accounts created only via admin invite link. Login redirects to role-specific landing page. Auth-aware nav bar.

**Role landing pages after login:**
- Admin → `/admin/dashboard`
- PlaceOwner → `/marinas` (their marina list)
- BoatOwner → `/browse`

| # | Task | Files | Status |
|---|------|-------|--------|
| 2.1 | Email sender | `Core/Interfaces/IEmailSender.cs` (`SendAsync(to, subject, htmlBody)`); `Infrastructure/Email/SmtpEmailSender.cs` (MailKit — reads host/port/user/pass from config); `Infrastructure/Email/ConsoleEmailSender.cs` (dev stub — prints to stdout); register the correct impl based on environment in `Program.cs`; add `"Smtp": { "Host", "Port", "User", "Password", "FromAddress" }` to appsettings | [ ] |
| 2.2 | Auth ViewModels | `Web/Models/LoginViewModel.cs`, `RegisterViewModel.cs` (BoatOwner only — no role selection), `InviteRegisterViewModel.cs` (email pre-filled from token, password + confirm) | [ ] |
| 2.3 | AccountController | `Web/Controllers/AccountController.cs` — `Register GET/POST` (creates BoatOwner, sends confirmation email, shows "check your inbox" page), `ConfirmEmail GET` (validates token, sets `EmailConfirmed=true`, redirects to login), `Login POST` (blocks unconfirmed emails; on success redirects by role), `Logout POST`, `InviteRegister GET` (validates invite token, pre-fills email) / `POST` (creates PlaceOwner account, assigns role, adds `MarinaAdmin` record, marks invite used) | [ ] |
| 2.4 | Auth Views | `Web/Views/Account/Login.cshtml`, `Register.cshtml`, `RegisterConfirmation.cshtml` ("check your email"), `ConfirmEmail.cshtml`, `InviteRegister.cshtml`, `AccessDenied.cshtml` | [ ] |
| 2.5 | Auth-aware layout + nav | `Web/Views/Shared/_LoginPartial.cshtml` (new); update `_Layout.cshtml` — show role-specific nav links using `@User.IsInRole()`; nav items: Admin → Admin panel; PlaceOwner → My Marinas; BoatOwner → Browse, My Bookings, My Vessels | [ ] |
| 2.6 | Identity options | `Web/Program.cs` — `ConfigureApplicationCookie` (LoginPath `/account/login`, AccessDeniedPath `/account/access-denied`), password policy, `AddDefaultTokenProviders`, `RequireConfirmedEmail = true` on sign-in options | [ ] |
| 2.7 | Password reset ViewModels + Views | `Web/Models/ForgotPasswordViewModel.cs` (email); `Web/Models/ResetPasswordViewModel.cs` (email, token, password, confirm); Views: `Account/ForgotPassword.cshtml`, `ForgotPasswordConfirmation.cshtml` ("check your email"), `ResetPassword.cshtml`, `ResetPasswordConfirmation.cshtml` | [ ] |
| 2.8 | Password reset controller actions | Add to `AccountController`: `ForgotPassword GET/POST` (generates reset token via `UserManager`, sends email via `IEmailSender`), `ResetPassword GET` (validates token), `ResetPassword POST` (calls `UserManager.ResetPasswordAsync`) | [ ] |

**Dev brief grouping:** Task 2.1 in one `/dev`; Tasks 2.2–2.4 in a second `/dev`; Tasks 2.5–2.8 in a third `/dev`.

---

## Phase 2b — Audit Logging (NLog)

**Goal:** Structured audit logs written to daily rolling files and indexed to Elasticsearch. Covers all PlaceOwner actions and login/logout events.

**Log entry fields:** `timestamp`, `userId`, `userEmail`, `userRole`, `action`, `entityType`, `entityId`, `marinaId`, `details` (JSON context).

**Log index pattern in ES:** `marina-audit-{yyyy.MM.dd}` — one index per day, queryable via Kibana.

| # | Task | Files | Status |
|---|------|-------|--------|
| 2b.1 | NLog setup | Add `NLog.Web.AspNetCore`, `NLog.Targets.ElasticSearch` to `Web.csproj`; create `nlog.config` at Web project root — two targets: `File` (`archiveEvery="Day"`, `archiveDateFormat="yyyy-MM-dd"`, JSON layout) and `ElasticSearch` (index `marina-audit-${date:format=yyyy.MM.dd}`, ES URI from env var); wire NLog into `Program.cs` via `UseNLog()`; add `"NLog": { "ElasticSearchUri": "..." }` to appsettings | [ ] |
| 2b.2 | Audit logger service | `Core/Interfaces/IAuditLogger.cs` — `Log(string userId, string userEmail, string action, string entityType, string entityId, string? marinaId, object? details)`; `Infrastructure/Logging/NLogAuditLogger.cs` — uses `ILogger<NLogAuditLogger>` (ASP.NET Core abstraction backed by NLog), writes structured log entry; register in `Program.cs` | [ ] |
| 2b.3 | Login/logout audit | `AccountController` — call `IAuditLogger.Log(...)` on successful login and logout | [ ] |
| 2b.4 | PlaceOwner action audit | Add `IAuditLogger` calls in `SpotsController` (Create, Edit, Deactivate), `SpotBookingsController` (Confirm, Reject), `MarinasController` (Edit POST) | [ ] |

**Note:** NLog config lives in `nlog.config` (not in `Program.cs`) — targets and layout can be changed without a recompile. Audit calls are explicit per-action, not a global filter, so each captures business-meaningful context.

**Dev brief grouping:** Tasks 2b.1–2b.2 in one `/dev`; Tasks 2b.3–2b.4 in a second `/dev` (after Phase 3 and 5 controllers exist).

---

## Phase 3 — PlaceOwner: Marina & Spot Management

**Goal:** PlaceOwners manage their own marinas and spots. Each marina has a canvas-based layout editor where spots (rectangles) are drawn and positioned. Ownership enforced on every action.

**Marina lifecycle:** Admin creates marina (name + region only) → Admin invites PlaceOwner for that marina → PlaceOwner fills in full details (description, address, phone, lat/lng) + designs layout. Marinas are never hard-deleted — Admin can deactivate them.

**PlaceOwner flow:** Receives invite link → registers → lands on their marina's Edit page to complete info → then opens Layout Editor to design spots.

**Ownership check (all PlaceOwner actions):** verify `MarinaAdmin` record exists for `(marinaId, currentUserId)` — not a single FK. Return `Forbid()` if not found.

| # | Task | Files | Status |
|---|------|-------|--------|
| 3.0 | File storage service | `Core/Interfaces/IFileStorageService.cs` — `SaveAsync(Stream stream, string fileName, string contentType)` returns `string` (path or URL), `DeleteAsync(string pathOrUrl)`; `Infrastructure/Storage/LocalFileStorageService.cs` — saves to `wwwroot/uploads/`, returns relative path; register as scoped in `Program.cs`; add `"Storage": { "Provider": "Local", "LocalBasePath": "wwwroot/uploads" }` to appsettings | [ ] |
| 3.1 | Marina repository | `Core/Interfaces/IMarinaRepository.cs` (`GetByIdAsync`, `GetByOwnerIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`); `Infrastructure/Repositories/MarinaRepository.cs`; register in `Program.cs` | [ ] |
| 3.2 | Spot repository | `Core/Interfaces/ISpotRepository.cs` (`GetByIdAsync` ignoring query filter for owner use, `GetByMarinaIdAsync(includeInactive)`, `GetAllAsync(includeInactive)`, `AddAsync`, `UpdateAsync`, `UpdatePositionsAsync(IEnumerable<SpotPositionUpdate>)`); `Infrastructure/Repositories/SpotRepository.cs`; register in `Program.cs` | [ ] |
| 3.3 | Marina ViewModels | `Web/Models/MarinaEditViewModel.cs` (full fields — PlaceOwner fills these in), `MarinaListItemViewModel.cs`, `MarinaLayoutViewModel.cs` (marina + all spots with canvas coords for editor) | [ ] |
| 3.4 | MarinasController + Views | `Web/Controllers/MarinasController.cs` `[Authorize(Roles="PlaceOwner")]` route `[Route("marinas")]` — `Index` (their marinas), `Edit GET/POST` (full marina details — no Create, Admin creates), `Layout GET` (canvas editor), `UploadBackground POST`; ownership check (`Forbid()` on mismatch); Views: `Marinas/Index.cshtml`, `Edit.cshtml`, `Layout.cshtml` | [ ] |
| 3.5 | Spot ViewModels | `Web/Models/SpotCreateViewModel.cs`, `SpotEditViewModel.cs` (both include `AllowedVesselTypes` as a `List<VesselType>` bound to a checkbox group — MVC model binding combines into flags int before save), `SpotListItemViewModel.cs`, `SpotPositionUpdateViewModel.cs` (id, canvasX, canvasY, canvasW, canvasH, rotation — used by JS save call) | [ ] |
| 3.6 | SpotsController + Views | `Web/Controllers/SpotsController.cs` `[Authorize(Roles="PlaceOwner")]` route `[Route("marinas/{marinaId}/spots")]` — `Index` (list with deactivate action), `Create GET/POST` (name/description/dimensions/price; canvas position left null until drawn), `Edit GET/POST`, `Deactivate POST`, `SavePositions POST` (receives JSON array of `SpotPositionUpdateViewModel`, bulk-saves canvas coords — called by the editor JS on save); ownership chain validated on all actions; Views: `Spots/Index.cshtml`, `Create.cshtml`, `Edit.cshtml` | [ ] |
| 3.7 | Marina Layout Editor JS | `Web/wwwroot/js/marina-editor.js` — Konva.js stage; loads marina background image + existing spots (from `GET /marinas/{id}/layout/data`); allows drag/resize of spot rectangles; Add Spot button creates a new rect and POSTs to `Spots/Create`; Save Layout button POSTs all positions to `SpotsController/SavePositions`; spot label shows name; color: placed=blue, unplaced=dashed | [ ] |
| 3.8 | Layout data JSON endpoint | `GET /marinas/{id}/layout/data` action on `MarinasController` — returns JSON: `{ backgroundImagePath, layoutWidth, layoutHeight, spots: [{id, name, canvasX, canvasY, canvasW, canvasH, rotation, isActive}] }` — used by both editor and viewer JS | [ ] |

| 3.9 | SpotSeasonalRule repository | `Core/Interfaces/ISpotSeasonalRuleRepository.cs` (`GetBySpotIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`); `Infrastructure/Repositories/SpotSeasonalRuleRepository.cs`; register in `Program.cs` | [ ] |
| 3.10 | SpotSeasonalRule ViewModels | `Web/Models/SpotSeasonalRuleCreateViewModel.cs`, `SpotSeasonalRuleEditViewModel.cs`, `SpotSeasonalRuleListItemViewModel.cs` | [ ] |
| 3.11 | SpotSeasonalRulesController + Views | `Web/Controllers/SpotSeasonalRulesController.cs` `[Authorize(Roles="PlaceOwner")]` route `[Route("marinas/{marinaId}/spots/{spotId}/seasonal-rules")]` — `Index` (list all rules for a spot), `Create GET/POST`, `Edit GET/POST`, `Delete POST`; ownership chain validated (marinaId → MarinaAdmin → spotId → spot belongs to marina); service-layer overlap check on Create/Edit — if new rule's dates intersect any existing rule for the same spot, return a validation error; Views: `SpotSeasonalRules/Index.cshtml`, `Create.cshtml`, `Edit.cshtml` | [ ] |

**Konva.js delivery:** Reference via CDN in the specific views that need it (`Layout.cshtml` for editor, Browse marina view). Do not add to `_Layout.cshtml` globally.

**Background image storage:** Abstracted behind `IFileStorageService` so the storage provider can be swapped without touching controllers.
- **Now:** `LocalFileStorageService` saves to `wwwroot/uploads/marina-backgrounds/` on the shared PVC; returns a relative path (e.g. `/uploads/marina-backgrounds/id.jpg`) stored in `Marina.BackgroundImagePath`.
- **Future:** Implement `AzureBlobStorageService` or `DigitalOceanSpacesStorageService` in Infrastructure, update DI registration in `Program.cs`. No controller changes needed.
- `Marina.BackgroundImagePath` stores whatever the service returns — relative path for local, full CDN URL for cloud.
- Accepted types: jpg, png, webp. Max size 5 MB enforced in controller.

**Dev brief grouping:** Tasks 3.1 + 3.3 + 3.4 together; Tasks 3.2 + 3.5 + 3.6 together; Tasks 3.7 + 3.8 together; Tasks 3.9–3.11 together.

---

## Phase 3b — Elasticsearch Marina Search

**Goal:** Marinas are indexed in Elasticsearch on create/update. The Browse search box queries ES across name, region, and phone fields. SQL Server remains the source of truth; ES is the search index only.

**Local dev:** Run Elasticsearch via Docker (`docker run -p 9200:9200 -e "discovery.type=single-node" elasticsearch:8.x`). Connection URI configured in `appsettings.Development.json`.

| # | Task | Files | Status |
|---|------|-------|--------|
| 3b.1 | ES packages + config | Add `Elastic.Clients.Elasticsearch` (latest 8.x) to `Infrastructure.csproj`; add `"Elasticsearch": { "Uri": "http://localhost:9200" }` to `appsettings.json` and `appsettings.Development.json` | [ ] |
| 3b.2 | Marina search service interface | `Core/Interfaces/IMarinaSearchService.cs` — `IndexAsync(Marina marina)`, `DeleteAsync(Guid id)`, `SearchAsync(string? query)` returning `IEnumerable<Guid>` (marina IDs) | [ ] |
| 3b.3 | ES implementation | `Infrastructure/Search/ElasticsearchMarinaSearchService.cs` — implements `IMarinaSearchService`; `IndexAsync` upserts a `MarinaDocument` (`{ id, name, region, phone, address, description }`); `SearchAsync` runs a multi-match query across all indexed fields with fuzzy matching; `DeleteAsync` removes the document by id; register in `Program.cs` as scoped | [ ] |
| 3b.4 | Index on write | Call `IMarinaSearchService.IndexAsync()` after every marina write that keeps it active: `MarinasController.Edit POST` (PlaceOwner edits details), `AdminController.EditMarina POST` (Admin edits details), `AdminController.ToggleMarinaActive POST` when activating. Call `IMarinaSearchService.DeleteAsync()` when deactivating: `AdminController.ToggleMarinaActive POST` when deactivating — removes the document from the index so the marina no longer appears in Browse search. No hard-deletes exist so `DeleteAsync` is only ever called on deactivation. | [ ] |
| 3b.5 | Seed existing marinas | Add a startup call in `Program.cs` (`app.Lifetime.ApplicationStarted`) that re-indexes all **active** marinas (`IsActive = true`) from SQL — idempotent, handles the case where ES index is wiped; inactive marinas are not indexed | [ ] |

**Dependency note:** Phase 3b depends on Phase 3 (marina entity + controller exist). Phase 7 Browse depends on Phase 3b being complete.

**Dev brief grouping:** Tasks 3b.1–3b.3 in one `/dev`; Tasks 3b.4–3b.5 in a second `/dev`.

---

## Phase 4 — BoatOwner: Vessel Management

**Goal:** BoatOwners register and manage their vessels.

| # | Task | Files | Status |
|---|------|-------|--------|
| 4.1 | Vessel repository | `Core/Interfaces/IVesselRepository.cs` (`GetByIdAsync`, `GetByOwnerIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`); `Infrastructure/Repositories/VesselRepository.cs`; register in `Program.cs` | [ ] |
| 4.2 | Vessel ViewModels | `Web/Models/VesselCreateViewModel.cs`, `VesselEditViewModel.cs`, `VesselListItemViewModel.cs` | [ ] |
| 4.3 | VesselsController + Views | `Web/Controllers/VesselsController.cs` `[Authorize(Roles="BoatOwner")]` route `[Route("vessels")]` — Index, Create GET/POST, Edit GET/POST, Delete POST; ownership check; Views: `Vessels/Index.cshtml`, `Create.cshtml`, `Edit.cshtml` | [ ] |

**Dev brief grouping:** Tasks 4.1–4.3 in one `/dev`.

---

## Phase 5 — Booking System

**Goal:** Full booking lifecycle. BoatOwners create/cancel. PlaceOwners confirm/reject. Status transitions enforced.

> **Security note:** Run `/security-review` before merging Tasks 5.5 and 5.6.
> **Simplify note:** Run `/simplify` on BookingsController and SpotBookingsController if either exceeds 150 lines.

| # | Task | Files | Status |
|---|------|-------|--------|
| 5.1 | Booking repository | `Core/Interfaces/IBookingRepository.cs` (`GetByIdAsync`, `GetByBoatOwnerIdAsync`, `GetBySpotIdAsync`, `GetByMarinaOwnerIdAsync`, `AddAsync`, `UpdateAsync`, `IsSpotAvailableAsync(spotId, start, end, excludeId?)`); `Infrastructure/Repositories/BookingRepository.cs`; register in `Program.cs` | [ ] |
| 5.2 | BookingService | `Core/Interfaces/IBookingService.cs`; `Core/Services/BookingService.cs` — `CreateAsync`: (1) load vessel + spot + marina + seasonal rules, (2) hard-block if vessel type not allowed: `spot.AllowedVesselTypes != VesselType.None && (spot.AllowedVesselTypes & vessel.Type) == 0` → return error, (3) hard-block if dimensions don't fit: `vessel.LengthMeters > spot.LengthMeters \|\| vessel.WidthMeters > spot.WidthMeters \|\| vessel.DepthMeters > spot.DepthMeters` → return error, (4) availability overlap check, (5) **pricing resolution**: find `SpotSeasonalRule` where `rule.StartDate <= booking.StartDate <= rule.EndDate` — **only `StartDate` is checked, not `EndDate` or any intermediate day; this is intentional**; if match → use `rule.PricePerDay` and `rule.MinBookingDays`; if no match → use `spot.PricePerDay` and `spot.DefaultMinBookingDays`; if `spot.PricePerDay` is null → use `marina.DefaultPricePerDay`; `TotalPrice = resolvedPricePerDay × (EndDate − StartDate).Days`, (6) enforce `MinBookingDays`: if `(EndDate − StartDate).Days < resolvedMinBookingDays` → return validation error, (7) create `Pending` booking; `CancelAsync` (role-aware — both BoatOwner and PlaceOwner may cancel only if `StartDate > today`; cancelling on or after `StartDate` is rejected regardless of role), `ConfirmAsync`, `AutoActionAsync` (fires after `AdminSettings.AutoActionTimeoutHours` if PlaceOwner took no action — either auto-confirms or auto-rejects per `AdminSettings.AutoActionType`), `CompleteOverdueAsync` (transitions `Confirmed` → `Completed` where `EndDate < today`); register in `Program.cs` | [ ] |
| 5.3 | BookingService tests | `tests/BookingServiceTests.cs` — overlap logic, price calc, invalid transitions, vessel too large → rejected, vessel exactly fits → allowed, vessel type not in `AllowedVesselTypes` → rejected, vessel type allowed (flags match) → allowed, `AllowedVesselTypes == None` (no restriction) → allowed regardless of type; **seasonal pricing**: booking start within a seasonal rule → rule price used, booking start outside all rules → spot default price used, spot price null + no rule → marina default price used; **MinBookingDays**: booking duration below minimum → rejected, booking duration exactly at minimum → allowed; **cancellation**: BoatOwner cancel before StartDate → allowed, BoatOwner cancel on StartDate → rejected, PlaceOwner cancel before StartDate → allowed, PlaceOwner cancel on StartDate → rejected; update `Tests.csproj` (add Infrastructure ref + `Microsoft.EntityFrameworkCore.InMemory`) | [ ] |
| 5.4 | Booking ViewModels | `Web/Models/BookingCreateViewModel.cs` (spotId, startDate, endDate, **vesselId** — dropdown of user's own vessels), `BookingListItemViewModel.cs` (includes vessel name), `BookingDetailsViewModel.cs` | [ ] |
| 5.5 | BoatOwner BookingsController + Views | `Web/Controllers/BookingsController.cs` `[Authorize(Roles="BoatOwner")]` route `[Route("bookings")]` — MyBookings, Create GET (pre-fills spotId + dates from query params; populates vessel dropdown via `IVesselRepository.GetByOwnerIdAsync`) / POST (passes vesselId to `BookingService.CreateAsync`), Cancel POST; Views: `Bookings/MyBookings.cshtml`, `Create.cshtml` | [ ] |
| 5.6 | PlaceOwner SpotBookingsController + Views | `Web/Controllers/SpotBookingsController.cs` `[Authorize(Roles="PlaceOwner")]` route `[Route("spot-bookings")]` — Incoming, Confirm POST, Reject POST; Views: `SpotBookings/Incoming.cshtml` | [ ] |
| 5.7 | Recurring jobs (Hangfire) | In `Program.cs` after `UseHangfireDashboard`: register two recurring jobs via `RecurringJob.AddOrUpdate()` — (1) **Auto-action job**: runs every 5 minutes, calls `BookingService.AutoActionAsync()` — finds all `Pending` bookings where `CreatedAt + AdminSettings.AutoActionTimeoutHours < now` and applies `AdminSettings.AutoActionType`; (2) **Completion job**: runs nightly (`"0 2 * * *"`), calls `BookingService.CompleteOverdueAsync()` — transitions `Confirmed` → `Completed` where `EndDate < today`; both jobs are idempotent and logged via NLog | [ ] |
| 5.8 | Booking email notifications | Add `IEmailSender` calls in controllers after status changes: new booking created → email all `MarinaAdmin` users for that marina; booking confirmed → email BoatOwner; booking rejected → email BoatOwner; booking cancelled by BoatOwner → email marina admins. HTML email bodies kept minimal — status, spot name, dates, price | [ ] |

**Availability overlap rule:** `start < booking.EndDate && end > booking.StartDate`, exclude `Status == Cancelled`. Use `<`/`>` not `<=`/`>=` to allow adjacent bookings.

**Dev brief grouping:** Tasks 5.1–5.3 in one `/dev`; Tasks 5.4–5.6 in a second `/dev`; Tasks 5.7–5.8 in a third `/dev`.

---

## Phase 6 — Admin Features

**Goal:** Admin invites PlaceOwners per marina, views all users/bookings/spots, sees marina canvas read-only, manages marina admin memberships, deactivates spots.

> **Security note:** Run `/security-review` before merging Task 6.2.

| # | Task | Files | Status |
|---|------|-------|--------|
| 6.1 | Admin ViewModels | `Web/Models/UserListItemViewModel.cs`, `MarinaCreateViewModel.cs` (name + region — minimal), `InviteAdminViewModel.cs` (email + marinaId dropdown), `MarinaAdminListItemViewModel.cs` (userId, email, invitedAt, invitedBy), `AdminMarinaListItemViewModel.cs` (marina name, region, admin count, spot count), `InvitationListItemViewModel.cs` (email, invitedAt, expiresAt, isUsed, status: `Pending` | `Expired` | `Accepted` — derived from `IsUsed` and `ExpiresAt` vs now), `AdminSettingsViewModel.cs` (`AutoActionType` dropdown, `AutoActionTimeoutHours` numeric input) | [ ] |
| 6.2 | Invitation repository | `Core/Interfaces/IInvitationRepository.cs` (`AddAsync`, `GetByTokenHashAsync`, `GetByMarinaIdAsync`, `MarkUsedAsync`); `Infrastructure/Repositories/InvitationRepository.cs`; register in `Program.cs` | [ ] |
| 6.3 | MarinaAdmin repository | `Core/Interfaces/IMarinaAdminRepository.cs` (`GetByMarinaIdAsync`, `GetByUserIdAsync`, `AddAsync`, `RemoveAsync`, `ExistsAsync(marinaId, userId)`); `Infrastructure/Repositories/MarinaAdminRepository.cs`; register in `Program.cs` | [ ] |
| 6.4 | AdminController + Views | `Web/Controllers/AdminController.cs` `[Authorize(Roles="Admin")]` route `[Route("admin")]` — `Dashboard`, `Users`, `AllBookings`, `AllMarinas` (lists **all** marinas regardless of active spots — Admin sees everything), `CreateMarina GET/POST` (name + region only), `EditMarina GET/POST` (Admin can edit any marina's details), `ToggleMarinaActive POST` (Admin can activate/deactivate a marina — deactivating hides it from Browse but does not cancel existing bookings; Admin never hard-deletes marinas), `ToggleSpotActive POST` (Admin can activate/deactivate any spot — same soft-delete rule as PlaceOwner), `MarinaLayout GET` (read-only canvas — reuses marina-viewer.js), `MarinaSpots GET` (per-marina spot list — shows all spots including inactive; Admin toggles active state from here), `InviteAdmin GET/POST` (creates a new `Invitation` record + sends email — no uniqueness check on email, Admin simply invites again if a prior invite expired), `MarinaInvitations GET` (lists all invitations for a given marina via `IInvitationRepository.GetByMarinaIdAsync` — shows status: Pending / Expired / Accepted), `MarinaAdmins GET`, `RevokeAdmin POST` (removes `MarinaAdmin` record; then checks if user has any remaining `MarinaAdmin` records — if none, strips the PlaceOwner role via `UserManager.RemoveFromRoleAsync`), `Settings GET/POST` (load single `AdminSettings` row, allow editing `AutoActionType` and `AutoActionTimeoutHours`); Views: `Admin/Dashboard.cshtml`, `Users.cshtml`, `AllBookings.cshtml`, `AllMarinas.cshtml`, `CreateMarina.cshtml`, `MarinaLayout.cshtml`, `MarinaSpots.cshtml`, `InviteAdmin.cshtml`, `MarinaInvitations.cshtml`, `MarinaAdmins.cshtml`, `Settings.cshtml` | [ ] |
| 6.5 | Repository extensions | `IMarinaRepository.GetAllAsync()` — returns all marinas (no active-spot filter); `ISpotRepository.GetByMarinaIdAsync(marinaId, includeInactive: true)` already exists (Task 3.2) — used by Admin's `MarinaSpots` action with `IgnoreQueryFilters()` | [ ] |

**Dev brief grouping:** Tasks 6.1–6.3 in one `/dev`; Task 6.4–6.5 in a second `/dev`.

---

## Phase 7 — Browse & Search

**Goal:** Anonymous visitors browse marina list. Authenticated users see the interactive marina canvas with spot status. Clicking a free spot on the canvas navigates directly to the booking form. Date-range filter updates spot statuses on the canvas.

**Spot status colors on canvas:** Free = green, Booked (Confirmed/Pending overlap) = red, Unavailable (IsActive=false) = gray, Incompatible (vessel doesn't fit) = orange. Unplaced spots (no canvas coords) shown in a separate sidebar list.

| # | Task | Files | Status |
|---|------|-------|--------|
| 7.1 | Browse ViewModels | `Web/Models/BrowseMarinaListViewModel.cs`, `BrowseMarinaDetailsViewModel.cs`, `SpotStatusViewModel.cs` (id, name, status enum: `Free`/`Booked`/`Unavailable`/`Incompatible`, canvasX, canvasY, canvasW, canvasH, rotation, pricePerDay, spotLengthMeters, spotWidthMeters, spotDepthMeters), `MarinaSearchFilterViewModel.cs` (`Query?`), `SpotAvailabilityFilterViewModel.cs` (`StartDate?`, `EndDate?`, `VesselId?`) | [ ] |
| 7.2 | BrowseController + Views | `Web/Controllers/BrowseController.cs` (no `[Authorize]`) route `[Route("browse")]` — `Index GET` (marina card list + search box — **only marinas where `IsActive = true` AND have at least one active spot are shown**; ES search also scoped to this set), `Marina GET` (renders canvas viewer; if BoatOwner is authenticated, also passes their vessel list as a `<select>` so they can filter by vessel); Views: `Browse/Index.cshtml`, `Browse/Marina.cshtml` | [ ] |
| 7.3 | Spot status JSON endpoint | `GET /browse/marina/{id}/spot-statuses?start=&end=&vesselId=` on `BrowseController` — returns JSON array of `SpotStatusViewModel`; status logic: `IsActive=false` → `Unavailable`; vessel provided and (type not allowed OR dimensions don't fit) → `Incompatible`; overlapping Confirmed/Pending booking → `Booked`; otherwise → `Free`; used by viewer JS | [ ] |
| 7.4 | Marina viewer JS | `Web/wwwroot/js/marina-viewer.js` — Konva.js read-only canvas; loads layout via `GET /marinas/{id}/layout/data`; loads statuses via `GET /browse/marina/{id}/spot-statuses`; colors spots: green/red/gray/orange per status; vessel dropdown onChange re-fetches statuses and redraws; date filter form submits → re-fetches statuses and redraws; clicking a `Free` spot navigates to `/bookings/create?spotId={id}&start=&end=&vesselId=` (pre-fills booking form); `Incompatible` and `Unavailable` spots are not clickable; tooltip on hover shows spot name, price, and dimensions | [ ] |
| 7.5 | Spot status query | `ISpotRepository.GetWithStatusAsync(marinaId, start?, end?, vesselId?)` — active spots left-joined against overlapping Confirmed/Pending bookings; if `vesselId` provided, also left-joins `Vessel` to compute `Incompatible`; update `SpotRepository` | [ ] |
| 7.6 | Update nav | Wire Browse route into `_Layout.cshtml`; add role-specific dashboard links for real | [ ] |

**Dev brief grouping:** Tasks 7.1–7.3 + 7.5 in one `/dev`; Tasks 7.4 + 7.6 in a second `/dev`.

---

## Phase 8 — Polish

**Goal:** Validation, pagination, error pages, landing page. Production-ready hardening.

| # | Task | Files | Status |
|---|------|-------|--------|
| 8.1 | ViewModel validation | All ViewModels: add `[Required]`, `[Range]`, `[StringLength]`; all POST actions: re-populate dropdowns on `ModelState` failure | [ ] |
| 8.2 | PaginatedList helper | `Core/Helpers/PaginatedList.cs` — generic `IQueryable<T>` wrapper; apply to `BrowseController.Index` (marina list), `AdminController.AllBookings`, `AdminController.Users`, `AdminController.AllMarinas` | [ ] |
| 8.3 | Error pages | Update `Views/Shared/Error.cshtml`; add `UseStatusCodePagesWithReExecute("/Home/Error/{0}")` in `Program.cs` | [ ] |
| 8.4 | Home page | Replace default template: `Views/Home/Index.cshtml` (landing with Browse link + auth state); update `HomeController.cs` | [ ] |
| 8.5 | Cleanup check | Verify `Core/Class1.cs` and `Infrastructure/Class1.cs` are deleted (should be done in Phase 1 — this is the final confirmation) | [ ] |
| 8.6 | Dockerfile | Multi-stage `Dockerfile` at repo root: `sdk:10` build stage (`dotnet publish`), `aspnet:10` runtime stage; exposes port 8080; sets `ASPNETCORE_ENVIRONMENT=Production` | [ ] |
| 8.7 | docker-compose.yml | Repo root `docker-compose.yml` for Mode 2 local dev: `sqlserver` (`MSSQL_PID=Express`), `elasticsearch` (`ES_JAVA_OPTS=-Xms256m -Xmx256m`), `kibana`; named volumes for persistence | [ ] |
| 8.8 | k8s manifests | `k8s/base/`: `deployment.yaml` (app ×2, readiness + liveness probes on `/health`), `sqlserver.yaml`, `elasticsearch.yaml`, `kibana.yaml`, `services.yaml`, `pvc.yaml` (sql-data, es-data, uploads-data), `migrate-job.yaml` (EF migration Job), `ingress.yaml`, `secrets-template.yaml`; `k8s/overlays/dev/` (no TLS, lower resource limits); `k8s/overlays/prod/` (cert-manager TLS annotation, full resource limits) | [ ] |

**Dev brief grouping:** Tasks 8.1–8.5 in one `/dev`; Tasks 8.6–8.8 in a second `/dev`.

---

## Cross-Cutting Reminders

| Concern | Rule |
|---------|------|
| Ownership enforcement | PlaceOwner actions check `MarinaAdmin` table for `(marinaId, currentUserId)` — not a single FK. Return `Forbid()` (403) if no record found — never 404. |
| RevokeAdmin role cleanup | After removing a `MarinaAdmin` record, check if the user has any remaining `MarinaAdmin` records. If none remain, remove the PlaceOwner role via `UserManager.RemoveFromRoleAsync` — a user with no marinas has no reason to hold the role. |
| Role-based redirect | `AccountController.Login` POST redirects after auth: Admin → `/admin/dashboard`, PlaceOwner → `/marinas`, BoatOwner → `/browse`. |
| Email confirmation gate | `RequireConfirmedEmail = true` in Identity options. `Login` action explicitly checks `EmailConfirmed` and returns an error if not confirmed. |
| Account active gate | `ApplicationUser.IsActive` checked on every sign-in by overriding `SignInManager<ApplicationUser>`. Disabled user is rejected with an error message. `IsSuperAdmin = true` blocks deletion at service layer. |
| Invite token security | Store SHA-256 hash of token in DB. Send raw token in email link only. Validate by hashing the incoming token and comparing to stored hash. Expire after 48 h. Any invalid token state (not found, expired, already used) → return `NotFound()` (404) — no information leaked to potential brute-forcers. |
| Soft-delete | Both `Marina` and `Spot` have `IsActive` flags. Admin is the only role that can toggle marina active state; PlaceOwner and Admin can toggle spot active state. Neither marinas nor spots are ever hard-deleted. Deactivating does not cancel existing bookings. Admin and PlaceOwner views use `IgnoreQueryFilters()` to see inactive records; Browse never does. |
| Money | `decimal(18,2)` everywhere. |
| Dates | `DateOnly` UTC throughout. Client-side JS handles display timezone conversion if needed. |
| Role seeding | Roles + Admin user + `AdminSettings` seeded in `OnModelCreating` with `HasData`. Fixed GUIDs for idempotency. |
| Migrations | Every entity change requires a new named migration. Dev includes generated files in the deliverable. |
| Booking cancellation | Both BoatOwner and PlaceOwner may cancel a `Pending` or `Confirmed` booking only if `StartDate > today`. Cancelling on or after `StartDate` is rejected for both roles. |
| BoatOwner no-vessel block | A BoatOwner with no registered vessels is blocked from the Browse/spot search flow and shown a prompt to register a vessel first. |
| Price preview | `BookingService` exposes a `PreviewPriceAsync(spotId, vesselId, start, end)` method that runs the full pricing resolution and returns `resolvedPricePerDay`, `resolvedMinBookingDays`, and `totalPrice`. The booking Create view calls this to show a price preview before the BoatOwner submits. |
| Pricing resolution | (1) Match `SpotSeasonalRule` where `rule.StartDate ≤ booking.StartDate ≤ rule.EndDate` → use rule price + MinBookingDays. (2) No match → use `Spot.PricePerDay` + `Spot.DefaultMinBookingDays`. (3) `Spot.PricePerDay` null → fall back to `Marina.DefaultPricePerDay`. `TotalPrice = resolvedPricePerDay × (EndDate − StartDate).Days`. |
| MinBookingDays | Enforced as a validation rule in `BookingService.CreateAsync`. If `(EndDate − StartDate).Days < resolvedMinBookingDays`, return a validation error — do not create the booking. |
| Seasonal rule overlap | Two `SpotSeasonalRule` records for the same spot must not have overlapping date ranges. Enforced at service layer on Create/Edit — reject if any existing rule intersects the new dates. |
| Auto-action | Hangfire recurring job (every 5 min) fires `BookingService.AutoActionAsync()` — finds `Pending` bookings where `CreatedAt + AutoActionTimeoutHours < now` and applies `AdminSettings.AutoActionType` (AutoApprove or AutoReject). |
| Booking completion | Hangfire recurring job (nightly `"0 2 * * *"`) fires `BookingService.CompleteOverdueAsync()` — transitions `Confirmed` → `Completed` where `EndDate < today`. |
| Hangfire dashboard | Mounted at `/hangfire`, restricted to Admin role. Shows job history, retries, and recurring job schedules. Uses same SQL Server DB — no extra infrastructure. |
| Elasticsearch sync | Any action that changes a marina's visible state must sync the ES index: edit → `IndexAsync`, deactivate → `DeleteAsync`, reactivate → `IndexAsync`. Applies to both PlaceOwner (`MarinasController`) and Admin (`AdminController`) write paths. ES is search-only; SQL Server is source of truth. |
| `/security-review` | Required before merging: Phase 5 Tasks 5.5–5.6, Phase 6 Task 6.2. |
| `/simplify` | Run on BookingsController, SpotBookingsController, AdminController when any exceeds 150 lines. |

### Canvas / Visual Layout

| Concern | Rule |
|---------|------|
| Canvas library | **Konva.js** via CDN. Include only on views that use it (`Layout.cshtml`, `Browse/Marina.cshtml`, `Admin/MarinaLayout.cshtml`). |
| Coordinate system | Logical units 0–LayoutWidth × 0–LayoutHeight (default 1200×800). JS scales to rendered size client-side. Never store pixel values tied to screen resolution. |
| Canvas position nullable | `CanvasX/Y/W/H/Rotation` are all nullable. A spot may exist without being placed. Unplaced spots appear in a sidebar list, not on the canvas. |
| Background image | Stored under `wwwroot/uploads/marina-backgrounds/{id}.{ext}`. Validate type (jpg/png/webp) and max size (5 MB) in controller. Serve as static files. |
| Spot status derivation | Free = active + no overlapping Confirmed/Pending booking. Booked = has overlap. Unavailable = `IsActive=false`. Computed in the repository/service, never stored. |
| Shared JS | `marina-viewer.js` is used by Browse, Admin, and PlaceOwner read-only views. `marina-editor.js` is PlaceOwner-only. Keep them separate files. |
| Save positions | The editor POSTs a JSON array to `SavePositions` in bulk on user action — not on every drag event. |

---

## Infrastructure & Deployment

**Production environment:** Single Debian VM on DigitalOcean, k3s Kubernetes cluster.
**Local environment:** WSL2 Debian on Windows dev machine, same k3s setup mirroring production.

### Kubernetes Pods

| Pod | Image | Notes |
|-----|-------|-------|
| `app` × 2 | Custom — built from `Dockerfile` in repo | ASP.NET Core 10; pulls config from Secrets |
| `sqlserver` × 1 | `mcr.microsoft.com/mssql/server:2022-latest` | `MSSQL_PID=Express`; needs PVC for `/var/opt/mssql` |
| `elasticsearch` × 1 | `docker.elastic.co/elasticsearch/elasticsearch:8.x` | `discovery.type=single-node`; needs PVC for `/usr/share/elasticsearch/data` |
| `kibana` × 1 | `docker.elastic.co/kibana/kibana:8.x` | Points to ES service |
| `nginx-ingress` | Already running | Reverse proxy + TLS termination |
| `cert-manager` | Already running | Let's Encrypt certificate automation |

### Additional Kubernetes Resources Needed

| Resource | Purpose |
|----------|---------|
| `PersistentVolumeClaim` (sql-data) | SQL Server data persistence across pod restarts |
| `PersistentVolumeClaim` (es-data) | Elasticsearch index persistence |
| `PersistentVolumeClaim` (uploads-data) | Marina background images — mounted to **both** app pods at `/app/wwwroot/uploads`; swap for Azure Blob / DO Spaces by changing `IFileStorageService` impl and removing this PVC |
| `Secret` (app-secrets) | `ConnectionStrings__DefaultConnection`, `Elasticsearch__Uri`, admin seed password, ASP.NET Core Data Protection key |
| `ConfigMap` (app-config) | Non-sensitive config (e.g. `ASPNETCORE_ENVIRONMENT=Production`) |
| `Job` (db-migrate) | Runs `dotnet ef database update` on each deploy before app pods start — ensures migrations are applied before traffic hits the app |
| `Ingress` resource | Routes HTTPS traffic from Nginx to the app service; cert-manager annotation for Let's Encrypt |
| Container registry | Docker Hub / DigitalOcean Container Registry — app image is pushed here on each build, pulled by k8s on deploy |

### SQL Server Express

- **10 GB/database limit** — ample for this domain at any realistic scale
- **1 GB buffer pool** — the practical RAM constraint; monitor with Kibana if needed
- **Upgrade path:** change `MSSQL_PID=Express` → `Standard` in the pod spec and restart. Same image, same PVC, same connection string. No data migration required.
- **Dev vs. prod:** LocalDB on Windows for local development; SQL Express Linux container in k8s for production.

### Local Development — Three Modes

**Mode 1 — Daily coding** (fastest, no containers)
- `dotnet run` on Windows, connecting to LocalDB
- Use this for all feature work — hot reload, instant feedback
- Elasticsearch not running; ES-dependent features (marina search) stubbed or skipped during this mode

**Mode 2 — Services** (test ES integration without full k8s)
- `docker-compose up` in WSL2 starts SQL Express + Elasticsearch + Kibana
- App still runs on Windows via `dotnet run`, `appsettings.Development.json` points to WSL2 IP
- Use this when working on Phase 3b (ES indexing) or any feature that needs a real DB + ES

**Mode 3 — Full infra** (validate k8s manifests before deploying to DO)
- Build Docker image locally, deploy to k3s running in WSL2 Debian
- Same manifests as production via Kustomize `dev` overlay (no TLS, plain HTTP, lower resource limits)
- Add `127.0.0.1 boatspotfinder.local` to Windows `hosts` file to reach Ingress from browser
- Use this before every push to production

### Kustomize Structure

```
k8s/
  base/                    # shared manifests (deployments, services, PVCs)
  overlays/
    dev/                   # WSL2 local: no TLS, HTTP only, ES heap 256m
    prod/                  # DigitalOcean: cert-manager TLS, full resource limits
```

### docker-compose.yml (Mode 2)

Services: `sqlserver` (`MSSQL_PID=Express`), `elasticsearch` (`ES_JAVA_OPTS=-Xms256m -Xmx256m`), `kibana`. Named volumes for data persistence. Lives at repo root.

### WSL2 Setup Prerequisites

- WSL2 with Debian distro
- Docker Desktop on Windows with WSL2 integration enabled (for image builds)
- k3s installed inside WSL2 Debian: `curl -sfL https://get.k3s.io | sh -`
- `kubectl` configured to point to WSL2 k3s context for infra testing

### Deployment Workflow (per release to production)

1. `docker build` → push image to registry with new tag
2. Apply `db-migrate` Job → wait for completion
3. `kubectl rollout restart deployment/app` → k8s pulls new image, rolls pods

**CI/CD:** `[ PENDING ]` — Skipped for initial delivery. Manual build + deploy while developing locally and on WSL2 k3s dev VM. Add GitHub Actions pipeline (test → build → push → deploy) once the dev environment is stable.

---

## Verification Checklist (End-to-End)

After all phases complete:

- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test` — all tests pass
- [ ] Anonymous user: can browse marina list; cannot access any role-gated page
- [ ] BoatOwner: can register, log in, add a vessel with dimensions, select vessel on Browse canvas (incompatible spots turn orange), create and cancel a booking — booking rejected if vessel dimensions exceed spot dimensions
- [ ] PlaceOwner: registration pending → Admin approves → can create marina, open layout editor, upload background image, draw spots as rectangles, save positions, set spot metadata, confirm/reject bookings
- [ ] Admin: can log in with seeded credentials, approve PlaceOwner, view all marinas + open read-only canvas for any marina, deactivate spot, view all bookings
- [ ] Marina canvas (Browse): free spots shown green, booked red, unavailable gray; clicking a free spot navigates to booking form
- [ ] Date-range filter on Browse/Marina view updates spot colors correctly
- [ ] Booking overlap: two bookings for the same spot on overlapping dates rejected correctly
- [ ] Soft-delete: deactivated spot gray on canvas, invisible to availability queries
- [ ] Background image: uploaded image renders behind spots on canvas in all three views (editor, viewer, admin)
