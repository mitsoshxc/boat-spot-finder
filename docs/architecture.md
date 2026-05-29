# Architecture

## Tech Stack

| Concern | Technology | Version |
|---|---|---|
| Framework | ASP.NET Core MVC | 10.0 |
| ORM | Entity Framework Core | 10.0 |
| Database | SQL Server / LocalDB (dev) | — |
| Identity | ASP.NET Core Identity | 10.0 |
| Background jobs | Hangfire | 1.8.20 |
| Data protection | `PersistKeysToDbContext` (same SQL Server DB) | 10.0 |
| Health checks | EF Core DbContext check | — |
| Search | Elasticsearch (optional) via `Elastic.Clients.Elasticsearch` | 8.19.22 |

---

## Solution Structure

```
BoatSpotFinder.slnx
src/
  BoatSpotFinder.Core/          — domain layer (no EF, no ASP.NET deps)
  BoatSpotFinder.Infrastructure/ — EF Core, repositories, migrations
  BoatSpotFinder.Web/           — MVC controllers, views, Program.cs
tests/
  BoatSpotFinder.Tests/
```

Project references: `Web` → `Infrastructure` → `Core`. `Core` has no project references.

---

## Layer Responsibilities

### Core (`BoatSpotFinder.Core`)

- **Entities** (`Core/Entities/`) — domain classes and enums. No EF attributes. `ApplicationUser` extends `IdentityUser` from `Microsoft.Extensions.Identity.Core` (no EF dependency).
- **Interfaces** (`Core/Interfaces/`) — repository and service contracts.
- **Settings** (`Core/Settings/`) — strongly-typed options classes bound from `appsettings.json` (e.g. `AppSettings`).
- **Helpers** (`Core/Helpers/`) — pure static utilities (e.g. `TokenHasher`).
- **Services** (`Core/Services/`) — business logic. `SpotSeasonalRuleService` is the first concrete service (Phase 3). All services return `ServiceResult` from `Core/Common/`.
- **Common** (`Core/Common/`) — cross-cutting types shared across services. Currently contains `ServiceResult`.
- **Models** (`Core/Models/`) — input/transfer records passed across layer boundaries (e.g. `SpotPositionUpdate`, `SpotSeasonalRuleInput`). Not ViewModels — these live in `Web/Models/`.

NuGet dependency: `Microsoft.Extensions.Identity.Core` only.

### Infrastructure (`BoatSpotFinder.Infrastructure`)

- **`Data/AppDbContext.cs`** — extends `IdentityDbContext<ApplicationUser>` and `IDataProtectionKeyContext`. Registers all DbSets. Overrides `SaveChanges` and `SaveChangesAsync` to maintain `BaseEntity.UpdatedAt`. Seeds roles, admin user, and `AdminSettings` via `HasData`.
- **`Data/Configurations/`** — one `IEntityTypeConfiguration<T>` file per entity. `BaseEntityConfiguration<T>` is an abstract generic base that sets `HasDefaultValueSql("GETUTCDATE()")` on both timestamp columns; concrete configs inherit it and call `base.Configure(builder)` first.
- **`Repositories/`** — concrete repository implementations.
- **`Search/`** — Elasticsearch integration. Contains `ElasticsearchMarinaSearchService` (real impl, uses `Elastic.Clients.Elasticsearch`) and `NullMarinaSearchService` (stub, always returns null from `SearchAsync`). Both implement `IMarinaSearchService` from `Core/Interfaces/`.
- **`Migrations/`** — EF Core migration files. Never hand-edited.

NuGet dependencies: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Tools`, `Elastic.Clients.Elasticsearch` (8.19.22).

### Web (`BoatSpotFinder.Web`)

- **`Program.cs`** — DI registration and middleware pipeline (see below).
- **`Controllers/`** — MVC controllers. Added from Phase 2 onwards. Phase 3 adds `MarinasController` (`placeowner/marinas`), `SpotsController` (`placeowner/marinas/{marinaId}/spots`), `SpotSeasonalRulesController` (`placeowner/marinas/{marinaId}/spots/{spotId}/seasonal-rules`), and `BrowseController` (`browse/marina/{id}/layout-data`). Phase 4 adds `VesselsController` (`vessels`) — `[Authorize(Roles="BoatOwner")]`, actions: Index, Create GET/POST, Edit GET/POST, Delete POST. Phase 5 adds `BookingsController` (`bookings`) — `[Authorize(Roles="BoatOwner")]`, actions: MyBookings GET, Create GET/POST, Cancel POST; and `SpotBookingsController` (`placeowner/spot-bookings`) — `[Authorize(Roles="PlaceOwner")]`, actions: Incoming GET, Confirm POST, Reject POST, Cancel POST.
- **`Views/`** — Razor views. Added from Phase 2 onwards.
- **`Infrastructure/`** — web-layer infrastructure: `HangfireAdminAuthFilter`, `CustomSignInManager`, and `Storage/LocalFileStorageService` (writes marina background images to `wwwroot/uploads/marina-backgrounds/`).
- **`wwwroot/js/`** — client-side JavaScript. `marina-editor.js` drives the PlaceOwner canvas layout editor (Konva stage, Add Spot modal, SavePositions POST, snap/overlap collision logic, sidebar delete modal, clear background modal, fullscreen toggle). Konva.js is loaded from CDN (`https://unpkg.com/konva@9/konva.min.js`) on the views that use the canvas — it is not bundled. See [`conventions.md`](conventions.md) § Canvas / Visual Layout for the full canvas ruleset.

