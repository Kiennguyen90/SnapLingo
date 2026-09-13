using SnapLingo.Application.Media;

namespace SnapLingo.Application.Tests.Fakes;

public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, byte[]> _objects = [];

    public async Task<StoredObject> PutAsync(string suggestedFileName, Stream content, string contentType, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _objects[suggestedFileName] = buffer.ToArray();
        return new StoredObject(suggestedFileName, buffer.Length);
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct) =>
        Task.FromResult<Stream>(new MemoryStream(_objects[objectKey]));

    public Task DeleteAsync(string objectKey, CancellationToken ct)
    {
        _objects.Remove(objectKey);
        return Task.CompletedTask;
    }

    public string GetSignedUrl(string objectKey, TimeSpan validFor) => $"signed://{objectKey}?exp={validFor.TotalSeconds}";
}
