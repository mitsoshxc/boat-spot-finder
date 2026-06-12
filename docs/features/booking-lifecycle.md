# Booking Lifecycle

A booking represents a BoatOwner's reservation of a marina Spot for a date range. Bookings are created in a `Pending` state and must be either confirmed or rejected by the marina's PlaceOwner before the start date. Two Hangfire recurring jobs handle timed-out decisions and post-stay completion automatically.

The booking domain spans `Core/Services/BookingService.cs`, `Core/Interfaces/IBookingService.cs`, `Infrastructure/Repositories/BookingRepository.cs`, `Web/Controllers/BookingsController.cs`, and `Web/Controllers/SpotBookingsController.cs`.

---

## How It Works

```
BoatOwner submits Create form
         |
         v
  BookingService.CreateAsync
   - validate spot, vessel, overlap
   - resolve pricing
   - persist Booking (Status = Pending)
   - email all MarinaAdmins
         |
         v
  PlaceOwner sees Incoming list (SpotBookingsController)
         |
    +---------+-----------+
    |                     |
  Confirm              Reject
    |                     |
  Confirmed            Cancelled
    |                     |
    v
  booking-complete-overdue job (nightly 02:00 UTC)
   - EndDate < today → Completed
```

If the PlaceOwner does not act within `AdminSettings.AutoActionTimeoutHours`, the `booking-auto-action` job (every 5 minutes) fires `AutoActionAsync`, which calls either `ConfirmCoreAsync` or `RejectCoreAsync` depending on `AdminSettings.AutoActionType`.

---

## Status Transitions

| From | To | Triggered by |
|---|---|---|
| (new) | `Pending` | `BookingService.CreateAsync` |
| `Pending` | `Confirmed` | `BookingService.ConfirmAsync` or `AutoActionAsync` (AutoApprove) |
| `Pending` | `Cancelled` | `BookingService.RejectAsync` or `AutoActionAsync` (AutoReject) or `CancelAsync` |
| `Confirmed` | `Cancelled` | `BookingService.CancelAsync` |
| `Confirmed` | `Completed` | `BookingService.CompleteOverdueAsync` (Hangfire job) |

`Completed` and `Cancelled` are terminal states — no further transitions are permitted.

---

## Pricing Resolution

`BookingService.ResolvePricingAsync` (private helper in `Core/Services/BookingService.cs`) is called during both `CreateAsync` and `PreviewPriceAsync`. The cascade is:

1. Load `SpotSeasonalRule` records for the spot via `ISpotSeasonalRuleRepository.GetBySpotIdAsync`.
2. Find the first matching rule: `r.StartDate <= booking.StartDate && booking.StartDate <= r.EndDate`. Only `StartDate` is checked against the rule range — not `EndDate` or intermediate days. Ordered by `r.StartDate` for deterministic selection when no overlapping rules exist (guaranteed by the seasonal-rule overlap constraint from Phase 3).
3. If a rule matches: use `rule.PricePerDay` and `rule.MinBookingDays`.
4. If no rule matches: use `spot.PricePerDay` (if set) or fall back to `marina.DefaultPricePerDay`; use `spot.DefaultMinBookingDays`.

`TotalPrice = resolvedPricePerDay × (EndDate.DayNumber − StartDate.DayNumber)`.

`MinBookingDays` is enforced after pricing: if `(EndDate − StartDate).Days < resolvedMinBookingDays`, `CreateAsync` returns `ServiceResult.Fail(...)`.

---

## Overlap Check

`IBookingRepository.IsSpotAvailableAsync(spotId, start, end, excludeBookingId)` in `Infrastructure/Repositories/BookingRepository.cs` returns `true` if no non-cancelled booking for the spot satisfies `start < booking.EndDate && end > booking.StartDate`. `excludeBookingId` is used in edit scenarios to avoid self-conflict; it is always `null` in `CreateAsync`.

Adjacent bookings (where one ends on the day another starts) are permitted — `<`/`>` not `<=`/`>=`.

---

## CancelAsync Role Resolution

`BookingService.CancelAsync` (`Core/Services/BookingService.cs`) resolves the caller's role internally. No ownership check runs in the controller — `SpotBookingsController.Cancel` and `BookingsController.Cancel` both pass `currentUserId` to the service unchanged.

