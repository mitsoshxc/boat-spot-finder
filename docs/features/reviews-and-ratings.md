# Reviews and Ratings

After a booking reaches `Completed` status, both parties — the BoatOwner and the marina's PlaceOwner(s) — have a 14-day window to submit a review. Reviews are bidirectional: a BoatOwner reviews the marina they stayed at; a PlaceOwner reviews the BoatOwner guest. Exactly one review per direction per booking is permitted. Rating averages are denormalized onto `Marina` and `ApplicationUser` for fast read access and are synced to Elasticsearch after every new review.

The feature spans `Core/Entities/`, `Core/Interfaces/`, `Core/Services/`, `Infrastructure/Repositories/`, `Infrastructure/Search/`, and `Web/Controllers/`, with a shared Razor view at `Views/Reviews/Create.cshtml`.

---

## How It Works

```
booking-complete-overdue job (nightly)
  → booking.Complete() persisted
  → review-invite email to BoatOwner  (/reviews/create?bookingId={id})
  → review-invite email to each marina admin  (/placeowner/reviews/create?bookingId={id})
         |
         v
  User clicks link → Create GET
   - CanReviewAsync: Status == Completed? window open? role resolved? not already reviewed?
   - 404 if not eligible
   - populate ReviewCreateViewModel with booking summary
         |
         v
  User submits form → Create POST
   - CanReviewAsync again (authoritative gate)
   - Review row inserted
   - Rating recomputed (full average across all reviews for that marina or BoatOwner)
   - Marina.AverageRating / Marina.ReviewCount  OR
     ApplicationUser.AverageRatingAsBoatOwner / ReviewCountAsBoatOwner updated
         |
         v
  ES sync (in single try/catch)
   - IReviewSearchService.IndexAsync(review)  → "reviews" index
   - if BoatOwner review: IMarinaSearchService.IndexAsync(marina)  → "marinas" index
         |
         v
  Redirect to MyBookings (BoatOwner) or Incoming (PlaceOwner)
```

1. `CompleteOverdueAsync` (called by the nightly Hangfire job) marks each overdue booking `Completed` and immediately fires two email fan-outs per booking: one to the BoatOwner and one to each marina admin. Each send is in its own `try/catch`; a failed send does not block the others.
2. The user opens the link and lands on `Create GET` in the appropriate controller. `CanReviewAsync` runs the full eligibility check. If any condition fails the action returns 404.
3. On `POST`, `CanReviewAsync` runs again as the authoritative gate. On success, the `Review` row is inserted, the rating average is recomputed from scratch using all existing reviews, and the denormalized fields on `Marina` or `ApplicationUser` are updated.
4. After DB writes succeed, both the review document and (for BoatOwner reviews) the updated marina document are pushed to Elasticsearch. ES failures are logged but do not roll back DB writes.
5. The completed booking card in `MyBookings` and `Incoming` shows the submitted score instead of the "Leave a review" CTA.

---

## Key Files

| File | Purpose |
|---|---|
| `src/BoatSpotFinder.Core/Entities/Review.cs` | `Review` entity — `BookingId`, `ReviewerId`, `ReviewerRole`, `Score`, `Comment`, navigation properties |
| `src/BoatSpotFinder.Core/Entities/ReviewerRole.cs` | `ReviewerRole` enum — `BoatOwner`, `PlaceOwner` |
| `src/BoatSpotFinder.Core/Models/ReviewEligibility.cs` | `record ReviewEligibility(bool IsEligible, ReviewerRole? Role, string? ErrorReason)` |
| `src/BoatSpotFinder.Core/Interfaces/IReviewRepository.cs` | Repository contract — `GetByBookingIdAsync`, `GetRecentByMarinaIdAsync`, `GetAllByMarinaIdAsync`, `GetAllByBoatOwnerIdAsync`, `AddAsync`, `ExistsAsync` |
| `src/BoatSpotFinder.Core/Interfaces/IReviewSearchService.cs` | Search service contract — `IndexAsync(Review)` |
| `src/BoatSpotFinder.Core/Interfaces/IReviewService.cs` | Service contract — `CanReviewAsync`, `CreateReviewAsync`, `GetForBookingAsync` |
| `src/BoatSpotFinder.Core/Services/ReviewService.cs` | Service implementation — eligibility, rating recompute, ES sync |
| `src/BoatSpotFinder.Infrastructure/Data/Configurations/ReviewConfiguration.cs` | EF config — unique index on `(BookingId, ReviewerRole)`, check constraint, FK delete rules |
| `src/BoatSpotFinder.Infrastructure/Migrations/20260529110213_AddReviewsAndRatings.cs` | Adds `Reviews` table, `Marina.AverageRating/ReviewCount`, `AspNetUsers.AverageRatingAsBoatOwner/ReviewCountAsBoatOwner` |
| `src/BoatSpotFinder.Infrastructure/Repositories/ReviewRepository.cs` | EF Core repository |
| `src/BoatSpotFinder.Infrastructure/Search/ElasticsearchReviewSearchService.cs` | Real ES implementation — writes to `"reviews"` index |
| `src/BoatSpotFinder.Infrastructure/Search/NullReviewSearchService.cs` | No-op stub — registered when `Elasticsearch:Uri` is blank |
| `src/BoatSpotFinder.Web/Controllers/BoatOwnerReviewsController.cs` | `[Authorize(Roles="BoatOwner")]`, route `/reviews`, actions: Create GET/POST |
| `src/BoatSpotFinder.Web/Controllers/PlaceOwnerReviewsController.cs` | `[Authorize(Roles="PlaceOwner")]`, route `/placeowner/reviews`, actions: Create GET/POST |
| `src/BoatSpotFinder.Web/Views/Reviews/Create.cshtml` | Shared review form — star picker, optional comment, booking summary block |
| `src/BoatSpotFinder.Web/Models/ReviewCreateViewModel.cs` | Create form model — `BookingId`, `Score`, `Comment`, summary read-only fields populated by controller |
| `src/BoatSpotFinder.Web/Models/ReviewSummaryViewModel.cs` | `Score`, `Comment`, `CreatedAt`, `ReviewerRole` — used when displaying a submitted review |
| `src/BoatSpotFinder.Web/Models/BookingReviewStatusViewModel.cs` | `CanCurrentUserReview`, `ReviewDeadline`, `CurrentUserScore`, `OtherPartyScore` |
| `src/BoatSpotFinder.Web/Models/BookingListItemViewModel.cs` | Extended with `CanCurrentUserReview`, `ReviewDeadline`, `CurrentUserScore`, `BoatOwnerAverageRating`, `BoatOwnerReviewCount` |

