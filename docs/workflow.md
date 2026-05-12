# Development Workflow & Claude Code Skills

## Team Structure

| Role | Who | Responsibility |
|---|---|---|
| Tech lead | User + Claude Code (main session) | Architecture, domain design, task breakdown, review |
| Dev agent | `/dev` skill | Receives spec from tech lead, writes the code, nothing more |

The tech lead never writes implementation code directly. All coding is delegated to `/dev` with a precise spec, then verified with `/verify` before accepting the work.

## Skills

| Skill | When to invoke |
|---|---|
| `/dev <spec>` | Delegate an implementation task to the dev agent |
| `/verify <spec>` | Tech lead verifies dev agent output — checks spec match, conventions, build, tests |
| `/review` | After completing a feature — full code review before merging |
| `/security-review` | Before any auth, booking-status, or payment-related change |
| `/simplify` | After an implementation pass when a file feels too long or complex |
| `/init` | If `CLAUDE.md` becomes stale — regenerates it from current codebase state |

## Feature Implementation Order

1. Define entity in `Core/Entities/`
2. Add EF config in `Infrastructure/Data/Configurations/`
3. Declare interface in `Core/Interfaces/`, implement in `Infrastructure/Repositories/`
4. Register in DI (`Program.cs`)
5. Write service logic in `Core/Services/` if business rules are non-trivial
6. Create ViewModel in `Web/Models/`, then controller action, then Razor view

## Agent Conventions

- For broad or cross-cutting changes, spawn an `Explore` agent before editing
- Use `/plan` mode for any new feature to align on approach before writing code
- Run `/security-review` on any action that changes `Booking.Status`
- Run `/simplify` when a controller exceeds ~150 lines

## Branch Strategy

```
main          — production-ready
develop       — integration branch
feature/<name> — one branch per feature
```
