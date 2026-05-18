namespace BoatSpotFinder.Core.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string relativeFileName, string contentType);
    Task DeleteAsync(string relativePath);
}
