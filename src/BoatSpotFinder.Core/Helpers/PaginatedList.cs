namespace BoatSpotFinder.Core.Helpers;

public class PaginatedList<T>
{
    public IReadOnlyList<T> Items { get; }
    public int PageIndex { get; }
    public int TotalPages { get; }
    public int TotalCount { get; }
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    private PaginatedList(IReadOnlyList<T> items, int totalCount, int pageIndex, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageIndex = pageIndex;
        TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    // Slices an already-fetched in-memory list; TotalCount is derived from source.Count.
    public static PaginatedList<T> CreateFromList(IReadOnlyList<T> source, int pageIndex, int pageSize)
    {
        var items = source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedList<T>(items, source.Count, pageIndex, pageSize);
    }

    // Accepts an already-sliced page of items with an externally known total count
    // (the Elasticsearch path: items are the SQL-fetched page, totalCount is the full ES id-list length).
    public static PaginatedList<T> CreateFromItems(IReadOnlyList<T> items, int totalCount, int pageIndex, int pageSize)
    {
        return new PaginatedList<T>(items, totalCount, pageIndex, pageSize);
    }
}