Resolution order:

1. `booking.BoatOwnerId == cancellerUserId` → **BoatOwner** (StartDate guard applies: reject if `booking.StartDate <= today`).
2. `IMarinaAdminRepository.ExistsAsync(booking.Spot.MarinaId, cancellerUserId)` → **PlaceOwner** (same StartDate guard).
3. `UserManager.IsInRoleAsync(user, "Admin")` → **Admin** (StartDate guard is skipped; Admin can cancel any Pending or Confirmed booking but not Completed or already-Cancelled).
4. None of the above → `ServiceResult.Fail("Forbidden")`.

All statuses other than `Pending` and `Confirmed` are rejected for every role before the StartDate check.

---

## ConfirmAsync / RejectAsync

`ConfirmAsync` and `RejectAsync` enforce a two-step pattern:

1. `IMarinaAdminRepository.ExistsAsync(booking.Spot.MarinaId, performerUserId)` — return `Forbidden` if the caller is not a marina admin for that spot's marina.
2. Delegate to private `ConfirmCoreAsync(booking)` or `RejectCoreAsync(booking)`.

The private methods have no ownership check and are also called directly by `AutoActionAsync` (the Hangfire job has no performer userId).

- `ConfirmCoreAsync` — transitions to `Confirmed`, emails BoatOwner with a link to `/bookings`.
- `RejectCoreAsync` — transitions to `Cancelled`, emails BoatOwner without a reason.

---

## DismissAsync / DismissByMarinaAsync

These methods remove a terminal booking from a user's list view without deleting the row.

**`BookingService.DismissAsync(bookingId, userId)`** — BoatOwner side.

1. Load booking by id; return `Fail("Booking not found")` if missing.
2. `booking.BoatOwnerId != userId` → return `Fail("Forbidden")`.
3. Booking must be `Cancelled`, `Completed`, or have `EndDate < today`; otherwise return `Fail("Only past or cancelled bookings can be dismissed")`.
4. Call `booking.DismissByOwner()` (sets `DismissedByOwner = true`), persist via `UpdateAsync`.

`BookingsController.MyBookings` filters out bookings where `DismissedByOwner == true`; PlaceOwner and Admin views are unaffected.

**`BookingService.DismissByMarinaAsync(bookingId, userId)`** — PlaceOwner side.

1. Load booking by id; return `Fail("Booking not found")` if missing.
2. `IMarinaAdminRepository.ExistsAsync(booking.Spot.MarinaId, userId)` → return `Fail("Forbidden")` if the caller is not a marina admin for the booking's marina.
3. Same terminal / elapsed guard as above.
4. Call `booking.DismissByMarina()` (sets `DismissedByMarina = true`), persist via `UpdateAsync`.

`SpotBookingsController.Incoming` filters out bookings where `DismissedByMarina == true`; BoatOwner and Admin views are unaffected. Because authorization is marina-wide (`ExistsAsync`), any admin of the marina can dismiss — there is no per-admin filter.

The booking row is **never deleted** by either method — the audit trail and Admin/cross-role visibility are preserved.

Both dismiss endpoints (`POST /bookings/{id}/dismiss`, `POST /placeowner/spot-bookings/{id}/dismiss`) content-negotiate (see `conventions.md` § JSON vs form content-negotiation): an `Accept: application/json` request returns `Json(new { ok = true })` on success / `BadRequest(new { error })` on failure; a plain form post falls back to `TempData` + redirect. `booking-dismiss.js` (loaded on `MyBookings.cshtml` and `Incoming.cshtml`) posts via `fetch` and removes the card in place — decrementing the section count, dropping an emptied section, and reloading to the empty state when the last card goes.

---

## Hangfire Recurring Jobs

Both jobs are registered in `Web/Program.cs` immediately after `UseHangfireDashboard`, using the typed generic overload so Hangfire resolves `IBookingService` via DI at execution time.

