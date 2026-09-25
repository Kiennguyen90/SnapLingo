using Microsoft.Extensions.Options;
using SnapLingo.Application.Stories;

namespace SnapLingo.Infrastructure.OpenAi;

/// <summary>Neural text embeddings for memory retrieval (Module 2).</summary>
public sealed class OpenAiTextEmbedder(OpenAiClient client, IOptions<OpenAiOptions> options) : ITextEmbedder
{
    public string ModelName => options.Value.EmbeddingModel;

    public Task<float[]> EmbedAsync(string text, CancellationToken ct) => client.CreateEmbeddingAsync(text, ct);
}
