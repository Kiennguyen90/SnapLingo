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
using SnapLingo.Application.Music;
using SnapLingo.Application.Stories;
using SnapLingo.Infrastructure.Auth;
using SnapLingo.Infrastructure.Common;
using SnapLingo.Infrastructure.Music;
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
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<StoryPipelineOptions>(configuration.GetSection(StoryPipelineOptions.SectionName));
        services.Configure<FeatureOptions>(configuration.GetSection(FeatureOptions.SectionName));

        services.AddSingleton<SignedUrlSigner>();
        services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Scoped, not Singleton, for the same captive-dependency reason as OpenAiClient below:
        // it depends on the typed HttpClient AddHttpClient registers.
        services.AddHttpClient<GoogleIdTokenValidator>();
        services.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidator>();

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
            services.AddScoped<IEventExtractor, OpenAiEventExtractor>();
            services.AddScoped<IClaimJudge, OpenAiClaimJudge>();
            services.AddScoped<ITextEmbedder, OpenAiTextEmbedder>();
            // Real narration. Scoped for the same typed-HttpClient reason as the adapters above.
            services.AddScoped<ITextToSpeechService, OpenAiTextToSpeechService>();
        }
        else
        {
            // The whole grounded pipeline (Modules 1–4) keeps working offline: deterministic
            // fakes that follow the same contracts, plus a real (hashing) text embedder.
            services.AddSingleton<ILessonGenerator, FakeLessonGenerator>();
            services.AddSingleton<IEventExtractor, FakeEventExtractor>();
            services.AddSingleton<IClaimJudge, CitationClaimJudge>();
            services.AddSingleton<ITextEmbedder, HashingTextEmbedder>();
            // Offline: a silent WAV, so the audio pipeline (storage, cache, signed URLs) still works.
            services.AddSingleton<ITextToSpeechService, FakeTextToSpeechService>();
        }

        // Background music adapter: fake today, same swap-in-a-real-provider-later shape as TTS.
        services.AddSingleton<IBackgroundMusicService, FakeBackgroundMusicService>();

        return services;
    }
}
