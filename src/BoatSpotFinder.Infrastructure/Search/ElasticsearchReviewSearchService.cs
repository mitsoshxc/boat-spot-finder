using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;

namespace BoatSpotFinder.Infrastructure.Search;

public class ElasticsearchReviewSearchService : IReviewSearchService
{
    private const string IndexName = "reviews";

    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchReviewSearchService> _logger;

    private record ReviewDocument(
        Guid Id,
        Guid BookingId,
        string ReviewerRole,
        Guid? MarinaId,
        string? BoatOwnerId,
        int Score,
        string? Comment,
        DateTime CreatedAt);

    public ElasticsearchReviewSearchService(
        ElasticsearchClient client,
        ILogger<ElasticsearchReviewSearchService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task IndexAsync(Review review)
    {
        try
        {
            if (review.Booking is null)
            {
                _logger.LogWarning("Skipping ES indexing for review {ReviewId}: Booking navigation not loaded.", review.Id);
                return;
            }

            Guid? marinaId;
            string? boatOwnerId;

            if (review.ReviewerRole == ReviewerRole.BoatOwner)
            {
                marinaId = review.Booking.Spot.MarinaId;
                boatOwnerId = null;
            }
            else
            {
                marinaId = null;
                boatOwnerId = review.Booking.BoatOwnerId;
            }

            var doc = new ReviewDocument(
                review.Id,
                review.BookingId,
                review.ReviewerRole.ToString(),
                marinaId,
                boatOwnerId,
                review.Score,
                review.Comment,
                review.CreatedAt);

            await _client.IndexAsync(doc, i => i.Index(IndexName).Id(review.Id.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index review {ReviewId} in Elasticsearch.", review.Id);
        }
    }
}
