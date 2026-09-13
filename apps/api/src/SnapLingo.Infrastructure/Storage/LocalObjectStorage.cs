using Microsoft.Extensions.Options;
using SnapLingo.Application.Media;

namespace SnapLingo.Infrastructure.Storage;

/// <summary>
/// Dev/local <see cref="IObjectStorage"/> backed by the filesystem, per spec section 7.3
/// ("provide local filesystem/S3-compatible implementation for development"). Swap for an
/// S3-compatible implementation behind the same interface for production.
/// </summary>
public sealed class LocalObjectStorage(IOptions<StorageOptions> options, SignedUrlSigner signer) : IObjectStorage
{
    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath);

    public async Task<StoredObject> PutAsync(string suggestedFileName, Stream content, string contentType, CancellationToken ct)
    {
        var objectKey = suggestedFileName.Replace('\\', '/').TrimStart('/');
        var fullPath = ResolvePath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, ct);

        return new StoredObject(objectKey, file.Length);
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct)
    {
        var fullPath = ResolvePath(objectKey);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Object '{objectKey}' was not found in storage.", fullPath);
        }

        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string objectKey, CancellationToken ct)
    {
        var fullPath = ResolvePath(objectKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetSignedUrl(string objectKey, TimeSpan validFor) => signer.BuildUrl(objectKey, validFor);

    private string ResolvePath(string objectKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, objectKey));
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Object key resolves outside the storage root.");
        }

        return fullPath;
    }
}
