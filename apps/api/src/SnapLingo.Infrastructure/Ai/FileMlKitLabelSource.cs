using System.Text.Json;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Common;
using SnapLingo.Application.Stories;

namespace SnapLingo.Infrastructure.Ai;

public sealed class MlKitOptions
{
    public const string SectionName = "MlKit";

    /// <summary>Folder holding one `&lt;sanitized-photo-object-key&gt;.json` file per photo, each
    /// a JSON array of `{ "text": ..., "confidence": ... }` — produced by the mobile label-dump
    /// tool (see <c>apps/mobile/lib/dev/mlkit_label_dump_page.dart</c>) on a real device.</summary>
    public string LabelsDirectory { get; set; } = "mlkit-labels";
}

/// <summary>Reads labels a real phone already produced; this adapter does no inference itself —
/// it is only the file-based bridge that lets the A/B experiment run from the backend.</summary>
public sealed class FileMlKitLabelSource(IOptions<MlKitOptions> options) : IMlKitLabelSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<MlKitLabel>> GetLabelsAsync(string photoObjectKey, CancellationToken ct)
    {
        var path = Path.Combine(options.Value.LabelsDirectory, SanitizeFileName(photoObjectKey) + ".json");
        if (!File.Exists(path))
        {
            throw new AppValidationException(
                $"No ML Kit label dump at '{path}'. Run the mobile label-dump tool for this photo first.");
        }

        await using var stream = File.OpenRead(path);
        var labels = await JsonSerializer.DeserializeAsync<List<MlKitLabel>>(stream, JsonOptions, ct);
        return labels ?? [];
    }

    private static string SanitizeFileName(string photoObjectKey)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(photoObjectKey.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
