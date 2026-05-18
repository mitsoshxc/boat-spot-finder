using BoatSpotFinder.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace BoatSpotFinder.Web.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveAsync(Stream stream, string relativeFileName, string contentType)
    {
        var diskPath = Path.Combine(_env.WebRootPath, "uploads", relativeFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);

        await using var fileStream = new FileStream(diskPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);

        return $"/uploads/{relativeFileName}";
    }

    public Task DeleteAsync(string relativePath)
    {
        var diskPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));
        if (File.Exists(diskPath))
            File.Delete(diskPath);

        return Task.CompletedTask;
    }
}
