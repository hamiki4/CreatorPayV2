using System.Net.Http.Json;
using CreatorPay.Application.Integration;

namespace CreatorPay.Infrastructure.Integration;

public sealed class V3AuthorityClient(HttpClient http, V3IntegrationOptions options) : IV3AuthorityClient
{
    public Task<V3IdentityAssertion> RedeemAsync(string code, string callbackId, CancellationToken ct) =>
        SendAsync<V3IdentityAssertion>("/api/integration/product/server/redeem", new { code, callbackId }, ct);
    public Task<V3AuthorityResult> RevalidateAsync(V3AuthorityRequest request, CancellationToken ct) =>
        SendAsync<V3AuthorityResult>("/api/integration/product/server/authority", request, ct);
    public Task<V3ProfileSynchronizationResult> SynchronizeAsync(V3ProfileSynchronizationRequest request,
        CancellationToken ct) => SendAsync<V3ProfileSynchronizationResult>(
            "/api/integration/product/server/profiles/synchronize", request, ct);

    private async Task<T> SendAsync<T>(string path, object body, CancellationToken ct)
    {
        if (!options.Enabled) throw new InvalidOperationException("V3 product integration is disabled.");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Weymela-Request", "1");
        request.Headers.Add("X-Weymela-Integration-Client", options.ClientId);
        request.Headers.Add("X-Weymela-Integration-Secret", options.ClientSecret);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("V3 product authority rejected the request.");
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException("V3 product authority returned an invalid response.");
    }
}
