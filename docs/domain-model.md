# Domain Model

## Roles

| Role | How assigned | Access |
|---|---|---|
| `BoatOwner` | Self-registration default | Browse spots, manage vessels, create/cancel own bookings |
| `PlaceOwner` | Registration + Admin approval | Manage own marina's spots, approve/reject/view bookings |
| `Admin` | Seeded in DB | Full access to all spots, bookings, users |

## Entities

```
ApplicationUser  (extends IdentityUser)
  ├── BoatOwner  — 1:N → Vessel
  │              — 1:N → Booking
  └── PlaceOwner — 1:N → Marina

Marina
  ├── Id, Name, Description, Address
  ├── Latitude, Longitude (double)
  ├── PlaceOwnerId (FK)
  └── Spots (1:N → Spot)

Spot
  ├── Id, Name, Description
  ├── LengthMeters, WidthMeters, DepthMeters (double)
  ├── PricePerDay (decimal 18,2)
  ├── IsActive (bool)
  ├── MarinaId (FK)
  └── Bookings (1:N → Booking)

Vessel
  ├── Id, Name, RegistrationNumber
  ├── LengthMeters, WidthMeters, DraftMeters (double)
  ├── BoatOwnerId (FK)
  └── Bookings (1:N → Booking)

Booking
  ├── Id
  ├── StartDate, EndDate (DateOnly, UTC)
  ├── TotalPrice (decimal 18,2)
  ├── Status (enum: Pending | Confirmed | Cancelled | Completed)
  ├── SpotId (FK)
  ├── VesselId (FK)
  └── BoatOwnerId (FK)
```

## Booking Status Flow

```
Pending → Confirmed (PlaceOwner approves)
        → Cancelled (BoatOwner or PlaceOwner cancels)
Confirmed → Completed (system, on EndDate passing)
          → Cancelled
```
