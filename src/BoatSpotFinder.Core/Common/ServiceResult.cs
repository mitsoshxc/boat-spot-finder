namespace BoatSpotFinder.Core.Common;

public record ServiceResult(bool Success, IEnumerable<string> Errors)
{
    public static ServiceResult Ok() => new(true, Array.Empty<string>());
    public static ServiceResult Fail(params string[] errors) => new(false, errors);
}
