# Boat Spot Finder

Web application for booking marina/harbour boat spots. Three roles: **BoatOwner**, **PlaceOwner**, **Admin**.

Stack: ASP.NET Core 10 MVC · Entity Framework Core 10 · SQL Server · ASP.NET Core Identity

## Roles in this project

**Tech lead**: The user + Claude Code in this session. Responsible for architecture decisions, domain design, task breakdown, and code review. All implementation tasks are delegated downward.

**Dev agent**: Invoked via `/dev`. Receives a precise implementation spec from the tech lead and writes the code — no architecture decisions, no alternatives, just faithful implementation following `docs/conventions.md`.

## Docs

- [Architecture & solution structure](docs/architecture.md)
- [Domain model & roles](docs/domain-model.md)
- [Coding conventions](docs/conventions.md)
- [Dev workflow & Claude Code skills](docs/workflow.md)
