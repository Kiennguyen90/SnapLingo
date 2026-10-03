namespace SnapLingo.Application.Audio;

public sealed class AudioOptions
{
    public const string SectionName = "Audio";

    /// <summary>Default TTS voice name. Kept in config, never hard-coded (spec checklist item).</summary>
    public string DefaultVoice { get; set; } = "alloy";

    /// <summary>Voice used to narrate the Vietnamese story text (see section 11.1's bilingual audio note).</summary>
    public string VietnameseVoice { get; set; } = "alloy";
}
