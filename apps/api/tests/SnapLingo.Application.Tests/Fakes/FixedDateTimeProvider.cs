using SnapLingo.Application.Abstractions;

namespace SnapLingo.Application.Tests.Fakes;

public sealed class FixedDateTimeProvider(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; } = now;
}
