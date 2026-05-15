# Conventions

These rules apply to every file in the codebase. The Dev agent enforces them on every brief.

---

## Layering

| Rule | Detail |
|---|---|
| Entities live only in `Core/Entities/` | No EF attributes on entity classes. |
| Repository interfaces live in `Core/Interfaces/` | Concrete implementations in `Infrastructure/Repositories/`. |
| Service interfaces live in `Core/Interfaces/` | Concrete implementations in `Core/Services/`. |
| EF configurations live in `Infrastructure/Data/Configurations/` | One file per entity; inherit `BaseEntityConfiguration<T>` for entities that extend `BaseEntity`. |
| `AppDbContext` lives in `Infrastructure/Data/` | Never in `Core` or `Web`. |
| Controllers and views live in `Web` | No business logic in controllers — delegate to service layer. |
| ViewModels live in `Web/Models/` | Never pass entity objects directly to views. |

`Core` must not reference `Infrastructure` or `Web`. `Infrastructure` must not reference `Web`.

---

## Naming

| Concern | Convention | Example |
|---|---|---|
| Controller | `{Feature}Controller` | `BookingController`, `AdminController` |
| ViewModel | `{Feature}{Action}ViewModel` | `BookingCreateViewModel`, `SpotEditViewModel` |
| Repository interface | `I{Entity}Repository` | `IAdminSettingsRepository` |
| Service interface | `I{Feature}Service` | `IBookingService` |
| EF configuration class | `{Entity}Configuration` | `BookingConfiguration` |
| Migration | `{YYYYMMDDHHmmss}_{PascalName}` | `20260515075443_InitialCreate` |

---

## Entities

- All entities except `ApplicationUser` and `MarinaAdmin` inherit `BaseEntity` (`Id` Guid, `CreatedAt` DateTime UTC, `UpdatedAt` DateTime UTC).
- `ApplicationUser` extends `IdentityUser` (string PK). FK columns pointing at `ApplicationUser` are always `string`, never `Guid`.
- `MarinaAdmin` is a join table with no `BaseEntity` — composite PK `(MarinaId, UserId)`.
- Navigation properties use collection initializers: `public ICollection<Spot> Spots { get; set; } = [];`
- Required navigation properties use null-forgiving: `public Marina Marina { get; set; } = null!;`

---

## Money and Decimals

- All price/money columns: `decimal` with `.HasPrecision(18, 2)` in the EF configuration.
- Do not store currency as `float` or `double`.

---

## Dates and Times

- `DateTime` properties that represent an absolute moment in time use UTC (`DateTime.UtcNow`).
- `DateOnly` for date-only fields (`StartDate`, `EndDate`, seasonal rule boundaries). EF Core 10 + SQL Server supports `DateOnly` natively.
- `DateTimeOffset` for fields that carry timezone context (`MarinaAdmin.InvitedAt`, `Invitation.ExpiresAt`).

---

## EF Configurations

- Every entity that extends `BaseEntity` gets a configuration class that inherits `BaseEntityConfiguration<T>`.
- The concrete `Configure` method calls `base.Configure(builder)` as its first line. This is the only way the `GETUTCDATE()` defaults are applied — EF Core does not discover abstract generic base classes via `ApplyConfigurationsFromAssembly`.
- `BaseEntityConfiguration<T>` sets `HasDefaultValueSql("GETUTCDATE()")` on both `CreatedAt` and `UpdatedAt`. This is a DB-level safety net; application code sets timestamps via `AppDbContext.SetTimestamps()`.
- Entities that do not extend `BaseEntity` (`ApplicationUser`, `MarinaAdmin`) implement `IEntityTypeConfiguration<T>` directly.

---

## Soft Delete

- `Marina.IsActive` and `Spot.IsActive` are the soft-delete flags.
- `Spot` has a global EF query filter (`s => s.IsActive`). All queries exclude inactive spots by default. Use `IgnoreQueryFilters()` in admin contexts that need to see inactive spots.
- Deactivating a marina or spot does not cancel existing bookings.

---

## Controllers

- Every controller that requires authentication is decorated with `[Authorize(Roles = "...")]` at the class level.
- No per-action `[ValidateAntiForgeryToken]` — the global `AutoValidateAntiforgeryTokenAttribute` registered in `Program.cs` covers all state-changing verbs.
- No business logic in controllers. Read input, call a service, redirect or return a view.
- Return `NotFound()` / `Forbid()` / `BadRequest()` from controllers, not raw status codes.

---

## CSRF

`AutoValidateAntiforgeryTokenAttribute` is registered globally in `Program.cs`. It validates on POST, PUT, PATCH, DELETE and skips GET, HEAD, OPTIONS, TRACE. It reads the token from either the form field or the `RequestVerificationToken` request header. No per-action attribute needed.

---

## Security — Tokens

Raw invite tokens are never stored in the database. Only the SHA-256 hash is stored (`Invitation.Token`). `Core/Helpers/TokenHasher.Hash(rawToken)` performs the hash. The raw token is sent in the email link only.

---

## Seed Data

- Seed rows use fixed, deterministic GUIDs so migrations are idempotent.
- The admin password hash in `AppDbContext` seed is a hardcoded PBKDF2 literal. Do not call `PasswordHasher` inside `OnModelCreating` — the random salt produces a new hash on every `dotnet ef migrations add`, creating spurious update migrations.
- `HasData` requires explicit values for `CreatedAt` and `UpdatedAt` even when the column has `HasDefaultValueSql("GETUTCDATE()")` — EF Core does not apply DB defaults to seeded rows.

---

## Comments

Do not add comments unless the reason behind the code is non-obvious. "What" comments are noise. "Why" comments are acceptable when the behavior would otherwise be misread as a bug (e.g. explaining why `Restrict` is used instead of `Cascade` on booking FKs).

---

## Build Verification

Every Dev agent brief must end with a successful `dotnet build` before the agent reports done.
