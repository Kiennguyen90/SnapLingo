using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SnapLingo.Api.CurrentUser;
using SnapLingo.Api.Endpoints;
using SnapLingo.Api.Middleware;
using SnapLingo.Application;
using SnapLingo.Application.Abstractions;
using SnapLingo.Infrastructure;
using SnapLingo.Infrastructure.Auth;
using SnapLingo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, JwtCurrentUserService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

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
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SnapLingo API",
        Version = "v1",
        Description = "Auth, photos with capture metadata, grounded memory-augmented story lessons with a verification report, person labels, feedback and spaced-repetition review."
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by POST /api/v1/auth/login.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearerScheme] = [] });
});

var app = builder.Build();

if (app.Environment.IsProduction())
{
    // Fail fast rather than ship with the well-known development secrets from appsettings.json.
    if (jwtOptions.SigningKey.StartsWith("dev-", StringComparison.Ordinal))
        throw new InvalidOperationException("Jwt:SigningKey must be overridden in Production (env var Jwt__SigningKey).");
    if ((builder.Configuration["Storage:SigningKey"] ?? "").StartsWith("dev-", StringComparison.Ordinal))
        throw new InvalidOperationException("Storage:SigningKey must be overridden in Production (env var Storage__SigningKey).");
}

app.UseExceptionHandler();

if (!app.Environment.IsProduction() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SnapLingo API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { service = "SnapLingo API", status = "running" }));

app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapPhotosEndpoints();
app.MapLessonsEndpoints();
app.MapReviewsEndpoints();
app.MapPeopleEndpoints();
app.MapMediaEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SnapLingoDbContext>();

    // Opt-in (Database__MigrateOnStartup=true) so containers can bring their own schema up.
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        await db.Database.MigrateAsync();

    // The seeded demo account has a publicly documented password: never create it in Production
    // unless explicitly requested.
    if (!app.Environment.IsProduction() || app.Configuration.GetValue<bool>("Database:SeedDemoUser"))
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.PasswordHasher<SnapLingo.Domain.Entities.User>>();
        await DbSeeder.SeedAsync(db, passwordHasher);
    }
}

app.Run();
