using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SnapLingo.Application.Audio;
using SnapLingo.Application.Auth;
using SnapLingo.Application.Emotion;
using SnapLingo.Application.Lessons;
using SnapLingo.Application.Media;
using SnapLingo.Application.Music;
using SnapLingo.Application.People;
using SnapLingo.Application.Review;
using SnapLingo.Application.Stories;
using SnapLingo.Application.Users;
using SnapLingo.Domain.Entities;

namespace SnapLingo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IEmotionPolicy, EmotionPolicy>();
        services.AddSingleton<PasswordHasher<User>>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<PhotoService>();
        services.AddScoped<LessonService>();
        services.AddScoped<MemoryService>();
        services.AddScoped<GroundingVerifier>();
        services.AddScoped<StoryPipeline>();
        services.AddScoped<PeopleService>();
        services.AddScoped<AudioService>();
        services.AddScoped<PhotoMusicService>();
        services.AddScoped<ReviewService>();

        return services;
    }
}
