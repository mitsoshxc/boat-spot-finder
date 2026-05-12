# Coding Conventions

## General

- Controllers use attribute routing + `[Authorize(Roles = "RoleName")]`
- Never pass EF entities directly to views — always use a ViewModel
- ViewModels live in `Web/Models/` named `<Feature>ViewModel`
- Repository pattern: interfaces in `Core/Interfaces/`, implementations in `Infrastructure/Repositories/`
- Wire all services and repositories via DI in `Program.cs`

## Data

- All money fields: `decimal(18,2)`
- Dates stored as UTC `DateOnly`; convert to local time for display via JavaScript
- Soft-delete pattern for Spots (`IsActive` flag) — never hard-delete a spot with past bookings
- EF entity configuration lives in `Infrastructure/Data/Configurations/` as `IEntityTypeConfiguration<T>`
- Migrations run automatically on startup in `Development` environment only

## Naming

| Artifact | Convention | Example |
|---|---|---|
| Controller | `<Feature>Controller` | `BookingsController` |
| ViewModel | `<Feature>ViewModel` | `BookingCreateViewModel` |
| Repository interface | `I<Entity>Repository` | `IBookingRepository` |
| Repository impl | `<Entity>Repository` | `BookingRepository` |
| EF config class | `<Entity>Configuration` | `BookingConfiguration` |

## Layering Rules

- `Core` has zero infrastructure dependencies (no EF, no HTTP)
- `Infrastructure` depends on `Core`, never on `Web`
- `Web` orchestrates only — business logic belongs in `Core/Services/`
