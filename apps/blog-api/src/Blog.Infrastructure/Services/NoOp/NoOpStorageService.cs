using Blog.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Blog.Infrastructure.Services.NoOp;

/// <summary>
/// Placeholder storage service for Phase 2. Phase 4 replaces with MinIO implementation.
/// Returns a deterministic placeholder URL so callers are not blocked by missing storage.
/// </summary>
public class NoOpStorageService(ILogger<NoOpStorageService> logger) : IStorageService
{
    public Task<string> UploadAsync(
        string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        logger.LogDebug(
            "NoOpStorageService: would upload {FileName} ({ContentType})",
            fileName, contentType);
        return Task.FromResult($"https://storage.placeholder/{fileName}");
    }

    public Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        logger.LogDebug("NoOpStorageService: would delete {FileUrl}", fileUrl);
        return Task.CompletedTask;
    }
}
