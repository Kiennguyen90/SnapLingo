using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Stories;

/// <summary>A past event retrieved as relevant to the current one, with the score components
/// kept separately so retrieval quality (Recall@k, proposal section 6.3) can be analyzed later.</summary>
public sealed record MemoryHit(PhotoEvent Event, double Score, double Semantic, double Recency, double Recurrence);

/// <summary>
/// Module 2 (proposal section 5.2): the learner's personal multimodal memory. Every photo becomes
/// one <see cref="PhotoEvent"/> (Module 1 output + embedding + local time); retrieval ranks past
/// events by semantic similarity, recency and how often the same place recurs.
/// </summary>
public sealed class MemoryService(
    IAppDbContext db,
    IEventExtractor extractor,
    ITextEmbedder embedder,
    IDateTimeProvider clock,
    IOptions<StoryPipelineOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly StoryPipelineOptions _options = options.Value;

    public sealed record EventWithUsage(PhotoEvent Event, int? InputTokens, int? OutputTokens, bool Extracted);

    /// <summary>Returns the photo's event, extracting and storing it on first use.</summary>
    public async Task<EventWithUsage> GetOrCreateEventAsync(Photo photo, CancellationToken ct)
    {
        var existing = await db.PhotoEvents.FirstOrDefaultAsync(e => e.PhotoId == photo.Id, ct);
        if (existing is not null)
        {
            return new EventWithUsage(existing, null, null, Extracted: false);
        }

        var result = await extractor.ExtractAsync(
            new EventExtractionInput(photo.Id, photo.ObjectKey, photo.MimeType, photo.Note, photo.ActivityTag), ct);
        var extracted = result.Event;

        if (string.IsNullOrWhiteSpace(extracted.Scene) || string.IsNullOrWhiteSpace(extracted.Description))
        {
            throw new AppValidationException("Event extraction returned no scene/description.");
        }

        var moment = LocalMoment.For(photo, _options.DefaultUtcOffsetMinutes);
        var placeKey = NormalizePlaceKey(extracted.PlaceKey, extracted.Scene);

        var photoEvent = new PhotoEvent
        {
            Id = Guid.NewGuid(),
            UserId = photo.UserId,
            PhotoId = photo.Id,
            OccurredAt = moment.Utc,
            LocalDayOfWeek = moment.DayOfWeek,
            LocalMinuteOfDay = moment.MinuteOfDay,
            Scene = extracted.Scene.Trim(),
            PlaceKey = placeKey,
            ObjectsJson = JsonSerializer.Serialize(Clean(extracted.Objects), JsonOptions),
            ActivitiesJson = JsonSerializer.Serialize(Clean(extracted.Activities), JsonOptions),
            Description = extracted.Description.Trim(),
            PeopleCount = Math.Max(0, extracted.PeopleCount),
            Confidence = extracted.Confidence,
            ExtractorModel = extractor.ModelName,
            EmbeddingModel = embedder.ModelName,
            CreatedAt = clock.UtcNow
        };
        photoEvent.Embedding = await embedder.EmbedAsync(EmbeddingText(photoEvent, photo), ct);

        db.PhotoEvents.Add(photoEvent);
        await db.SaveChangesAsync(ct);

        return new EventWithUsage(photoEvent, result.InputTokens, result.OutputTokens, Extracted: true);
    }

    /// <summary>
    /// Top-k past events related to <paramref name="current"/>. Only events that happened
    /// strictly before it are candidates, so a story can never "remember" the future and the
    /// time-split evaluation (proposal section 6.1, split A) holds by construction.
    /// </summary>
    public async Task<IReadOnlyList<MemoryHit>> RetrieveAsync(PhotoEvent current, CancellationToken ct)
    {
        var o = _options.Memory;
        var since = current.OccurredAt.AddDays(-o.LookbackDays);

        var candidates = await db.PhotoEvents
            .Include(e => e.Photo)
            .Where(e => e.UserId == current.UserId
                        && e.Id != current.Id
                        && e.OccurredAt < current.OccurredAt
                        && e.OccurredAt >= since)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return [];
        }

        var samePlaceCount = candidates.Count(e => e.PlaceKey == current.PlaceKey);

        return candidates
            .Select(e =>
            {
                var semantic = Math.Max(0, Cosine(current.Embedding, e.Embedding));
                var days = (current.OccurredAt - e.OccurredAt).TotalDays;
                var recency = Math.Pow(0.5, days / o.RecencyHalfLifeDays);
                // Recurrence rewards the place the learner keeps coming back to: the more
                // past visits to the current place, the more each of them matters.
                var recurrence = e.PlaceKey == current.PlaceKey ? Math.Min(1.0, samePlaceCount / 3.0) : 0;
                var score = o.SemanticWeight * semantic + o.RecencyWeight * recency + o.RecurrenceWeight * recurrence;
                return new MemoryHit(e, score, semantic, recency, recurrence);
            })
            .Where(h => h.Score >= o.MinScore)
            .OrderByDescending(h => h.Score)
            .Take(o.TopK)
            .OrderBy(h => h.Event.OccurredAt)
            .ToList();
    }

    public static IReadOnlyList<string> Objects(PhotoEvent e) => ReadList(e.ObjectsJson);

    public static IReadOnlyList<string> Activities(PhotoEvent e) => ReadList(e.ActivitiesJson);

    internal static string EmbeddingText(PhotoEvent e, Photo photo) =>
        string.Join(". ", new[]
        {
            e.Scene,
            e.Description,
            "Objects: " + string.Join(", ", Objects(e)),
            "Activities: " + string.Join(", ", Activities(e)),
            photo.ActivityTag is null ? null : "Tag: " + photo.ActivityTag
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length)
        {
            return 0;
        }

        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    internal static string NormalizePlaceKey(string? placeKey, string scene)
    {
        var raw = string.IsNullOrWhiteSpace(placeKey) ? scene : placeKey;
        var chars = raw.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        var key = string.Join('_', new string(chars).Split('_', StringSplitOptions.RemoveEmptyEntries));
        return key.Length > 100 ? key[..100] : (key.Length == 0 ? "unknown_place" : key);
    }

    private static List<string> Clean(IEnumerable<string>? items) =>
        (items ?? []).Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static IReadOnlyList<string> ReadList(string json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
}
