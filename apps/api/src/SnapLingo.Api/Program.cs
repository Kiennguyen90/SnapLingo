using System.Text.Json.Serialization;
using SnapLingo.Api.CurrentUser;
using SnapLingo.Api.Endpoints;
using SnapLingo.Api.Middleware;
using SnapLingo.Application;
using SnapLingo.Application.Abstractions;
using SnapLingo.Infrastructure;
using SnapLingo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, DevCurrentUserService>();

builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SnapLingo API",
        Version = "v1",
        Description = "Photos, lesson generation (fake AI adapter) and spaced-repetition review. No real OpenAI integration yet."
    });
});

var app = builder.Build();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SnapLingo API v1");
});

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { service = "SnapLingo API", status = "running" }));

app.MapPhotosEndpoints();
app.MapLessonsEndpoints();
app.MapReviewsEndpoints();
app.MapMediaEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SnapLingoDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.Run();
