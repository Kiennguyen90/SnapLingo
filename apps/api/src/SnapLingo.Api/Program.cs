using Microsoft.EntityFrameworkCore;
using SnapLingo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Default configuration.");

builder.Services.AddDbContext<SnapLingoDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SnapLingo API",
        Version = "v1",
        Description = "Phase 1: domain, database and health check. No AI integration yet."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SnapLingo API v1");
});

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { service = "SnapLingo API", status = "running" }));

app.Run();
