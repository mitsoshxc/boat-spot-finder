using BoatSpotFinder.Web.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BoatSpotFinder.Tests.Infrastructure;

public class LocalFileStorageServiceTests
{
    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "";
    }

    [Fact]
    public async Task Save_DoesNotDisposeStream()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "bsf-tests-" + Guid.NewGuid().ToString("N"));
        var env = new StubWebHostEnvironment { WebRootPath = webRoot };
        var bytes = new byte[] { 1, 2, 3, 4 };
        using var source = new MemoryStream(bytes);
        var service = new LocalFileStorageService(env);

        try
        {
            await service.SaveAsync(source, "marina-backgrounds/test.bin", "application/octet-stream");

            Assert.True(source.CanRead);
            source.Position = 0;
            var roundTrip = new byte[4];
            var n = source.Read(roundTrip, 0, 4);
            Assert.Equal(4, n);
            Assert.Equal(bytes, roundTrip);
        }
        finally
        {
            if (Directory.Exists(webRoot))
            {
                Directory.Delete(webRoot, recursive: true);
            }
        }
    }
}
