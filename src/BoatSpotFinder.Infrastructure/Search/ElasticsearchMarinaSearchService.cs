using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;

namespace BoatSpotFinder.Infrastructure.Search;

public class ElasticsearchMarinaSearchService : IMarinaSearchService
{
    private const string IndexName = "marinas";

    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchMarinaSearchService> _logger;

    private record MarinaDocument(Guid Id, string Name, string Region, string Phone, string Address, string Description, decimal? AverageRating, int ReviewCount);

    public ElasticsearchMarinaSearchService(
        ElasticsearchClient client,
        ILogger<ElasticsearchMarinaSearchService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task IndexAsync(Marina marina)
    {
        try
        {
            var doc = new MarinaDocument(
                marina.Id,
                marina.Name,
                marina.Region,
                marina.Phone,
                marina.Address,
                marina.Description,
                marina.AverageRating,
                marina.ReviewCount);

            await _client.IndexAsync(doc, i => i.Index(IndexName).Id(marina.Id.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index marina {MarinaId} in Elasticsearch.", marina.Id);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        try
        {
            await _client.DeleteAsync<MarinaDocument>(id.ToString(), d => d.Index(IndexName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete marina {MarinaId} from Elasticsearch.", id);
        }
    }

    public async Task<IEnumerable<Guid>?> SearchAsync(string? query)
    {
        try
        {
            SearchResponse<MarinaDocument> response;

            if (string.IsNullOrWhiteSpace(query))
            {
                response = await _client.SearchAsync<MarinaDocument>(s => s
                    .Indices(IndexName)
                    .Size(10000)
                    .Query(q => q.MatchAll(new MatchAllQuery())));
            }
            else
            {
                response = await _client.SearchAsync<MarinaDocument>(s => s
                    .Indices(IndexName)
                    .Size(10000)
                    .Query(q => q.MultiMatch(m => m
                        .Fields(new[] { "name", "region", "phone", "address", "description" })
                        .Query(query)
                        .Fuzziness(new Fuzziness("AUTO")))));
            }

            return response.Hits
                .Select(h => Guid.Parse(h.Id!))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch search failed for query '{Query}'.", query);
            return [];
        }
    }
}
