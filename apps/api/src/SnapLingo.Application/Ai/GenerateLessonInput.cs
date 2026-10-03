using SnapLingo.Application.Stories;

namespace SnapLingo.Application.Ai;

/// <summary>A story sentence the verifier rejected, sent back to the generator with the reason
/// so the next attempt can fix or remove it (proposal Figure 1, red loop).</summary>
public sealed record RejectedSentence(string En, string Reason);

/// <summary>Everything an <see cref="ILessonGenerator"/> needs to produce one lesson.</summary>
public sealed record GenerateLessonInput(
    Guid PhotoId,
    string PhotoObjectKey,
    string PhotoMimeType,
    LessonProfile Profile,
    string CefrLevel,
    string? NativeLanguage,
    string? LearningGoal,
    int VocabularyTarget,
    EvidenceBundle Evidence,
    IReadOnlyList<RejectedSentence> Rejected,
    string? LearnerName = null);
