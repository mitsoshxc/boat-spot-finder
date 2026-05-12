# Development Workflow & Claude Code Skills

## Team Structure

| Role | Who | Responsibility |
|---|---|---|
| Tech lead | User + Claude Code (main session) | Architecture, domain design, task breakdown, review |
| Dev agent | `/dev` skill | Receives spec from tech lead, writes the code, nothing more |
| Doc agent | `/doc` skill | Updates or creates docs after every approved implementation |

The tech lead never writes implementation code or documentation directly. All coding is delegated to `/dev`, verified with `/verify`, then documented with `/doc`.

## Skills

| Skill | When to invoke |
|---|---|
| `/dev <spec>` | Delegate an implementation task to the Dev agent |
| `/verify <spec>` | Verify Dev's output — spec match, conventions, build, tests — returns Approved or Needs rework |
| `/doc <spec>` | Delegate documentation updates to the Doc agent after an approved verification |
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
