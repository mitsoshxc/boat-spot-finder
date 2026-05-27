# Domain Model

## Roles

Three ASP.NET Core Identity roles are seeded in `AppDbContext.OnModelCreating`. They are not runtime-created.

| Role | Fixed GUID | Description |
|---|---|---|
| `Admin` | `20000000-...-001` | Full application management. |
| `PlaceOwner` | `20000000-...-002` | Marina ownership. Accounts created via invite only. |
| `BoatOwner` | `20000000-...-003` | Self-registered. Can create bookings. |

`ApplicationUser.IsSuperAdmin` is a bool column, not an Identity role. It is used at the service layer to block deletion of the seeded admin account. It has no effect on `User.IsInRole(...)` checks.

---

## Entities

### BaseEntity (abstract)

All domain entities except `ApplicationUser` and `MarinaAdmin` inherit `BaseEntity`.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key. |
| `CreatedAt` | `DateTime` (UTC) | Set by `AppDbContext.SetTimestamps()` on insert. DB default `GETUTCDATE()` applies only to seed rows. |
| `UpdatedAt` | `DateTime` (UTC) | Updated by `SetTimestamps()` on every save. |

### ApplicationUser

Extends `IdentityUser` (string PK).

| Property | Type | Notes |
|---|---|---|
| `IsActive` | `bool` | Default `true`. Checked on every sign-in; inactive users are rejected. |
| `IsSuperAdmin` | `bool` | Default `false`. Set only on the seeded admin. |
| `AverageRatingAsBoatOwner` | `decimal?` | **Deferred to Phase 5b.** Do not add until task 5b.1. |
| `ReviewCountAsBoatOwner` | `int` | **Deferred to Phase 5b.** Do not add until task 5b.1. |

Seeded admin: `admin@boatspotfinder.com`, `Id = "30000000-0000-0000-0000-000000000001"`, `IsSuperAdmin = true`. Password hash is a hardcoded PBKDF2 literal (must be changed on first login).

### Marina

| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | |
| `Description` | `string` | |
| `Address` | `string` | |
| `Region` | `string` | |
| `Phone` | `string` | |
| `Latitude` / `Longitude` | `double` | Geographic coordinates. |
| `DefaultPricePerDay` | `decimal (18,2)` | Fallback when `Spot.PricePerDay` is null. |
| `IsActive` | `bool` | Default `true`. Soft-delete. Deactivating hides marina from Browse; existing bookings are not cancelled. |
| `LayoutWidth` / `LayoutHeight` | `int` | Default 1200 / 800. Defines the canvas coordinate space for spot layout. |
| `BackgroundImagePath` | `string?` | Null when no image uploaded. Cleared via `MarinasController.ClearBackground`; deletion from storage handled before nulling the field. |
| `AverageRating` | `decimal?` | **Deferred to Phase 5b.** |
| `ReviewCount` | `int` | **Deferred to Phase 5b.** |

Ownership: via `MarinaAdmin` join table — no direct `PlaceOwnerId` FK.

### MarinaAdmin (join table — no BaseEntity)

Composite PK: `(MarinaId, UserId)`.

| Property | Type | Notes |
|---|---|---|
| `MarinaId` | `Guid` FK → `Marina` | Cascade delete. |
| `UserId` | `string` FK → `ApplicationUser` | Cascade delete. |
| `InvitedAt` | `DateTimeOffset` | |
| `InvitedById` | `string` FK → `ApplicationUser` | Restrict delete (see FK rules below). |

### Spot

| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | |
| `Description` | `string` | |
| `LengthMeters` / `WidthMeters` / `DepthMeters` | `double` | Physical dimensions. |
| `PricePerDay` | `decimal (18,2)?` | Nullable. Null = use `Marina.DefaultPricePerDay`. |
| `DefaultMinBookingDays` | `int` | |
| `IsActive` | `bool` | Default `false`. Global query filter (`s => s.IsActive`) applied in `SpotConfiguration`. Inactive spots are excluded from all queries unless the filter is explicitly ignored. |
| `AllowedVesselTypes` | `VesselType` (flags int) | `None = 0` means no restriction. |
| `MarinaId` | `Guid` FK → `Marina` | |
| `CanvasX` / `CanvasY` / `CanvasW` / `CanvasH` / `CanvasRotation` | `double?` | Canvas layout coordinates. All nullable. Unit system: 0–`Marina.LayoutWidth` × 0–`Marina.LayoutHeight`. Scaled client-side to screen size. |

### SpotSeasonalRule

| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | E.g. "Summer 2026". |
| `StartDate` / `EndDate` | `DateOnly` | Season date range. |
| `PricePerDay` | `decimal (18,2)` | Overrides spot price for this season. |
| `MinBookingDays` | `int` | |
| `SpotId` | `Guid` FK → `Spot` | |

