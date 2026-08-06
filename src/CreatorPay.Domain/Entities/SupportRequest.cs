using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class SupportRequest : Entity
{
    public string PublicReference { get; set; } = "";
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string UserType { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public string PreferredLanguage { get; set; } = "en";
    public bool ConsentAcknowledged { get; set; }
    public string Status { get; set; } = "Open";
}