| Job id | Cron | Calls | Effect |
|---|---|---|---|
| `booking-auto-action` | `*/5 * * * *` | `IBookingService.AutoActionAsync()` | Processes Pending bookings where `CreatedAt + AdminSettings.AutoActionTimeoutHours < UtcNow`. Calls `ConfirmCoreAsync` or `RejectCoreAsync` based on `AdminSettings.AutoActionType`. |
| `booking-complete-overdue` | `0 2 * * *` | `IBookingService.CompleteOverdueAsync()` | Transitions Confirmed bookings to Completed where `EndDate < DateOnly.FromDateTime(DateTime.UtcNow)`. After each transition, fires review-invite emails to the BoatOwner and all marina admins (see § Review-Invite Emails below). |

---

## Review-Invite Emails

After `booking.Complete()` is persisted inside `CompleteOverdueAsync`, two email fan-outs run for each completed booking:

**BoatOwner (single recipient).** `UserManager.FindByIdAsync(booking.BoatOwnerId)` resolves the user. If the user exists and has a non-null email, one email is sent with:
- Subject: `"How was your stay at {marina.Name}?"`
- Review link: `{AppSettings.BaseUrl}/reviews/create?bookingId={booking.Id}`
- Deadline note: `booking.EndDate.AddDays(14)` formatted `yyyy-MM-dd`.

**PlaceOwner fan-out (all marina admins).** `IMarinaAdminRepository.GetByMarinaIdAsync(booking.Spot.MarinaId)` returns all admin records. For each, `UserManager.FindByIdAsync(admin.UserId)` resolves the user. One email per admin with:
- Subject: `"Rate {ownerName} — stay complete at {spot.Name}"` (`ownerName` = `UserName ?? Email ?? "your guest"`).
- Review link: `{AppSettings.BaseUrl}/placeowner/reviews/create?bookingId={booking.Id}`
- Deadline note: same 14-day window.

**First-to-submit race.** All marina admins for the booking's marina receive the invite. Only one PlaceOwner review is permitted per booking (`(BookingId, ReviewerRole)` unique index). The first admin to submit wins. Later admins who click their link receive `"You have already reviewed this booking"` from `CanReviewAsync` and the `Create GET` action returns 404. This is a benign UX outcome — no data is corrupted.

Each send is individually wrapped in `try/catch(Exception ex)` + `ILogger.LogError`. Failure of one admin's email does not block the others. See § Email Failure Policy for the general contract.

---

## Email Failure Policy

Every `IEmailSender.SendAsync` call in `BookingService` is wrapped in `try/catch(Exception ex)`. On failure, `ILogger<BookingService>` logs the error and execution continues. The booking state change (Confirmed, Cancelled, Completed) has already been persisted before the email fires and is not rolled back when an email fails.

Emails fired per event:

| Event | Recipients |
|---|---|
| `CreateAsync` (new booking) | All MarinaAdmins for the spot's marina |
| `CancelAsync` by BoatOwner | All MarinaAdmins |
| `CancelAsync` by PlaceOwner | BoatOwner |
| `CancelAsync` by Admin | BoatOwner + all MarinaAdmins |
| `ConfirmCoreAsync` | BoatOwner |
| `RejectCoreAsync` | BoatOwner |
| `CompleteOverdueAsync` (per booking) | BoatOwner (review invite) + all MarinaAdmins (review invite, one email each) |

---

## Key Files

