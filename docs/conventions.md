# Conventions

These rules apply to every file in the codebase. The Dev agent enforces them on every brief.

---

## Layering

| Rule | Detail |
|---|---|
| Entities live only in `Core/Entities/` | No EF attributes on entity classes. |
| Repository interfaces live in `Core/Interfaces/` | Concrete implementations in `Infrastructure/Repositories/`. |
| Service interfaces live in `Core/Interfaces/` | Concrete implementations in `Core/Services/`. Search service implementations live in `Infrastructure/Search/`. |
| Shared result/DTO types live in `Core/Common/` or `Core/Models/` | `Core/Common/` for cross-cutting types (e.g. `ServiceResult`); `Core/Models/` for input/transfer objects. |
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
- Navigation properties use collection initializers: `public ICollection<Spot> Spots { get; init; } = [];`
- Required navigation properties use null-forgiving: `public Marina Marina { get; init; } = null!;`
- Property setters — `init` preferred. Use `init` instead of `set` wherever the value is written only at construction or in an object initialiser and never reassigned afterward. `init` prevents accidental post-construction mutation without sacrificing EF Core compatibility (EF Core 6+ supports `init` setters via reflection).
- Property setters — `private set` fallback. When a property is mutated by domain logic after construction (e.g., `Booking.Status`, `Marina.IsActive`, `Spot.IsActive`), use `private set` and expose a dedicated public method on the entity to perform the mutation — for example, `public void Confirm() { Status = BookingStatus.Confirmed; }`. Never expose a plain `public set` on a domain entity property that has business rules governing when it changes.

---

## ViewModels and DTOs

All ViewModels, DTOs, request, response, and event objects are declared as `record`, not `class`. Records provide value equality, immutability, and concise syntax. Example: `public record CreateMarinaRequest(string Name, string Address);`. Applies to everything in `Web/Models/` and any DTO/ETO/request/response type defined in `Core` or `Infrastructure`.

**Exception — mutable collection ViewModels.** Use `class` (not `record`) when a ViewModel needs to hold a mutable collection property that is repopulated on multiple code paths (e.g., a `List<SelectListItem>` dropdown list that is rebuilt after a failed POST). `BookingCreateViewModel` and `BookingListItemViewModel` are the current examples: they are `class` because their `Vessels` / attribution properties are written after construction in controller action bodies, not in a single object initialiser. All other ViewModels remain `record`.

---

## Views

**Every view is designed via `/frontend-design` before `/dev`.** Razor views must use the custom CSS components defined in `wwwroot/css/site.css` — never Bootstrap classes. Custom CSS only, no framework. List views, form views, detail pages: all generated through `/frontend-design` first so markup and styling are coherent across the app. The tech lead embeds the generated markup verbatim in the `/dev` brief.

**Data formatting rules** (apply in all views regardless of layout):

- Column headers are property names with spaces inserted before capitals (`AverageRatingAsBoatOwner` → `Average Rating As Boat Owner`).
- `DateOnly`/`DateTimeOffset` rendered with `ToString("yyyy-MM-dd")`.
- `decimal` price fields rendered with `ToString("F2")`.
- Booleans rendered as `Yes`/`No`.
- Empty list: a single full-width row or block with text `No records yet.`
- POST action buttons use `<form method="post">` with `@Html.AntiForgeryToken()`. Button classes come from the design produced by `/frontend-design` (e.g. `btn btn--sm btn--ghost`).

**Brief overrides take precedence**: when a brief specifies columns, layout, or copy, that overrides any generic component pattern.

---

## Responsive design

1. **Mobile-first principle.** Default CSS targets small viewports. Use `@media (min-width: …)` queries to progressively enhance for larger screens. Never write desktop defaults that collapse via `@media (max-width: …)`. As of 2026-05-18 `wwwroot/css/site.css` contains zero `max-width` media queries — keep it that way.

2. **Breakpoints.** Use only these two breakpoints; do not introduce custom ones without tech-lead sign-off:
   - `@media (min-width: 720px)` — tablet enhancements (side-by-side fields, larger card padding, navigation goes inline).
   - `@media (min-width: 960px)` — desktop enhancements (two-column editor grids, sticky sidebars).

