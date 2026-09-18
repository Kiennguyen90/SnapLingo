using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Common;
using SnapLingo.Application.Media;
using SnapLingo.Domain.Entities;
using SnapLingo.Domain.Enums;

namespace SnapLingo.Application.Music;

/// <summary>
/// Attaches a mood-matched background-music track to a photo and caches it, same shape as
/// <see cref="Audio.AudioService"/>: the cache key is model + mood, so re-requesting the same
/// mood for the same photo never calls the music adapter twice, and two photos that land on
/// the same mood+model reuse one another's already-composed bytes in object storage.
/// </summary>
public sealed class PhotoMusicService(
    IAppDbContext db,
    IBackgroundMusicService music,
    IObjectStorage storage)
{
    public async Task<(PhotoMusicAsset Asset, string SignedUrl)> GetOrCreateAsync(
        Guid userId,
        Guid photoId,
        EmotionType mood,
        CancellationToken ct)
    {
        var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.UserId == userId, ct)
            ?? throw new NotFoundException("Photo not found.");

        var moodKey = mood.ToString().ToLowerInvariant();
        var trackHash = ComputeHash(music.ModelName, moodKey);

        var existingForPhoto = await db.PhotoMusicAssets
            .FirstOrDefaultAsync(m => m.PhotoId == photo.Id, ct);

        if (existingForPhoto is not null &&
            existingForPhoto.TrackHash == trackHash &&
            existingForPhoto.Model == music.ModelName)
        {
            return (existingForPhoto, storage.GetSignedUrl(existingForPhoto.ObjectKey, TimeSpan.FromMinutes(15)));
        }

        // Global reuse: another photo already composed this exact mood+model — skip the
        // music adapter call and re-point at the same object storage bytes.
        var reusable = await db.PhotoMusicAssets
            .Where(m => m.TrackHash == trackHash && m.Model == music.ModelName)
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
            var composed = await music.ComposeAsync(mood, ct);
            await using var stream = new MemoryStream(composed.AudioBytes);
            var stored = await storage.PutAsync($"music/{trackHash}.wav", stream, composed.ContentType, ct);
            objectKey = stored.ObjectKey;
            durationMs = composed.DurationMs;
        }

        PhotoMusicAsset asset;
        if (existingForPhoto is not null)
        {
            existingForPhoto.Mood = moodKey;
            existingForPhoto.ObjectKey = objectKey;
            existingForPhoto.TrackHash = trackHash;
            existingForPhoto.Model = music.ModelName;
            existingForPhoto.DurationMs = durationMs;
            asset = existingForPhoto;
        }
        else
        {
            asset = new PhotoMusicAsset
            {
                Id = Guid.NewGuid(),
                PhotoId = photo.Id,
                Mood = moodKey,
                ObjectKey = objectKey,
                TrackHash = trackHash,
                Model = music.ModelName,
                DurationMs = durationMs
            };
            db.PhotoMusicAssets.Add(asset);
        }

        await db.SaveChangesAsync(ct);
        return (asset, storage.GetSignedUrl(asset.ObjectKey, TimeSpan.FromMinutes(15)));
    }

    private static string ComputeHash(string model, string moodKey)
    {
        var payload = $"{model}|{moodKey}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
