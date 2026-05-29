using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;

namespace BoatSpotFinder.Infrastructure.Search;

public class NullReviewSearchService : IReviewSearchService
{
    public Task IndexAsync(Review review) => Task.CompletedTask;
}
