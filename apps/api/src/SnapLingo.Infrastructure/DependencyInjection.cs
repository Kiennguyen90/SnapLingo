using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Ai;
using SnapLingo.Application.Audio;
using SnapLingo.Application.Media;
using SnapLingo.Infrastructure.Ai;
using SnapLingo.Infrastructure.Audio;
using SnapLingo.Infrastructure.Common;
using SnapLingo.Infrastructure.Persistence;
using SnapLingo.Infrastructure.Storage;

namespace SnapLingo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // An empty (not just missing) value reaches here too — e.g. appsettings.json's
            // default is "" and ASPNETCORE_ENVIRONMENT wasn't set to Development, so the
            // appsettings.Development.json overlay never applied. Fail with a clear message
            // instead of the cryptic ArgumentNullException that AddNpgSql's Guard.ThrowIfNull
            // throws for an empty string ("Value cannot be null" even though it wasn't null).
            throw new InvalidOperationException("Missing ConnectionStrings:Default configuration.");
        }

        services.AddDbContext<SnapLingoDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<SnapLingoDbContext>());

        services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres");

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<PhotoOptions>(configuration.GetSection(PhotoOptions.SectionName));
        services.Configure<AudioOptions>(configuration.GetSection(AudioOptions.SectionName));

        services.AddSingleton<SignedUrlSigner>();
        services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Fake adapters until Phase 4 (OpenAI) / Phase 5 (TTS) land — same interfaces, no
        // business-logic changes required when they're swapped in.
        services.AddSingleton<ILessonGenerator, FakeLessonGenerator>();
        services.AddSingleton<ITextToSpeechService, FakeTextToSpeechService>();

        return services;
    }
}
