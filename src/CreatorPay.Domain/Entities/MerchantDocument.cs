using CreatorPay.Domain.Common;

namespace CreatorPay.Domain.Entities;

public sealed class MerchantDocument : Entity
{
    public Guid MerchantId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageProvider { get; set; } = "MetadataOnly";
    public string? StorageKey { get; set; }
}
