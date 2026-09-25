using System.Text;
using SnapLingo.Application.Stories;

namespace SnapLingo.Infrastructure.Ai;

/// <summary>
/// Free, deterministic embedding: a bag of words and word bigrams hashed into a fixed number of
/// buckets (the "hashing trick"), L2-normalized. Much weaker than a neural embedding, but it
/// ranks "iced coffee at a sidewalk café" close to another café visit, which is enough for
/// retrieval to work offline and in tests. Used when no OpenAI key is configured.
/// </summary>
public sealed class HashingTextEmbedder : ITextEmbedder
{
    public const int Dimensions = 256;

    public string ModelName => "hashing-bow-256";

    public Task<float[]> EmbedAsync(string text, CancellationToken ct) => Task.FromResult(Embed(text));

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var words = Tokenize(text);

        for (var i = 0; i < words.Count; i++)
        {
            Add(vector, words[i], 1f);
            if (i + 1 < words.Count)
            {
                Add(vector, words[i] + " " + words[i + 1], 0.5f);
            }
        }

        var norm = Math.Sqrt(vector.Sum(v => v * v));
        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] = (float)(vector[i] / norm);
            }
        }

        return vector;
    }

    private static List<string> Tokenize(string text) =>
        new string(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .ToList();

    private static void Add(float[] vector, string token, float weight)
    {
        // FNV-1a: stable across processes, unlike string.GetHashCode().
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(token))
        {
            hash = (hash ^ b) * 16777619u;
        }

        vector[hash % Dimensions] += weight;
    }

    private static readonly HashSet<string> StopWords =
        ["the", "and", "with", "for", "are", "was", "this", "that", "objects", "activities", "tag"];
}
