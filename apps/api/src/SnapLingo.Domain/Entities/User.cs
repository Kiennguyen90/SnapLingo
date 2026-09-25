namespace SnapLingo.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }

    /// <summary>Google account `sub` claim, set once the user has signed in with Google at least
    /// once. Null for accounts that have only ever used email/password.</summary>
    public string? GoogleId { get; set; }

    public string? DisplayName { get; set; }
    public string CefrLevel { get; set; } = "A2";
    public string? NativeLanguage { get; set; }

    /// <summary>Opt-in: the learner may label people in their photos (proposal section 3.4). Off by default.</summary>
    public bool PersonLabelingEnabled { get; set; }

    /// <summary>Set when the learner agrees that their in-app usage data may be used for the
    /// study (informed consent, proposal section 3.4). Null means not consented.</summary>
    public DateTimeOffset? ResearchConsentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
