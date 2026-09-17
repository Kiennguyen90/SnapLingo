using System.Net;

namespace SnapLingo.Application.Tests.Fakes;

/// <summary>Returns queued responses in order (repeats the last one once the queue is empty).</summary>
public sealed class StubHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
{
    private int _index;
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        // Capture the body now — OpenAiClient disposes the request right after SendAsync
        // returns, so reading request.Content later (e.g. from a test) would throw ODE.
        RequestBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));

        var factory = responses[Math.Min(_index, responses.Length - 1)];
        _index++;
        return factory(request);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}
