using System.Net;
using CreatorPay.Api.Partnerships;

namespace CreatorPay.Api.IntegrationTests;

public sealed class TikTokVideoUrlResolverTests
{
    private const string Canonical = "https://www.tiktok.com/@bealti_shekuar/video/7679446711865036039?_r=1&_t=ZT-99JmDTkTzAM";

    [Theory]
    [InlineData("https://www.tiktok.com/@creator/video/1234567890123456789", "https://www.tiktok.com/@creator/video/1234567890123456789")]
    [InlineData("  https://www.tiktok.com/@creator/video/1234567890123456789?lang=en&utm_source=copy  ", "https://www.tiktok.com/@creator/video/1234567890123456789?lang=en&utm_source=copy")]
    public async Task Canonical_video_links_are_accepted_without_network_access(string value, string expected)
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Canonical links must not use the network."));
        var result = await Resolver(handler).ResolveAsync(value, default);
        Assert.True(result.Accepted);
        Assert.Equal(expected, result.VideoUrl);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("https://www.tiktok.com/t/ZTD3GnwFT/")]
    [InlineData("https://www.tiktok.com/t/ZTD3GnwFT/?share_app_id=1233")]
    [InlineData("https://vm.tiktok.com/ZM123abc_/")]
    [InlineData("https://vt.tiktok.com/ZS123abc-/")]
    public async Task Official_short_share_links_resolve_to_the_exact_canonical_post(string value)
    {
        var handler = new StubHandler(_ => Redirect(Canonical));
        var result = await Resolver(handler).ResolveAsync(value, default);
        Assert.True(result.Accepted);
        Assert.Equal(Canonical, result.VideoUrl);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(HttpMethod.Head, handler.LastMethod);
    }

    [Theory]
    [InlineData("https://www.tiktok.com/@creator")]
    [InlineData("https://www.tiktok.com/@creator/")]
    [InlineData("https://www.tiktok.com/")]
    [InlineData("https://example.com/@creator/video/123456789")]
    [InlineData("https://tiktok.com.example.com/@creator/video/123456789")]
    [InlineData("not a url")]
    [InlineData("http://www.tiktok.com/@creator/video/123456789")]
    [InlineData("ftp://www.tiktok.com/@creator/video/123456789")]
    [InlineData("https://evil.tiktok.com/@creator/video/123456789")]
    [InlineData("https://www.tiktok.com:444/@creator/video/123456789")]
    public async Task Profiles_spoofed_hosts_and_unsupported_urls_are_rejected(string value)
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Invalid links must not use the network."));
        var result = await Resolver(handler).ResolveAsync(value, default);
        Assert.False(result.Accepted);
        Assert.Equal(TikTokVideoUrlResolver.InvalidVideoMessage, result.Error);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Short_link_redirecting_outside_allowed_tiktok_destinations_is_rejected()
    {
        var handler = new StubHandler(_ => Redirect("https://example.com/video/7679446711865036039"));
        var result = await Resolver(handler).ResolveAsync("https://vm.tiktok.com/ZM123abc/", default);
        Assert.False(result.Accepted);
        Assert.Equal(TikTokVideoUrlResolver.UnverifiedShortLinkMessage, result.Error);
    }

    [Fact]
    public async Task Short_link_redirect_count_is_limited()
    {
        var handler = new StubHandler(_ => Redirect("https://vt.tiktok.com/ZS123abc/"));
        var result = await Resolver(handler).ResolveAsync("https://vm.tiktok.com/ZM123abc/", default);
        Assert.False(result.Accepted);
        Assert.Equal(4, handler.RequestCount);
    }

    private static TikTokVideoUrlResolver Resolver(HttpMessageHandler handler) => new(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(1) });

    private static HttpResponseMessage Redirect(string location) => new(HttpStatusCode.MovedPermanently)
    {
        Headers = { Location = new Uri(location) }
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastMethod = request.Method;
            return Task.FromResult(response(request));
        }
    }
}
