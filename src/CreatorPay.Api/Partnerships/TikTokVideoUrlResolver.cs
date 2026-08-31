using System.Net;
using System.Text.RegularExpressions;

namespace CreatorPay.Api.Partnerships;

public sealed record TikTokVideoUrlResult(bool Accepted, string? VideoUrl, string Error)
{
    public static TikTokVideoUrlResult Valid(string videoUrl) => new(true, videoUrl, string.Empty);
    public static TikTokVideoUrlResult Invalid(string error) => new(false, null, error);
}

public interface ITikTokVideoUrlResolver
{
    Task<TikTokVideoUrlResult> ResolveAsync(string? value, CancellationToken cancellationToken);
}

public sealed partial class TikTokVideoUrlResolver(HttpClient httpClient) : ITikTokVideoUrlResolver
{
    public const string InvalidVideoMessage = "Enter a TikTok video link, not a profile link.";
    public const string UnverifiedShortLinkMessage = "We couldn't verify this TikTok video link. Please copy the link again from the TikTok video.";
    private const int MaximumUrlLength = 1000;
    private const int MaximumRedirects = 4;
    private static readonly TimeSpan ResolutionTimeout = TimeSpan.FromSeconds(6);

    private static readonly HashSet<string> CanonicalHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "tiktok.com",
        "www.tiktok.com"
    };

    private static readonly HashSet<string> ShortLinkHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "tiktok.com",
        "www.tiktok.com",
        "vm.tiktok.com",
        "vt.tiktok.com"
    };

    public async Task<TikTokVideoUrlResult> ResolveAsync(string? value, CancellationToken cancellationToken)
    {
        if (!TryParse(value, out var uri, out var kind)) return TikTokVideoUrlResult.Invalid(InvalidVideoMessage);
        if (kind == TikTokUrlKind.CanonicalVideo) return TikTokVideoUrlResult.Valid(Normalize(uri!));

        var current = uri!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ResolutionTimeout);
        for (var redirect = 0; redirect < MaximumRedirects; redirect++)
        {
            try
            {
                using var response = await SendHeadersOnlyAsync(current, timeout.Token);
                if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
                    return TikTokVideoUrlResult.Invalid(UnverifiedShortLinkMessage);

                var next = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                if (!TryParse(next.AbsoluteUri, out var parsed, out var nextKind))
                    return TikTokVideoUrlResult.Invalid(UnverifiedShortLinkMessage);
                if (nextKind == TikTokUrlKind.CanonicalVideo)
                    return TikTokVideoUrlResult.Valid(Normalize(parsed!));
                current = parsed!;
            }
            catch (HttpRequestException)
            {
                return TikTokVideoUrlResult.Invalid(UnverifiedShortLinkMessage);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return TikTokVideoUrlResult.Invalid(UnverifiedShortLinkMessage);
            }
        }

        return TikTokVideoUrlResult.Invalid(UnverifiedShortLinkMessage);
    }

    private async Task<HttpResponseMessage> SendHeadersOnlyAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var head = new HttpRequestMessage(HttpMethod.Head, uri);
        var response = await httpClient.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)) return response;
        response.Dispose();
        using var get = new HttpRequestMessage(HttpMethod.Get, uri);
        return await httpClient.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static bool TryParse(string? value, out Uri? uri, out TikTokUrlKind kind)
    {
        uri = null;
        kind = TikTokUrlKind.Invalid;
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaximumUrlLength ||
            !Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps || !parsed.IsDefaultPort ||
            !string.IsNullOrEmpty(parsed.UserInfo)) return false;

        var host = parsed.IdnHost;
        if (CanonicalHosts.Contains(host) && CanonicalVideoPath().IsMatch(parsed.AbsolutePath))
        {
            uri = parsed;
            kind = TikTokUrlKind.CanonicalVideo;
            return true;
        }

        var shortPath = host is "tiktok.com" or "www.tiktok.com"
            ? TikTokTPath().IsMatch(parsed.AbsolutePath)
            : host is "vm.tiktok.com" or "vt.tiktok.com" && TikTokHostShortPath().IsMatch(parsed.AbsolutePath);
        if (!shortPath || !ShortLinkHosts.Contains(host)) return false;
        uri = parsed;
        kind = TikTokUrlKind.ShortLink;
        return true;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static string Normalize(Uri uri) => uri.GetLeftPart(UriPartial.Path) + uri.Query;

    private enum TikTokUrlKind { Invalid, CanonicalVideo, ShortLink }

    [GeneratedRegex(@"^/@[A-Za-z0-9._-]+/video/\d+/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalVideoPath();

    [GeneratedRegex(@"^/t/[A-Za-z0-9_-]+/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TikTokTPath();

    [GeneratedRegex(@"^/[A-Za-z0-9_-]+/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TikTokHostShortPath();
}
