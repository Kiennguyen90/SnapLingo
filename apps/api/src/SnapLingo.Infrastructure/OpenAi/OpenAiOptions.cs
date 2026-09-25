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

    /// <summary>Raised from 0.5 — at 0.5 the story read like a checklist (one flat sentence
    /// per vocabulary word); higher lets the model vary sentence structure and phrasing while
    /// the prompt's own grounding rules (never invent visual facts) keep it from drifting.</summary>
    public double Temperature { get; set; } = 0.8;

    /// <summary>Vision model for Module 1 (event extraction). Run at temperature 0: it records
    /// facts, and the same photo should always yield the same event.</summary>
    public string ExtractorModel { get; set; } = "gpt-4o";

    /// <summary>Model for the grounding judge (Module 4b). A smaller model keeps verification
    /// cheap (proposal risk table: "use a small model for the verification step"); temperature 0.</summary>
    public string VerifierModel { get; set; } = "gpt-4o-mini";

    /// <summary>Embedding model for memory retrieval (Module 2).</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Total attempts (including the first), for 429/5xx/timeout responses only.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Backoff base: delay before retry N is N * this many seconds. Lowered in tests for speed.</summary>
    public double RetryBaseDelaySeconds { get; set; } = 2.0;
}