3. **Media query placement and the cascade.** A `@media` block has the same specificity as a non-media rule with the same selector. When two rules tie on specificity, the *later* rule in document order wins. Therefore: a `@media (min-width: 720px) { .foo { … } }` block must appear **after** the `.foo` base rule, otherwise the base rule will override the media query at desktop widths. Two `min-width: 720px` blocks exist in `site.css` for exactly this reason — one early (covers selectors defined above it: header, nav, auth-card), one late (covers `.hero` and `.prose` which are defined later).

4. **Touch targets.** Interactive elements on mobile (buttons, file-input cues, action links in stacked rows) must be at least 44px tall. Add `min-height: 44px` explicitly on `.btn` instances that appear inside stacked mobile action rows.

5. **Form actions on mobile.** Stacked form action rows use `flex-direction: column-reverse` so the primary submit button sits visually above the secondary cancel/back link. At ≥720px the row reverts to `flex-direction: row` with `justify-content: space-between`.

6. **Modal pattern.** Modals are bottom sheets on mobile (`align-items: flex-end`, rounded only on top corners, slide up from bottom via `@keyframes modal-panel-in-mobile`) and centered cards at ≥720px (rounded all corners, slide+scale fade-in via `@keyframes modal-panel-in`).

7. **Grid collapse rule.** Multi-column grids (`.field-row`, `.marina-grid`, `.editor-grid`) default to `grid-template-columns: 1fr` (single column). The min-width queries expand them. Do not invert this — never default to multi-column.

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

### GetByIdAsync vs GetActiveByIdAsync

`ISpotRepository` exposes two single-entity lookup methods with different filter behaviour:

| Method | Filter | Use case |
|---|---|---|
| `GetByIdAsync(id)` | Calls `IgnoreQueryFilters()` — returns active and inactive spots | Owner/admin contexts: editing, layout, ownership verification |
| `GetActiveByIdAsync(id)` | Respects the global `IsActive` filter — returns active spots only | Consumer contexts: `BookingService`, availability checks |

Always use `GetByIdAsync` in PlaceOwner and Admin controllers. Always use `GetActiveByIdAsync` in booking-related services that must not act on inactive spots.

---

## Controllers

- Every controller that requires authentication is decorated with `[Authorize(Roles = "...")]` at the class level.
- No per-action `[ValidateAntiForgeryToken]` — the global `AutoValidateAntiforgeryTokenAttribute` registered in `Program.cs` covers all state-changing verbs.
- No business logic in controllers. Read input, call a service, redirect or return a view.
- Return `NotFound()` / `Forbid()` / `BadRequest()` from controllers, not raw status codes.

### Ownership checks

Every PlaceOwner controller action that operates on a specific marina must call `IMarinaAdminRepository.ExistsAsync(marinaId, userId)` as the **first** thing it does, before loading any entity. Return `Forbid()` immediately if the check fails. For spot actions, a second check follows: load the spot via `ISpotRepository.GetByIdAsync` (which ignores the `IsActive` filter) and verify `spot.MarinaId == marinaId` before proceeding. Return `Forbid()` if the spot does not belong to the marina.

### Controller → service layering

When a service interface exists for a domain operation (e.g. `ISpotSeasonalRuleService`), controllers must call the service — never the underlying repository directly for that operation. The service owns validation and business rules. Calling the repository directly bypasses the overlap check and violates the layering contract.

### JSON vs form content-negotiation

