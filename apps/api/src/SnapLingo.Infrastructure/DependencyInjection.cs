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
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Default configuration.");

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
