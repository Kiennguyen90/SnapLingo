namespace SnapLingo.Application.Abstractions;

/// <summary>Testing seam for "now" — the review scheduler and lesson timestamps depend on it.</summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
