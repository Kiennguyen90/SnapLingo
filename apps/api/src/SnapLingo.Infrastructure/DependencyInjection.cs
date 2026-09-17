using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SnapLingo.Application.Abstractions;
using SnapLingo.Application.Ai;
using SnapLingo.Application.Audio;
using SnapLingo.Application.Auth;
using SnapLingo.Application.Media;
using SnapLingo.Infrastructure.Ai;
using SnapLingo.Infrastructure.Audio;
using SnapLingo.Infrastructure.Auth;
using SnapLingo.Infrastructure.Common;
using SnapLingo.Infrastructure.OpenAi;
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
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));

        services.AddSingleton<SignedUrlSigner>();
        services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Phase 4: ILessonGenerator is real OpenAI when an API key is configured (local
        // `dotnet user-secrets`, or OpenAi__ApiKey in real deployments), and falls back to the
        // deterministic fake otherwise — e.g. CI, or a contributor who hasn't set up a key —
        // so the rest of the app never has to know or care which one is behind the interface.
        var openAiApiKey = configuration[$"{OpenAiOptions.SectionName}:ApiKey"];
        if (!string.IsNullOrWhiteSpace(openAiApiKey))
        {
            services.AddHttpClient<OpenAiClient>((sp, http) =>
            {
                var openAiOptions = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
                http.BaseAddress = new Uri(openAiOptions.BaseUrl);
                http.Timeout = TimeSpan.FromSeconds(openAiOptions.TimeoutSeconds);
            });
            // Scoped, not Singleton: it depends on the typed HttpClient AddHttpClient
            // registers (transient-per-resolve, pooled handlers) — a Singleton would
            // capture one HttpClient for the app's whole lifetime (captive dependency).
            services.AddScoped<ILessonGenerator, OpenAiLessonGenerator>();
        }
        else
        {
            services.AddSingleton<ILessonGenerator, FakeLessonGenerator>();
        }

        // TTS is still Phase 5 / fake regardless of the OpenAI switch above.
        services.AddSingleton<ITextToSpeechService, FakeTextToSpeechService>();

        return services;
    }
}
