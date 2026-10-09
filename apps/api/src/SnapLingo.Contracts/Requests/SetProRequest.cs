namespace SnapLingo.Contracts.Requests;

/// <summary>
/// Pro toggle: no payment provider is wired up yet, so the client sets this directly (see
/// <c>UserService.SetProAsync</c>). A real purchase flow would set it from a verified
/// payment webhook/receipt instead.
/// </summary>
public sealed record SetProRequest(bool IsPro);
