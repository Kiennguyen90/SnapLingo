using SnapLingo.Application.Auth;
using SnapLingo.Application.Common;

namespace SnapLingo.Application.Tests.Fakes;

/// <summary>Test double for <see cref="IGoogleIdTokenValidator"/>: treats the "idToken" string as
/// an opaque lookup key into a pre-seeded table of tokens -> Google identities, so tests can
/// exercise <c>AuthService.GoogleLoginAsync</c> without a real Google credential or network call.</summary>
public sealed class FakeGoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly Dictionary<string, GoogleUserInfo> _tokens = [];

    public FakeGoogleIdTokenValidator Register(string idToken, GoogleUserInfo info)
    {
        _tokens[idToken] = info;
        return this;
    }

    public Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct) =>
        _tokens.TryGetValue(idToken, out var info)
            ? Task.FromResult(info)
            : throw new AuthenticationException("Invalid Google credential.");
}
