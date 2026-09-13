namespace SnapLingo.Application.Media;

public sealed class PhotoOptions
{
    public const string SectionName = "Photos";

    /// <summary>Max upload size in bytes. Spec checklist default: 10 MB.</summary>
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp"];
}