When a POST action must be callable both from a standard HTML form and from JavaScript (e.g. the canvas editor's Add Spot modal and sidebar Delete modal), the controller inspects `Request.Headers.Accept`:

```csharp
var isJson = Request.Headers.Accept.ToString().Contains("application/json");
```

- If `isJson` is true and the request is valid: return `Json(new { id = ..., name = ... })`.
- If `isJson` is true and ModelState is invalid: return `BadRequest(ModelState)`.
- If `isJson` is false: use the standard redirect-or-view pattern.

The booking **dismiss** endpoints (`BookingsController.Dismiss`, `SpotBookingsController.Dismiss`) follow this pattern: the JSON branch returns `Json(new { ok = true })` / `BadRequest(new { error })`, the form branch keeps `TempData` + redirect. `booking-dismiss.js` posts via fetch and removes the card in place.

The JS caller sets `Accept: application/json` and includes the `RequestVerificationToken` header (read from the hidden `__RequestVerificationToken` form input on the page).

The same pattern applies to Delete actions invoked from a JS modal. On success the JSON branch returns `Json(new { ok = true })`; on a business-rule violation (e.g. "Cannot delete a spot that has bookings") it returns `BadRequest(new { error = "..." })`.

### Flag enum aggregation from checkbox lists

`[Flags]` enum properties bound via a checkbox list (e.g. `Spot.AllowedVesselTypes`) are not model-bound as a bitmask by the MVC framework — they arrive as a `List<VesselType>` of individually checked values. Aggregate them manually:

```csharp
var allowedFlags = model.AllowedVesselTypes.Count > 0
    ? model.AllowedVesselTypes.Aggregate(VesselType.None, (acc, v) => acc | v)
    : VesselType.None;
```

Never rely on the default model binder to combine flag values.

### Explicit UpdateAsync

Repositories call `_context.{DbSet}.Update(entity)` explicitly before `SaveChangesAsync()`. EF Core change tracking is not assumed — the entity may have been constructed or detached outside the tracked context. Always call `UpdateAsync` after mutating an entity, even when the entity was loaded from the same `DbContext` instance in the same request.

### File upload validation

When accepting uploaded files, validate both the MIME type (`IFormFile.ContentType`) and the file extension (`Path.GetExtension(file.FileName).ToLowerInvariant()`) — one check alone is insufficient. Allowed types for background images: `image/jpeg`, `image/png`, `image/webp`; allowed extensions: `.jpg`, `.jpeg`, `.png`, `.webp`. Enforce a 5 MB maximum (`file.Length > 5 * 1024 * 1024`). No pixel-dimension check is performed. On re-upload, delete the old file via `IFileStorageService.DeleteAsync` before saving the new one.

---

## Service Return Values

Services return `ServiceResult` (`Core/Common/ServiceResult.cs`) rather than throwing exceptions for validation failures or business rule violations.

```csharp
public record ServiceResult(bool Success, IEnumerable<string> Errors)
{
    public static ServiceResult Ok() => new(true, Array.Empty<string>());
    public static ServiceResult Fail(params string[] errors) => new(false, errors);
}
```

A generic variant `ServiceResult<T>` also exists in the same file for operations that return a value on success:

```csharp
public record ServiceResult<T>(bool Success, IEnumerable<string> Errors, T Value)
{
    public static ServiceResult<T> Ok(T value) => new(true, Array.Empty<string>(), value);
    public static ServiceResult<T> Fail(params string[] errors) => new(false, errors, default!);
}
```

`IBookingService.CreateAsync` returns `Task<ServiceResult<Guid>>` — the `Value` carries the new booking's `Id`. All other service methods return the non-generic `ServiceResult`. New operations that need to return a value on success should use the generic variant; otherwise prefer the non-generic form.

- A successful operation returns `ServiceResult.Ok()` (or `ServiceResult<T>.Ok(value)`).
- A failed operation returns `ServiceResult.Fail(...)` with one or more human-readable error messages.
- Controllers check `result.Success`; on failure they add each error to `ModelState` and return the view.
- Do not throw for expected validation failures. Reserve exceptions for unexpected infrastructure errors.
- All future services must follow this pattern.

---

## Search Indexing

`IMarinaSearchService` (`Core/Interfaces/`) exposes three methods: `IndexAsync(Marina)`, `DeleteAsync(Guid)`, and `SearchAsync(string?)`.

### Sentinel pattern on SearchAsync

`SearchAsync` returns `Task<IEnumerable<Guid>?>`. The nullable return is a deliberate sentinel:

- `null` means "Elasticsearch is not active; apply no filter — return all from SQL."
- A non-null value (including an empty list) means "Elasticsearch responded; restrict results to this set of IDs."

The real implementation (`ElasticsearchMarinaSearchService`) never returns null from `SearchAsync` — on exception it logs and returns an empty enumerable. Only `NullMarinaSearchService` (the no-op stub registered when `Elasticsearch:Uri` is blank) returns null. Consumers must branch on null.

### Sync rules

These rules apply to all `*SearchService` implementations (`IMarinaSearchService`, `IReviewSearchService`, and any future search services).

- ES sync always happens **after** a DB write succeeds. Never sync before the DB write.
- On ES exception, log and continue — the DB is the source of truth. Index drift is recovered by the startup seed on next process start.
- In `MarinasController.Edit POST`, `IndexAsync` is called only when `marina.IsActive == true`. Inactive marinas are not indexed. Admin-side sync is implemented in `AdminController` (Phase 6): `EditMarina POST` calls `IndexAsync` only when the marina is active; `ToggleMarinaActive POST` calls `DeleteAsync` on deactivation and `IndexAsync` on reactivation; `CreateMarina POST` does not index (new marinas are incomplete and have no spots).
- `IndexAsync` and `DeleteAsync` swallow exceptions internally (log + continue), so callers do not need try/catch. `ReviewService.CreateReviewAsync` wraps both `IReviewSearchService.IndexAsync` and the follow-up `IMarinaSearchService.IndexAsync` in a single `try/catch` — a single logged error covers both.

### Implementations

| Class | Location | Behaviour |
|---|---|---|
| `ElasticsearchMarinaSearchService` | `Infrastructure/Search/` | Real ES client. Index name `"marinas"`. Multi-match across name, region, phone, address, description with fuzziness AUTO. Size 10000. `MarinaDocument` now includes `averageRating` and `reviewCount` fields. |
| `NullMarinaSearchService` | `Infrastructure/Search/` | No-op stub. `IndexAsync`/`DeleteAsync` return `Task.CompletedTask`. `SearchAsync` returns `null`. |
| `ElasticsearchReviewSearchService` | `Infrastructure/Search/` | Real ES client. Index name `"reviews"`. Writes a `ReviewDocument` containing `Id`, `BookingId`, `ReviewerRole` (string), `MarinaId` (for BoatOwner reviews), `BoatOwnerId` (for PlaceOwner reviews), `Score`, `Comment`, `CreatedAt`. |
| `NullReviewSearchService` | `Infrastructure/Search/` | No-op stub. `IndexAsync` returns `Task.CompletedTask`. |

DI wiring is controlled by a single config guard in `Program.cs`: if `Elasticsearch:Uri` is blank, both `NullMarinaSearchService` and `NullReviewSearchService` are registered; otherwise `ElasticsearchClient` (singleton) + both real implementations (scoped) are registered. All future `*SearchService` pairs follow the same pattern.

---

## Routing

- Admin management controllers use `[Route("admin/...")]`.
- PlaceOwner management controllers use `[Route("placeowner/...")]`.
- Public and BoatOwner controllers have no route prefix.
- Hangfire dashboard stays at `/hangfire` (Admin-only auth, no prefix).
- Layout data endpoint (`GET /browse/marina/{id}/layout-data`) lives on `BrowseController` (no prefix) so it is served by the public pod.

Enables Nginx Ingress to route `/admin/` and `/placeowner/` path prefixes to a dedicated management pod independently of public traffic.

---

## CSRF

`AutoValidateAntiforgeryTokenAttribute` is registered globally in `Program.cs`. It validates on POST, PUT, PATCH, DELETE and skips GET, HEAD, OPTIONS, TRACE. It reads the token from either the form field or the `RequestVerificationToken` request header. No per-action attribute needed.

---

## Security — Tokens

Raw invite tokens are never stored in the database. Only the SHA-256 hash is stored (`Invitation.Token`). `Core/Helpers/TokenHasher.Hash(rawToken)` performs the hash. The raw token is sent in the email link only. Token generation (a `Guid.NewGuid().ToString("N")` raw value) happens in `AdminController.InviteAdmin POST`; token consumption (hash lookup, expiry check, `MarkUsedAsync`) happens in `AccountController.InviteRegister`.

---

## Seed Data

- Seed rows use fixed, deterministic GUIDs so migrations are idempotent.
- The admin password hash in `AppDbContext` seed is a hardcoded PBKDF2 literal. Do not call `PasswordHasher` inside `OnModelCreating` — the random salt produces a new hash on every `dotnet ef migrations add`, creating spurious update migrations.
- `HasData` requires explicit values for `CreatedAt` and `UpdatedAt` even when the column has `HasDefaultValueSql("GETUTCDATE()")` — EF Core does not apply DB defaults to seeded rows.

---

## Comments

Do not add comments unless the reason behind the code is non-obvious. "What" comments are noise. "Why" comments are acceptable when the behavior would otherwise be misread as a bug (e.g. explaining why `Restrict` is used instead of `Cascade` on booking FKs).

---

## Always-Braces for Conditionals

Every `if`, `else if`, and `else` body is wrapped in `{ }`, even when the body is a single statement.

Rationale: eliminates the dangling-else / missed-statement class of bug and keeps diffs clean when a body later grows to multiple statements.

| Language | Brace style | Rule |
|---|---|---|
| C# (`.cs`) | Allman — `{` and `}` each on their own line, indented to match the `if`/`else` keyword | Applies to all `.cs` files |
| JavaScript (`.js` and inline `<script>` in `.cshtml`) | K&R — `{` at the end of the `if (...)` line, `}` on its own line aligned with the `if` | Applies to all `.js` files and inline script blocks |

Scope: `.cs`, `.js`, and inline `<script>` in `.cshtml`. Razor `@if (...) { ... }` blocks are already braced multi-line and are unaffected by this rule. `for` / `while` / `foreach` bodies follow the same intent informally but are not the enforcement target of this rule.

C# Allman example:

```csharp
if (vessel is null)
{
    return ServiceResult.Fail("Vessel not found");
}
```

JavaScript K&R example:

```js
if (e.key === 'Escape') {
    e.preventDefault();
    close();
}
```

---

## JavaScript

Use `const` by default for every variable declaration. Use `let` only when the binding is reassigned. **Never use `var`** in any `.js` file or in inline `<script>` blocks inside `.cshtml`.

- `const` permits mutation of the bound value (array `.push`, object property assignment, DOM/Konva method calls) — it only forbids rebinding the identifier. Prefer `const` whenever the binding itself does not change.
- `let` is correct for loop counters (`for (let i = 0; ...)`), accumulators, flags toggled inside a branch, and variables declared without an initializer and assigned in branches.
- This rule is JS-only. `@{ var x = ... }` blocks in `.cshtml` are C# Razor and remain unaffected.

Enforced on every JS Dev brief.

---

## Canvas / Visual Layout

| Concern | Rule |
|---|---|
| Canvas library | Konva.js via CDN `https://unpkg.com/konva@9/konva.min.js`. Included only on views that use it (`Layout.cshtml`, `Browse/Marina.cshtml`, `Admin/MarinaLayout.cshtml`). |
| Coordinate system | Logical units 0–`LayoutWidth` × 0–`LayoutHeight` (default 1200×800). JS scales to rendered size client-side. Never store pixel values tied to screen resolution. |
| Canvas positions nullable | `CanvasX/Y/W/H/Rotation` are all nullable. A spot may exist without being placed. Unplaced spots appear in a sidebar list, not on the canvas. |
| Background image | Stored under `wwwroot/uploads/marina-backgrounds/{id}.{ext}`. Validate type (jpg/png/webp) and max size (5 MB) in the controller. Serve as static files. Removable via `MarinasController.ClearBackground` (see "Clear background image" in Editor interactions). |
| Spot status derivation | Free = active + no overlapping Confirmed/Pending booking. Booked = has overlap. Unavailable = `IsActive=false`. Computed in the repository/service, never stored. |
| Shared JS | `marina-viewer.js` is used by Browse, Admin, and PlaceOwner read-only views. `marina-editor.js` is PlaceOwner-only. Keep them separate files. |
| Save positions | The editor POSTs a JSON array to `SavePositions` in bulk on user action — not on every drag event. |

### Editor interactions

| Behavior | Rule |
|---|---|
| Overlap and bounds (client-side only) | Spots cannot overlap each other or extend outside the layout's logical bounds. Enforced in `marina-editor.js` using axis-aligned bounding boxes (AABB) via `node.getClientRect({ relativeTo: layer, skipStroke: true })`. During drag and resize, spots snap to neighbor edges and canvas edges when within a threshold. On `dragend` / `transformend`, if the spot's AABB overlaps any other spot or extends out of bounds, position/size/rotation revert to the pre-drag / pre-transform snapshot. `SpotsController.SavePositions` does **not** enforce this server-side — the contract is client-side only. Snap targets that match the spot's starting edge position (captured on `dragstart` / `transformstart` as `_preDragAABB` / `_preTransformAABB`) are excluded for the duration of the gesture so a spot that begins touching a neighbor can be released freely; entering a new snap zone later in the same gesture still snaps as usual. |
| New-spot placement | When `addSpotToCanvas` runs the unplaced branch (brand-new spots from the Add Spot modal, or spots loaded with null `CanvasX/Y/W/H`), it scans the canvas in a grid step for the first 80×50 slot that does not overlap any existing spot. If a free slot is found the spot lands there automatically; if the canvas is full a cascade offset formula is used as a fallback so the spot is still visible (the user drags it to its final position manually). |
| Rotation | `rotateEnabled: false` on the Konva Transformer — the rotation handle is hidden and users cannot change rotation. Stored `Spot.CanvasRotation` values still round-trip through `SavePositions`, so previously-rotated spots render at their saved angle. |
| Transformer anchors | The Transformer exposes all eight anchors — 4 corners (`top-left`, `top-right`, `bottom-left`, `bottom-right`) and 4 edge midpoints (`top-center`, `bottom-center`, `middle-left`, `middle-right`) — so users can resize one axis at a time via edge anchors or both axes at once via corner anchors. |
| Spot label color | `Konva.Text` labels are always rendered with white fill regardless of placement state, for legibility against the spot's slate fill. |
| Fullscreen mode | The layout editor has a CSS-overlay fullscreen mode toggled by the `.workspace--fullscreen` modifier on `.workspace--editor`. When active, CSS hides the workspace head, plate caption, upload row, and nav links; the canvas shell fills viewport height; the sidebar gets internal scroll. On enter, JS DOM-moves the three essential buttons (Add spot, Exit fullscreen, Save layout) into `.spot-sidebar__toolbar` inside the sidebar; on exit they move back to `.toolbar`. Toggled by button or ESC key. Add/Delete modal `z-index` sits above the fullscreen overlay so modals function inside fullscreen. |
| Clear background image | The marina layout editor exposes a "Clear background" row (rendered only when `Marina.BackgroundImagePath` is set) that opens a confirmation modal and, on confirm, POSTs to `MarinasController.ClearBackground` (`POST /placeowner/marinas/{id}/background/clear`). The action runs the standard PlaceOwner ownership check, calls `IFileStorageService.DeleteAsync` to remove the file from `wwwroot/uploads/marina-backgrounds/`, sets `BackgroundImagePath` to null via the entity's `ClearBackgroundImage()` method, persists via `UpdateAsync`, and is idempotent — clearing when no background exists is a no-op success. Content-negotiates per § Controllers § JSON vs form content-negotiation. The JS confirm flow reloads the page on success rather than mutating the canvas in place. |

---

## Audit Logging

When a controller action adds a new audited operation:

- Call `_auditLogger.Log(...)` on the **success path only**, after the DB write or service call has completed and succeeded.
- Do **not** wrap the call in `try/catch` — NLog handles internal errors and never propagates them to callers.
- Use the caller's `User.FindFirstValue(ClaimTypes.NameIdentifier)` for `userId` and `User.Identity!.Name` for `userEmail`. For `Logout`, capture these before calling `SignOutAsync` because the claims context is cleared by sign-out.
- Pass `details: null` unless carrying business context that is not derivable from the other fields (e.g., `previousStatus`, `spotName`, `email`). Keep anonymous objects flat.
- See [`docs/features/audit-logging.md`](features/audit-logging.md) for the full audited-action catalog and log schema.

---

## Tooling Triggers

- `/simplify` is run on `BookingsController`, `SpotBookingsController`, and `AdminController` whenever any of them exceeds 150 lines.

---

## Build Verification

Every Dev agent brief must end with a successful `dotnet build` before the agent reports done.