NuGet dependencies: `Hangfire.Core`, `Hangfire.SqlServer`, `Hangfire.AspNetCore`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.

---

## Program.cs — DI Registration Order

1. `Configure<AppSettings>(...)` — bind `AppSettings:BaseUrl`.
2. `Configure<SmtpOptions>(...)` — bind `Smtp` section.
3. `AddScoped<IEmailSender, ConsoleEmailSender>()` (Development) or `SmtpEmailSender` (other environments).
4. `AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))` — MVC + global CSRF on all state-changing verbs.
5. `AddDbContext<AppDbContext>(UseSqlServer)` — reads `ConnectionStrings:DefaultConnection`.
6. `AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders().AddSignInManager<CustomSignInManager>()` — uses `AddIdentity` (not `AddDefaultIdentity`) to allow the custom sign-in manager chain.
7. `ConfigureApplicationCookie(...)` — sets `LoginPath` and `AccessDeniedPath`.
8. `AddDataProtection().PersistKeysToDbContext<AppDbContext>()`.
9. Scoped repository and service registrations: `IAdminSettingsRepository`, `IInvitationRepository`, `IMarinaAdminRepository`, `IAuditLogger`, `IMarinaRepository`, `ISpotRepository`, `ISpotSeasonalRuleRepository`, `ISpotSeasonalRuleService`, `IFileStorageService`, `IVesselRepository`, `IBookingRepository`, `IBookingService`. (`IMarinaSearchService` is registered in step 10.)
10. Elasticsearch config guard — reads `Elasticsearch:Uri` from configuration. If blank: registers `NullMarinaSearchService` as `IMarinaSearchService` (scoped). If set: registers `ElasticsearchClient` as singleton (with `DefaultIndex("marinas")`) then `ElasticsearchMarinaSearchService` as `IMarinaSearchService` (scoped).
11. `AddHangfire(c => c.UseSqlServerStorage(...))` + `AddHangfireServer()`.
12. `AddHealthChecks().AddDbContextCheck<AppDbContext>()`.

---

## Program.cs — Middleware Pipeline

```
HttpsRedirection
StaticFiles
Routing
Authentication
Authorization
UseHangfireDashboard("/hangfire")
RecurringJob.AddOrUpdate "booking-auto-action"   (*/5 * * * *)
RecurringJob.AddOrUpdate "booking-complete-overdue"  (0 2 * * *)
MapControllerRoute (default: {controller=Home}/{action=Index}/{id?})
MapHealthChecks("/health")
```

Auto-migration block runs after `app.Build()`, before `app.Run()`, in `Development` only:

