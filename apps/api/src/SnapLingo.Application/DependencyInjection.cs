using Microsoft.Extensions.DependencyInjection;
using SnapLingo.Application.Audio;
using SnapLingo.Application.Emotion;
using SnapLingo.Application.Lessons;
using SnapLingo.Application.Media;
using SnapLingo.Application.Review;

namespace SnapLingo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IEmotionPolicy, EmotionPolicy>();
        services.AddScoped<PhotoService>();
        services.AddScoped<LessonService>();
        services.AddScoped<AudioService>();
        services.AddScoped<ReviewService>();

        return services;
    }
}
