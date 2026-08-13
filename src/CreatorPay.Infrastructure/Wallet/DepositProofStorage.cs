using CreatorPay.Application.Wallet;
using Microsoft.Extensions.Configuration;

namespace CreatorPay.Infrastructure.Wallet;

public sealed class DepositProofStorage(IConfiguration configuration) : IDepositProofStorage
{
    public const long MaximumBytes = 5 * 1024 * 1024;
    private readonly string root = Path.GetFullPath(configuration["DepositProofStorage:RootPath"] ?? "/app/data/deposit-proofs");

    public async Task<DepositProofDescriptor> SaveAsync(Guid merchantId, Stream content, string fileName, string contentType, long sizeBytes, CancellationToken ct)
    {
        if (sizeBytes is <= 0 or > MaximumBytes) throw new ArgumentException("Payment proof must be an image no larger than 5 MB.");
        var extension = contentType.ToLowerInvariant() switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => throw new ArgumentException("Payment proof must be a JPG, PNG, or WEBP image.") };
        var header = new byte[12]; var read = await content.ReadAsync(header.AsMemory(), ct); content.Position = 0;
        var valid = contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ? read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff
            : contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            : read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        if (!valid) throw new ArgumentException("Payment proof content does not match its image type.");
        var merchantFolder = Path.Combine(root, merchantId.ToString("N")); Directory.CreateDirectory(merchantFolder);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(merchantFolder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
        var key = $"{merchantId:N}/{Guid.NewGuid():N}{extension}"; var path = Resolve(key);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await content.CopyToAsync(output, ct);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        return new(key, Path.GetFileName(fileName), contentType.ToLowerInvariant(), sizeBytes);
    }

    public Task<(Stream Content, string ContentType, string FileName)> OpenAsync(string storageKey, CancellationToken ct)
    {
        var path = Resolve(storageKey); if (!File.Exists(path)) throw new KeyNotFoundException("Payment proof was not found.");
        var type = Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => throw new InvalidOperationException("Stored payment proof type is invalid.") };
        return Task.FromResult<(Stream, string, string)>((new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous), type, "payment-proof" + Path.GetExtension(path)));
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Invalid payment proof path.");
        return path;
    }
}