```csharp
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

This block replaces the need to run `dotnet ef database update` manually during development. It is not active in production.

---

## Elasticsearch Startup Seed

After `app.MapHealthChecks("/health")` and before `app.Run()`, a startup seed block re-indexes all active marinas into Elasticsearch on each process start:

```csharp
app.Lifetime.ApplicationStarted.Register(() => Task.Run(async () =>
{
    // loads all IsActive marinas and calls IMarinaSearchService.IndexAsync on each
}));
```

The `Task.Run` wrapper is intentional: `Register` accepts `Action`, so a bare `Register(async () => ...)` would be async-void and silently swallow exceptions. Exceptions surface via `ILogger<Program>`. When `NullMarinaSearchService` is active (no ES URI configured), all calls are no-ops. The seed is idempotent — running it repeatedly only refreshes the index.

---

## Hangfire

Dashboard at `/hangfire`. Protected by `HangfireAdminAuthFilter` (`Web/Infrastructure/HangfireAdminAuthFilter.cs`), which returns true only when `User.IsInRole("Admin")`. Uses the same SQL Server database as the application — no separate Hangfire store.

Two recurring jobs are registered immediately after `UseHangfireDashboard` in `Program.cs`:

| Job id | Schedule | Method | Effect |
|---|---|---|---|
| `booking-auto-action` | `*/5 * * * *` (every 5 min) | `IBookingService.AutoActionAsync()` | Auto-confirms or auto-rejects Pending bookings whose `CreatedAt + AdminSettings.AutoActionTimeoutHours < UtcNow`, depending on `AdminSettings.AutoActionType`. |
| `booking-complete-overdue` | `0 2 * * *` (02:00 UTC daily) | `IBookingService.CompleteOverdueAsync()` | Transitions Confirmed bookings to Completed where `EndDate < today`. |

Both jobs are registered using the typed generic overload `RecurringJob.AddOrUpdate<IBookingService>(...)` so Hangfire resolves `IBookingService` through DI at execution time.

---

## Health Checks

Endpoint: `GET /health`. Includes an EF Core `DbContextCheck<AppDbContext>`. No Elasticsearch health check is registered — ES connectivity is not monitored at the `/health` endpoint; index drift is recovered by the startup seed (see § Elasticsearch Startup Seed above).

---

## Frontend / Design System

No CSS framework, no client-side validation library, no jQuery. All views use only custom CSS defined in `wwwroot/css/site.css`. `wwwroot/lib/` is empty — Bootstrap, jQuery, jquery-validation, and jquery-validation-unobtrusive have been removed.

**Single stylesheet:** `src/BoatSpotFinder.Web/wwwroot/css/site.css`.

**Button variants.** Beyond the base `.btn` and `.btn--sm` / `.btn--ghost` modifiers, `site.css` defines `.btn--danger` for destructive actions. It renders with `color: var(--alert)` and a transparent background; on hover the background fills to `var(--alert-bg)` and the border sharpens to `var(--alert)`. Added in Phase 4 for the vessel Delete action.

**Token system.** The top of `site.css` defines CSS custom properties on `:root` for:
- Palette — `--ink`, `--brass`, `--sand`, `--cream`, `--slate`, `--alert`, `--notice-bg`, and their variants.
- Typography — `--font-display` (Fraunces), `--font-body` (Manrope).
- Radii — `--r-xs` through `--r-lg`.
- Shadows — `--shadow-card`, `--shadow-btn`.
- Lines — `--hairline`, `--hairline-deep`, `--hairline-ink`.

**Typography.** Fraunces (display serif, weights 300–600) and Manrope (sans body, weights 400–700) are loaded from Google Fonts via two `<link>` tags in `_Layout.cshtml`. Fallbacks defined in `--font-display` and `--font-body` tokens.

**Component naming.** BEM-style: block (`auth-card`), element (`auth-card__head`), modifier (`auth-card--narrow`). Block names reflect the component's role, not its visual style.

**Accessibility.** `@media (prefers-reduced-motion: reduce)` at the end of the animation section sets `opacity: 1; transform: none; animation: none` on `.auth-card__head > *`, `.form > *`, `.hero > *`, and `.prose > *`, and collapses all transitions to `0.01ms`.

**Responsive.** Mobile-first. Default rules target small viewports. Two `@media (min-width: 720px)` blocks layer on tablet enhancements (the header nav goes inline, `.brand__tag` and `.auth-strip__greeting` become visible, paddings on `.auth-card` / `.site-main` / `.site-footer` / `.hero` / `.prose` expand, `.hero__actions` switches from stacked column to row). A `@media (min-width: 960px)` block covers the workspace editor's two-column layout and sticky sidebar. No `max-width` media queries remain in `site.css`. See [`conventions.md`](conventions.md) § Responsive design for the full ruleset.

**Validation.** Forms validate server-side only via ASP.NET Core ModelState. Each `<form>` carries the `novalidate` attribute to suppress the browser's default tooltip-style errors, so all errors render through `asp-validation-summary` (top-of-form `.auth-errors` block) and `asp-validation-for` (per-field `.field__error` span) on the next page load after a failed POST. No client-side JavaScript validation runs on text/ModelState forms. **File input validation is the one carve-out**: file uploads pre-validate size and MIME client-side (e.g. the marina background-image input in `marina-editor.js` checks `image/jpeg|png|webp`, allowed extensions, and the 5 MB limit before POSTing). Server-side validation in the controller stays as defense-in-depth and remains authoritative — the JS check exists so the user does not round-trip a multi-megabyte file just to see a "too big" error.

---

## Configuration Files

| File | Committed | Purpose |
|---|---|---|
| `src/BoatSpotFinder.Web/appsettings.json` | Yes | Logging defaults; `ConnectionStrings:DefaultConnection` is an empty placeholder; `Elasticsearch:Uri` is an empty string (ES off). |
| `src/BoatSpotFinder.Web/appsettings.Development.json` | No (git-ignored) | LocalDB connection string; `AppSettings:BaseUrl = "https://localhost:5001"`; `Elasticsearch:Uri = "http://localhost:9200"` when running with Docker. |

Never commit a real connection string to `appsettings.json`. Production secrets are injected via environment variables or k8s Secrets (Phase 8).

---

## Key Commands

```bash
# Build
dotnet build

# Add a migration
dotnet ef migrations add <Name> \
  --project src/BoatSpotFinder.Infrastructure \
  --startup-project src/BoatSpotFinder.Web

# Apply migration manually (CI / k8s, outside the app)
dotnet ef database update \
  --project src/BoatSpotFinder.Infrastructure \
  --startup-project src/BoatSpotFinder.Web

# Run (auto-migrates in Development)
dotnet run --project src/BoatSpotFinder.Web
```
