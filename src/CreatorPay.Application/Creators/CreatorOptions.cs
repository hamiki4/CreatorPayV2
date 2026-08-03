namespace CreatorPay.Application.Creators;

public sealed class CreatorVerificationOptions
{
    public const string SectionName = "CreatorVerification";
    public int TokenLifetimeMinutes { get; set; } = 30;
}
