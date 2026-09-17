namespace SnapLingo.Infrastructure.OpenAi;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    /// <summary>
    /// Never put a real value in appsettings.json (it would get committed). Set locally via
    /// `dotnet user-secrets set "OpenAi:ApiKey" "sk-..."` in SnapLingo.Api, or the
    /// OpenAi__ApiKey environment variable for CI/deployment.
    /// </summary>
    public string ApiKey { get; set; } = "";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    /// <summary>Must support vision (image input) + Structured Outputs. Kept in config per spec — never hard-coded.</summary>
    public string Model { get; set; } = "gpt-4o";

    public double Temperature { get; set; } = 0.5;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Total attempts (including the first), for 429/5xx/timeout responses only.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Backoff base: delay before retry N is N * this many seconds. Lowered in tests for speed.</summary>
    public double RetryBaseDelaySeconds { get; set; } = 2.0;
}
