namespace SnapLingo.Application.Audio;

public sealed class AudioOptions
{
    public const string SectionName = "Audio";

    /// <summary>Default TTS voice name. Kept in config, never hard-coded (spec checklist item).</summary>
    public string DefaultVoice { get; set; } = "alloy";
}
