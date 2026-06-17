using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;
using Microsoft.Extensions.Logging;

namespace BoatSpotFinder.Infrastructure.Search;

public class ElasticsearchMarinaSearchService : IMarinaSearchService
{
    private const string IndexName = "marinas";

    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchMarinaSearchService> _logger;

    private bool _indexEnsured;

    private record MarinaDocument(Guid Id, string Name, string Region, string Phone, string Address, string Description, decimal? AverageRating, int ReviewCount);

    public ElasticsearchMarinaSearchService(
        ElasticsearchClient client,
        ILogger<ElasticsearchMarinaSearchService> logger)
    {
        _client = client;
        _logger = logger;
    }

    private async Task EnsureIndexAsync()
    {
        if (_indexEnsured)
        {
            return;
        }

        var exists = await _client.Indices.ExistsAsync(IndexName);
        if (!exists.Exists)
        {
            // Raw JSON is used here because the typed fluent API does not expose
            // NGramTokenizer or the analysis block under IndexSettings.
            const string indexDefinition = """
                {
                  "settings": {
                    "index": { "max_ngram_diff": 13 },
                    "analysis": {
                      "tokenizer": {
                        "marina_ngram_tokenizer": {
                          "type": "ngram",
                          "min_gram": 2,
                          "max_gram": 15,
                          "token_chars": ["letter", "digit"]
                        }
                      },
                      "analyzer": {
                        "marina_ngram_analyzer": {
                          "type": "custom",
                          "tokenizer": "marina_ngram_tokenizer",
                          "filter": ["lowercase"]
                        },
                        "marina_ngram_search_analyzer": {
                          "type": "custom",
                          "tokenizer": "standard",
                          "filter": ["lowercase"]
                        }
                      }
                    }
                  },
                  "mappings": {
                    "properties": {
                      "name": {
                        "type": "text",
                        "analyzer": "standard",
                        "fields": {
                          "ngram":   { "type": "text", "analyzer": "marina_ngram_analyzer", "search_analyzer": "marina_ngram_search_analyzer" },
                          "keyword": { "type": "keyword" }
                        }
                      },
                      "region": {
                        "type": "text",
                        "analyzer": "standard",
                        "fields": {
                          "ngram":   { "type": "text", "analyzer": "marina_ngram_analyzer", "search_analyzer": "marina_ngram_search_analyzer" },
                          "keyword": { "type": "keyword" }
                        }
                      }
                    }
                  }
                }
                """;

            await _client.Transport.RequestAsync<ElasticsearchStringResponse>(
                Elastic.Transport.HttpMethod.PUT,
                $"/{IndexName}",
                PostData.String(indexDefinition));
        }

        _indexEnsured = true;
    }

    public async Task IndexAsync(Marina marina)
    {
        try
        {
            await EnsureIndexAsync();

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
                    .Query(q => q.Bool(b => b
                        // search_analyzer on the .ngram sub-field uses standard+lowercase so
                        // the query text itself is not n-grammed, preventing over-matching
                        .MinimumShouldMatch(1)
                        .Should(
                            mm => mm.MultiMatch(m => m
                                .Fields(new[] { "name^3", "region^2", "address", "description", "phone" })
                                .Query(query)
                                .Fuzziness(new Fuzziness("AUTO"))),
                            mm => mm.MultiMatch(m => m
                                .Fields(new[] { "name.ngram^2", "region.ngram" })
                                .Query(query))))));
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
