using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Media;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Application.Audio;

/// <summary>
/// Generates lesson audio on demand and caches it (spec section 11.1): the cache key is
/// SHA256(model + voice + normalized text + style), so replaying the same story/dialogue
/// text with the same voice never calls the TTS adapter twice (AC-04).
/// </summary>
public sealed class AudioService(
    IAppDbContext db,
    ITextToSpeechService tts,
    IObjectStorage storage)
{
    public async Task<(AudioAsset Asset, string SignedUrl)> GetOrCreateAsync(
        Guid lessonId,
        string kind,
        string text,
        string voice,
        string? styleHint,
        CancellationToken ct)
    {
        var normalizedText = Normalize(text);
        var textHash = ComputeHash(tts.ModelName, voice, normalizedText, styleHint);

        var existingForLesson = await db.AudioAssets
            .FirstOrDefaultAsync(a => a.LessonId == lessonId && a.Kind == kind, ct);

        if (existingForLesson is not null &&
            existingForLesson.TextHash == textHash &&
            existingForLesson.Voice == voice &&
            existingForLesson.Model == tts.ModelName)
        {
            return (existingForLesson, storage.GetSignedUrl(existingForLesson.ObjectKey, TimeSpan.FromMinutes(15)));
        }

        // Global reuse: someone else already synthesized this exact text+voice+model — skip
        // the AI/TTS call and re-point at the same object storage bytes.
        var reusable = await db.AudioAssets
            .Where(a => a.TextHash == textHash && a.Voice == voice && a.Model == tts.ModelName)
            .FirstOrDefaultAsync(ct);

        string objectKey;
        int durationMs;

        if (reusable is not null)
        {
            objectKey = reusable.ObjectKey;
            durationMs = reusable.DurationMs;
        }
        else
        {
            var synthesized = await tts.SynthesizeAsync(normalizedText, voice, styleHint, ct);
            await using var stream = new MemoryStream(synthesized.AudioBytes);
            var stored = await storage.PutAsync($"audio/{textHash}.mp3", stream, synthesized.ContentType, ct);
            objectKey = stored.ObjectKey;
            durationMs = synthesized.DurationMs;
        }

        AudioAsset asset;
        if (existingForLesson is not null)
        {
            existingForLesson.ObjectKey = objectKey;
            existingForLesson.TextHash = textHash;
            existingForLesson.Voice = voice;
            existingForLesson.Model = tts.ModelName;
            existingForLesson.DurationMs = durationMs;
            asset = existingForLesson;
        }
        else
        {
            asset = new AudioAsset
            {
                Id = Guid.NewGuid(),
                LessonId = lessonId,
                Kind = kind,
                ObjectKey = objectKey,
                TextHash = textHash,
                Voice = voice,
                Model = tts.ModelName,
                DurationMs = durationMs
            };
            db.AudioAssets.Add(asset);
        }

        await db.SaveChangesAsync(ct);
        return (asset, storage.GetSignedUrl(asset.ObjectKey, TimeSpan.FromMinutes(15)));
    }

    private static string Normalize(string text) =>
        Regex.Replace(text.Trim(), @"\s+", " ");

    private static string ComputeHash(string model, string voice, string normalizedText, string? styleHint)
    {
        var payload = $"{model}|{voice}|{normalizedText}|{styleHint}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