| File | Purpose |
|---|---|
| `src/BoatSpotFinder.Core/Interfaces/IBookingService.cs` | Service contract — 9 public method signatures |
| `src/BoatSpotFinder.Core/Services/BookingService.cs` | Full booking lifecycle implementation |
| `src/BoatSpotFinder.Core/Interfaces/IBookingRepository.cs` | Repository contract — 8 methods |
| `src/BoatSpotFinder.Infrastructure/Repositories/BookingRepository.cs` | EF Core repository; `GetByBoatOwnerIdAsync` and `GetByMarinaOwnerIdAsync` eager-load `Spot.Marina` and `Vessel` |
| `src/BoatSpotFinder.Core/Models/PricePreview.cs` | `record PricePreview(decimal PricePerDay, int MinBookingDays, decimal TotalPrice)` — return type of `PreviewPriceAsync` |
| `src/BoatSpotFinder.Web/Controllers/BookingsController.cs` | BoatOwner controller — MyBookings, Create, PreviewPrice, Cancel, Dismiss |
| `src/BoatSpotFinder.Web/Controllers/SpotBookingsController.cs` | PlaceOwner controller — Incoming, Confirm, Reject, Cancel, Dismiss |
| `src/BoatSpotFinder.Web/Models/BookingCreateViewModel.cs` | Create form model — implements `IValidatableObject` (departure-after-arrival check); includes nullable `Preview` and `Vessels` dropdown |
| `src/BoatSpotFinder.Web/Models/BookingListItemViewModel.cs` | Shared list item shape used in both MyBookings and Incoming views |
| `src/BoatSpotFinder.Web/Models/MyBookingsViewModel.cs` | Three-section view model for MyBookings — `Pending`, `Confirmed`, `Past` (each `List<BookingListItemViewModel>`) |
| `src/BoatSpotFinder.Web/Models/IncomingBookingsViewModel.cs` | Three-section view model for Incoming — `Pending`, `Confirmed`, `Past` |
| `src/BoatSpotFinder.Web/Views/Bookings/` | BoatOwner views — `MyBookings.cshtml`, `Create.cshtml` |
| `src/BoatSpotFinder.Web/Views/SpotBookings/` | PlaceOwner view — `Incoming.cshtml` (includes inline JS deadline ticker using `const`/`let`) |

---

## Business Rules

- `BookingService.CreateAsync` returns `ServiceResult<Guid>` — the `Value` carries the new booking's `Id`. All other service methods return the non-generic `ServiceResult`.
- A spot must be active (`IsActive == true`) to accept bookings. `BookingService.CreateAsync` uses `ISpotRepository.GetActiveByIdAsync`, which respects the global EF query filter and returns `null` for inactive spots.
- `Spot.AllowedVesselTypes != None` restricts bookings by vessel type bitmask: `(spot.AllowedVesselTypes & vessel.Type) == 0` is rejected.
- Vessel dimensions must fit within spot dimensions (`LengthMeters`, `WidthMeters`, `DepthMeters`). Exact fit is allowed.
- `EndDate` must be strictly after `StartDate` (`EndDate > StartDate`). This is enforced in two places: `BookingCreateViewModel.Validate` (`IValidatableObject`) — server-side before the controller calls the service — and `BookingService.CreateAsync` as a domain guard immediately before the availability check. Both emit the same message: `"Departure must be after the arrival date."`. The `Create.cshtml` form also enforces this client-side via vanilla JS, but the server-side checks are authoritative.
- Overlap check uses strict inequality (`<`/`>`), permitting back-to-back bookings where one ends on the day the next begins.
- Only `booking.StartDate` (the first day of the stay) is matched against a `SpotSeasonalRule`'s date range — not subsequent days.
- BoatOwner and PlaceOwner cannot cancel once the booking's `StartDate` is today or in the past. Admin has no StartDate restriction.
- Only `Pending` and `Confirmed` bookings can be cancelled. `Completed` and already-`Cancelled` bookings are terminal.

---

## Quick Reference

| Task | How |
|---|---|
| BoatOwner creates booking | `GET /bookings/create?spotId=...` → `POST /bookings/create` |
| BoatOwner views their bookings | `GET /bookings` |
| BoatOwner cancels | `POST /bookings/{id}/cancel` |
| BoatOwner dismisses past/cancelled booking | `POST /bookings/{id}/dismiss` |
| Live price preview | `GET /bookings/preview-price?spotId&vesselId&startDate&endDate` → JSON `{ pricePerDay, minBookingDays, totalPrice }` |
| PlaceOwner views incoming | `GET /placeowner/spot-bookings` |
| PlaceOwner confirms | `POST /placeowner/spot-bookings/{id}/confirm` |
| PlaceOwner rejects | `POST /placeowner/spot-bookings/{id}/reject` |
| PlaceOwner cancels | `POST /placeowner/spot-bookings/{id}/cancel` |
| PlaceOwner dismisses past/cancelled booking | `POST /placeowner/spot-bookings/{id}/dismiss` |
| Auto-action timeout (hours) | `AdminSettings.AutoActionTimeoutHours` (default 6) |
| Auto-action behaviour | `AdminSettings.AutoActionType` — `AutoApprove` or `AutoReject` |
