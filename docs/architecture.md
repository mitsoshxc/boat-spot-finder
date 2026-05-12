# Architecture

## Tech Stack

| Layer | Technology |
|---|---|
| Web framework | ASP.NET Core 8 MVC |
| Views | Razor (.cshtml) |
| ORM | Entity Framework Core 8 |
| Auth | ASP.NET Core Identity |
| Database (dev) | SQL Server LocalDB |
| Database (prod) | Azure SQL |
| Migrations | EF Core code-first |
| Testing | xUnit + EF InMemory provider |

## Solution Structure

```
BoatSpotFinder.sln
├── src/
│   ├── BoatSpotFinder.Web/            # ASP.NET Core MVC host
│   │   ├── Controllers/
│   │   ├── Views/
│   │   ├── Models/                    # ViewModels only — never EF entities
│   │   ├── wwwroot/
│   │   └── Program.cs
│   ├── BoatSpotFinder.Core/           # Domain models, interfaces, business logic
│   │   ├── Entities/
│   │   ├── Interfaces/
│   │   └── Services/
│   └── BoatSpotFinder.Infrastructure/ # EF Core, repositories, migrations
│       ├── Data/
│       │   └── Configurations/        # IEntityTypeConfiguration<T> per entity
│       ├── Repositories/
│       └── Migrations/
└── tests/
    └── BoatSpotFinder.Tests/          # xUnit test project
```

## Project References

```
Web → Core, Infrastructure
Infrastructure → Core
Tests → Core
```

## Key Commands

```powershell
# Run the app
dotnet run --project src/BoatSpotFinder.Web

# Add a migration
dotnet ef migrations add <Name> --project src/BoatSpotFinder.Infrastructure --startup-project src/BoatSpotFinder.Web

# Apply migrations
dotnet ef database update --project src/BoatSpotFinder.Infrastructure --startup-project src/BoatSpotFinder.Web

# Run tests
dotnet test
```
