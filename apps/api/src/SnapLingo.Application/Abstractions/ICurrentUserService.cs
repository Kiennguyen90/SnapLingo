namespace SnapLingo.Application.Abstractions;

/// <summary>
/// Resolves the caller's user id for the current request. No authentication phase exists
/// yet (see README), so the API-layer implementation falls back to a seeded demo user —
/// this interface is the seam that a real auth phase will replace later.
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
}
