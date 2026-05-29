using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IReviewSearchService
{
    Task IndexAsync(Review review);
}
