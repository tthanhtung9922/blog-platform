namespace Blog.Application.Abstractions;
public interface IStorageService
{
    Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string fileUrl, CancellationToken ct = default);
}
