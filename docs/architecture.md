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
| Health checks | EF Core DbContext check + optional Elasticsearch check | — |

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
- **Services** (`Core/Services/`) — business logic. Added from Phase 2 onwards.

NuGet dependency: `Microsoft.Extensions.Identity.Core` only.

### Infrastructure (`BoatSpotFinder.Infrastructure`)

- **`Data/AppDbContext.cs`** — extends `IdentityDbContext<ApplicationUser>` and `IDataProtectionKeyContext`. Registers all DbSets. Overrides `SaveChanges` and `SaveChangesAsync` to maintain `BaseEntity.UpdatedAt`. Seeds roles, admin user, and `AdminSettings` via `HasData`.
- **`Data/Configurations/`** — one `IEntityTypeConfiguration<T>` file per entity. `BaseEntityConfiguration<T>` is an abstract generic base that sets `HasDefaultValueSql("GETUTCDATE()")` on both timestamp columns; concrete configs inherit it and call `base.Configure(builder)` first.
- **`Repositories/`** — concrete repository implementations.
- **`Migrations/`** — EF Core migration files. Never hand-edited.

NuGet dependencies: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Tools`.

### Web (`BoatSpotFinder.Web`)

- **`Program.cs`** — DI registration and middleware pipeline (see below).
- **`Controllers/`** — MVC controllers. Added from Phase 2 onwards.
- **`Views/`** — Razor views. Added from Phase 2 onwards.
- **`Infrastructure/`** — web-layer infrastructure (e.g. `HangfireAdminAuthFilter`).

NuGet dependencies: `Hangfire.Core`, `Hangfire.SqlServer`, `Hangfire.AspNetCore`, `AspNetCore.HealthChecks.Elasticsearch`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.

---

## Program.cs — DI Registration Order

1. `Configure<AppSettings>(...)` — bind `AppSettings:BaseUrl`.
2. `AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))` — MVC + global CSRF on all state-changing verbs.
3. `AddDbContext<AppDbContext>(UseSqlServer)` — reads `ConnectionStrings:DefaultConnection`.
4. `AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders()` — uses `AddIdentity` (not `AddDefaultIdentity`) so that Phase 2's `CustomSignInManager` can be chained.
5. `AddDataProtection().PersistKeysToDbContext<AppDbContext>()`.
6. `AddScoped<IAdminSettingsRepository, AdminSettingsRepository>()`.
7. `AddHangfire(c => c.UseSqlServerStorage(...))` + `AddHangfireServer()`.
8. `AddHealthChecks().AddDbContextCheck<AppDbContext>()` + conditional `AddElasticsearch(esUri)` when `Elasticsearch:Uri` is configured.

---

## Program.cs — Middleware Pipeline

```
HttpsRedirection
StaticFiles
Routing
Authentication
Authorization
UseHangfireDashboard("/hangfire")
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

## Hangfire

Dashboard at `/hangfire`. Protected by `HangfireAdminAuthFilter` (`Web/Infrastructure/HangfireAdminAuthFilter.cs`), which returns true only when `User.IsInRole("Admin")`. Uses the same SQL Server database as the application — no separate Hangfire store.

---

## Health Checks

Endpoint: `GET /health`. Always includes an EF Core `DbContextCheck<AppDbContext>`. The Elasticsearch check is registered only when `Elasticsearch:Uri` is present in configuration, so the endpoint reports healthy in development (Mode 1, no Docker, no ES).

---

## Frontend / Design System

No CSS framework is used. All views designed via `/frontend-design` use only custom CSS — Bootstrap classes are not referenced in any view produced by the design workflow. (`wwwroot/lib/bootstrap/` still exists on disk pending a cleanup pass; `Home/Index.cshtml`, `Home/Privacy.cshtml`, and `Shared/Error.cshtml` still carry Bootstrap classes and are queued for that pass.)

**Single stylesheet:** `src/BoatSpotFinder.Web/wwwroot/css/site.css` (~598 lines).

**Token system.** The top of `site.css` defines CSS custom properties on `:root` for:
- Palette — `--ink`, `--brass`, `--sand`, `--cream`, `--slate`, `--alert`, `--notice-bg`, and their variants.
- Typography — `--font-display` (Fraunces), `--font-body` (Manrope).
- Radii — `--r-xs` through `--r-lg`.
- Shadows — `--shadow-card`, `--shadow-btn`.
- Lines — `--hairline`, `--hairline-deep`, `--hairline-ink`.

**Typography.** Fraunces (display serif, weights 300–600) and Manrope (sans body, weights 400–700) are loaded from Google Fonts via two `<link>` tags in `_Layout.cshtml`. Fallbacks defined in `--font-display` and `--font-body` tokens.

**Component naming.** BEM-style: block (`auth-card`), element (`auth-card__head`), modifier (`auth-card--narrow`). Block names reflect the component's role, not its visual style.

**Accessibility.** `@media (prefers-reduced-motion: reduce)` at the end of the animation section sets `opacity: 1; transform: none; animation: none` on `.auth-card__head > *` and `.form > *`, and collapses all transitions to `0.01ms`.

**Responsive.** A single breakpoint at `max-width: 720px` at the end of `site.css` adjusts the header layout, hides `.brand__tag` and `.auth-strip__greeting`, and reduces padding on `.auth-card`, `.site-main`, and `.site-footer`.

---

## Configuration Files

| File | Committed | Purpose |
|---|---|---|
| `src/BoatSpotFinder.Web/appsettings.json` | Yes | Logging defaults; `ConnectionStrings:DefaultConnection` is an empty placeholder. |
| `src/BoatSpotFinder.Web/appsettings.Development.json` | No (git-ignored) | LocalDB connection string; `AppSettings:BaseUrl = "https://localhost:5001"`. |

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