---

## Business Rules

- Only bookings with `Status == Completed` can be reviewed. `CanReviewAsync` returns not-eligible for any other status.
- The unique index `(BookingId, ReviewerRole)` enforces exactly one review per direction per booking. There is no edit or delete path.
- The review window is 14 days from `Booking.EndDate`. `CanReviewAsync` computes `booking.EndDate.AddDays(14) >= DateOnly.FromDateTime(DateTime.UtcNow)`. After the window closes, `Create GET` returns 404.
- Role is resolved inside `CanReviewAsync`: `userId == booking.BoatOwnerId` → `ReviewerRole.BoatOwner`; `IMarinaAdminRepository.ExistsAsync(booking.Spot.MarinaId, userId)` → `ReviewerRole.PlaceOwner`. Any other caller is not eligible.
- Marina average rating (`AverageRating`) is the mean of all `Review.Score` where `ReviewerRole == BoatOwner` for spots in that marina. BoatOwner average (`AverageRatingAsBoatOwner`) is the mean of all `Review.Score` where `ReviewerRole == PlaceOwner` for bookings owned by that user.
- Both averages are **recomputed in full** on each new review — not incremented. `GetAllByMarinaIdAsync` / `GetAllByBoatOwnerIdAsync` include the just-inserted row because `AddAsync` commits before the recompute. For ApplicationUser updates, `UserManager.FindByIdAsync` is called to get a fresh instance; calling `UpdateAsync` on a stale instance would produce a no-op due to a mismatched `ConcurrencyStamp`.
- DB writes (review insert + rating update) are committed before ES sync. ES failures are logged via `ILogger.LogError` and do not roll back DB writes. Index drift is recovered by the startup seed on next process start.
- All marina admins receive the PlaceOwner review-invite email. Only the first admin to submit a review succeeds; later admins receive 404 from `Create GET` (via `CanReviewAsync` returning "You have already reviewed this booking"). This is a benign race — no data is corrupted.
- `Score` is validated client-side via `[Required]` + `[Range(1, 5)]` on `ReviewCreateViewModel` and enforced at the DB level by check constraint `CK_Reviews_Score`. `Comment` is optional and capped at 2000 characters by `[StringLength(2000)]`.
- Both controllers use an explicit `return View("~/Views/Reviews/Create.cshtml", model)` path because the shared view lives outside each controller's default view folder.

---

## Quick Reference

| Task | How |
|---|---|
| BoatOwner submits review | `GET /reviews/create?bookingId={id}` → `POST /reviews/create` |
| PlaceOwner submits review | `GET /placeowner/reviews/create?bookingId={id}` → `POST /placeowner/reviews/create` |
| Check eligibility | `IReviewService.CanReviewAsync(bookingId, userId)` |
| Get all reviews for a booking | `IReviewService.GetForBookingAsync(bookingId)` |
| Get recent reviews for a marina | `IReviewRepository.GetRecentByMarinaIdAsync(marinaId, count)` |
| Check if review already exists | `IReviewRepository.ExistsAsync(bookingId, reviewerRole)` |
| Review window duration | 14 days from `Booking.EndDate` |
| Rating precision | `decimal(18,2)` — `HasPrecision(18, 2)` in EF config |
