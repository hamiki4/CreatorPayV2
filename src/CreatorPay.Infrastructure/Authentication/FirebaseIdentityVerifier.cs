using CreatorPay.Application.Authentication;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPay.Infrastructure.Authentication;

public sealed class FirebaseAuthOptions
{
    public const string SectionName = "FirebaseAuth";
    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountPath { get; set; } = string.Empty;
    public string AuthorizedOrigin { get; set; } = "https://pilot.weymela.com";
    public string CallbackPath { get; set; } = "/auth/firebase-action";
    public int PinResetAuthorizationMinutes { get; set; } = 10;
}

public sealed class FirebaseIdentityVerifier : IFirebaseIdentityVerifier, IDisposable
{
    private readonly FirebaseApp? app;
    private readonly ILogger<FirebaseIdentityVerifier> logger;

    public FirebaseIdentityVerifier(IOptions<FirebaseAuthOptions> configured, ILogger<FirebaseIdentityVerifier> logger)
    {
        this.logger = logger;
        var options = configured.Value;
        if (!options.Enabled) return;
        if (string.IsNullOrWhiteSpace(options.ProjectId) || string.IsNullOrWhiteSpace(options.ServiceAccountPath))
            throw new InvalidOperationException("FirebaseAuth is enabled but ProjectId or ServiceAccountPath is missing.");
        if (!Uri.TryCreate(options.AuthorizedOrigin, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps || origin.PathAndQuery != "/")
            throw new InvalidOperationException("FirebaseAuth AuthorizedOrigin must be an HTTPS origin without a path.");
        if (!options.CallbackPath.StartsWith('/') || options.CallbackPath.StartsWith("//", StringComparison.Ordinal) || options.CallbackPath.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("FirebaseAuth CallbackPath must be a local absolute path.");
        app = FirebaseApp.Create(new AppOptions { Credential = CredentialFactory.FromFile<ServiceAccountCredential>(options.ServiceAccountPath).ToGoogleCredential(), ProjectId = options.ProjectId }, $"weymela-{Guid.NewGuid():N}");
    }

    public async Task<Result<FirebaseIdentityProof>> VerifyIdTokenAsync(string idToken, bool checkRevoked, CancellationToken ct)
    {
        if (app is null) return Result<FirebaseIdentityProof>.Failure("Firebase identity verification is not configured.");
        if (string.IsNullOrWhiteSpace(idToken)) return Result<FirebaseIdentityProof>.Failure("Invalid Firebase identity token.");
        try
        {
            var auth = FirebaseAuth.GetAuth(app);
            var token = await auth.VerifyIdTokenAsync(idToken, checkRevoked, ct);
            var firebaseUser = await auth.GetUserAsync(token.Uid, ct);
            if (string.IsNullOrWhiteSpace(firebaseUser.Email)) return Result<FirebaseIdentityProof>.Failure("Firebase identity has no email address.");
            return Result<FirebaseIdentityProof>.Success(new(token.Uid, firebaseUser.Email, firebaseUser.EmailVerified));
        }
        catch (FirebaseAuthException ex)
        {
            logger.LogWarning(ex, "Firebase identity token validation failed; no token or credential content was logged.");
            return Result<FirebaseIdentityProof>.Failure("Invalid Firebase identity token.");
        }
    }

    public void Dispose() => app?.Delete();
}
