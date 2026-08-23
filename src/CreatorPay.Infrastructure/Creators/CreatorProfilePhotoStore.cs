using CreatorPay.Application.Creators;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace CreatorPay.Infrastructure.Creators;

public sealed class CreatorProfilePhotoStore(IConfiguration configuration) : ICreatorProfilePhotoStore
{
    private const int TargetSize = 512;
    private readonly string root = Path.GetFullPath(configuration["CreatorProfilePhotos:RootPath"] ?? "/app/data/profile-photos");

    public async Task<(string StorageKey, long SizeBytes)> SaveAsync(Guid creatorId, Stream content, string contentType, long sizeBytes, CancellationToken ct)
    {
        if (sizeBytes is <= 0 or > ProfilePhotoLimits.MaximumUploadBytes) throw new ArgumentException("Profile photo must be a JPG, PNG, or WEBP image no larger than 12 MB.");
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        if (bytes.Length != sizeBytes) throw new ArgumentException("Profile photo size does not match the uploaded content.");

        var creatorFolder = Path.Combine(root, creatorId.ToString("N"));
        Directory.CreateDirectory(creatorFolder);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(creatorFolder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);

        memory.Position = 0;
        try
        {
            var detectedType = DetectType(bytes);
            if (detectedType is null) throw new ArgumentException("Profile photo must be a JPG, PNG, or WEBP image.");
            var declaredType = NormalizeDeclared(contentType);
            if (declaredType is not null && declaredType != detectedType) throw new ArgumentException("Profile photo content does not match its image type.");
            using (var image = await Image.LoadAsync(memory, ct))
            {
                var extension = detectedType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => throw new ArgumentException("Profile photo must be a JPG, PNG, or WEBP image.")
                };
                var storageKey = $"{creatorId:N}/{Guid.NewGuid():N}{extension}";
                var path = Resolve(storageKey);
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center,
                    Size = new Size(TargetSize, TargetSize)
                }));
                using var output = new MemoryStream();
                switch (detectedType)
                {
                    case "image/jpeg":
                        await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 85 }, ct);
                        break;
                    case "image/png":
                        await image.SaveAsPngAsync(output, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression }, ct);
                        break;
                    case "image/webp":
                        await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = 82 }, ct);
                        break;
                    default:
                        throw new ArgumentException("Profile photo must be a JPG, PNG, or WEBP image.");
                }
                var resized = output.ToArray();
                await File.WriteAllBytesAsync(path, resized, ct);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                return (storageKey, resized.Length);
            }
        }
        catch (UnknownImageFormatException ex)
        {
            throw new ArgumentException("Profile photo must be a JPG, PNG, or WEBP image.", ex);
        }
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct)
    {
        var path = Resolve(storageKey);
        if (!File.Exists(path)) throw new KeyNotFoundException("Profile photo was not found.");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(root, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Invalid creator profile photo path.");
        return path;
    }

    private static string? NormalizeDeclared(string contentType)
    {
        var normalized = contentType.Trim().ToLowerInvariant();
        return normalized is "image/jpeg" or "image/png" or "image/webp" ? normalized : null;
    }

    private static string? DetectType(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

}
