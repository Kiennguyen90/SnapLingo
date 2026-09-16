namespace SnapLingo.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? DisplayName { get; set; }
    public string CefrLevel { get; set; } = "A2";
    public string? NativeLanguage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