Unique index on `(SpotId, StartDate, EndDate)`. Overlap with an existing rule is rejected at the service layer.

### Vessel

| Property | Type | Notes |
|---|---|---|
| `Name` | `string` | |
| `Description` | `string?` | |
| `Type` | `VesselType` (single value, stored as int) | |
| `LengthMeters` / `WidthMeters` / `DepthMeters` | `double` | |
| `OwnerId` | `string` FK → `ApplicationUser` | |

### Booking

| Property | Type | Notes |
|---|---|---|
| `SpotId` | `Guid` FK → `Spot` | Restrict delete (see FK rules). |
| `VesselId` | `Guid?` FK → `Vessel` | Nullable. SET NULL when vessel is deleted. Null means "vessel deleted". |
| `BoatOwnerId` | `string` FK → `ApplicationUser` | Restrict delete (see FK rules). |
| `StartDate` / `EndDate` | `DateOnly` | |
| `TotalPrice` | `decimal (18,2)` | |
| `Status` | `BookingStatus` (stored as int) | |

### AdminSettings (single-row config)

| Property | Type | Notes |
|---|---|---|
| `AutoActionType` | `AutoActionType` enum | `AutoApprove` or `AutoReject`. |
| `AutoActionTimeoutHours` | `int` | Default 6. |

Seeded with `Id = "10000000-0000-0000-0000-000000000001"`. Never inserted or deleted by application code — only updated. Accessed via `IAdminSettingsRepository`.

### Invitation

| Property | Type | Notes |
|---|---|---|
| `Email` | `string` | Invitee email. |
| `Token` | `string` | SHA-256 hash of the raw token. Raw token is sent in the email link only. |
| `MarinaId` | `Guid` FK → `Marina` | |
| `ExpiresAt` | `DateTimeOffset` | 48 hours from creation. |
| `IsUsed` | `bool` | Default `false`. |
| `InvitedById` | `string` FK → `ApplicationUser` | |

Indexed on `Token` and `MarinaId`.

---

## Enums

### BookingStatus

| Value | Int |
|---|---|
| `Pending` | 0 |
| `Confirmed` | 1 |
| `Cancelled` | 2 |
| `Completed` | 3 |

### AutoActionType

| Value | Description |
|---|---|
| `AutoApprove` | Hangfire job auto-confirms pending bookings after timeout. |
| `AutoReject` | Hangfire job auto-cancels pending bookings after timeout. |

### VesselType (`[Flags]`)

| Value | Int |
|---|---|
| `None` | 0 |
| `SailBoat` | 1 |
| `MotorBoat` | 2 |
| `Catamaran` | 4 |
| `RIB` | 8 |
| `Yacht` | 16 |
| `Other` | 32 |

Used on `Spot.AllowedVesselTypes` (flags — bitmask of permitted types) and `Vessel.Type` (single value — what the vessel is).

---

## Relationships and FK Delete Rules

| Relationship | Rule | Reason |
|---|---|---|
| `MarinaAdmin.MarinaId` → `Marina` | Cascade | Deleting a marina removes its admin memberships. |
| `MarinaAdmin.UserId` → `ApplicationUser` | Cascade | Deleting a user removes their marina admin memberships. |
| `MarinaAdmin.InvitedById` → `ApplicationUser` | Restrict | Avoid multiple cascade paths to `MarinaAdmin` from `ApplicationUser`. |
| `Booking.VesselId` → `Vessel` | SET NULL | Vessel deletion preserves booking history; null indicates vessel was deleted. |
| `Booking.SpotId` → `Spot` | Restrict | Bookings are audit rows; spot deletion must not cascade-delete booking history. |
| `Booking.BoatOwnerId` → `ApplicationUser` | Restrict | Same reason. Also avoids multiple cascade paths on `Bookings` (three FKs reach `Bookings` from `AspNetUsers` and `Spots`). |
| `Invitation.InvitedById` → `ApplicationUser` | (EF convention — no explicit override) | |

SQL Server rejects migrations when more than one cascade path reaches a table. `Bookings` has three incoming FKs (`BoatOwnerId`, `SpotId`, `VesselId`). `SpotId` and `BoatOwnerId` must be `Restrict` to satisfy SQL Server.

---

## EF Core Notes

- `Spot` has a global query filter `s => s.IsActive`. Use `IgnoreQueryFilters()` in admin queries that need to see inactive spots.
- `DateOnly` is supported natively by EF Core 10 with SQL Server — no value converter needed.
- `VesselType` and `BookingStatus` are stored as `int` via `.HasConversion<int>()`.
- All decimal money columns use `.HasPrecision(18, 2)`.
- FK types pointing at `ApplicationUser` are `string` (matching `IdentityUser.Id`), not `Guid`.
