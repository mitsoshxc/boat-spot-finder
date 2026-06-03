namespace BoatSpotFinder.Core.Common;

public record ServiceResult(bool Success, IEnumerable<string> Errors)
{
    public static ServiceResult Ok() => new(true, Array.Empty<string>());
    public static ServiceResult Fail(params string[] errors) => new(false, errors);
}

public record ServiceResult<T>(bool Success, IEnumerable<string> Errors, T Value)
{
    public static ServiceResult<T> Ok(T value) => new(true, Array.Empty<string>(), value);
    public static ServiceResult<T> Fail(params string[] errors) => new(false, errors, default!);
}
