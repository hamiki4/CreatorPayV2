using System.Text;
using CreatorPay.Infrastructure.Creators;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CreatorPay.Api.IntegrationTests;

public sealed class CreatorProfilePhotoStoreTests
{
    [Fact]
    public async Task Saves_and_reopens_a_valid_png()
    {
        var root = Path.Combine(Path.GetTempPath(), $"creatorpay-profile-photos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new CreatorProfilePhotoStore(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CreatorProfilePhotos:RootPath"] = root
            }).Build());

            using var image = new Image<Rgba32>(1, 1);
            image[0, 0] = new Rgba32(255, 0, 0);
            await using var stream = new MemoryStream();
            await image.SaveAsPngAsync(stream);
            stream.Position = 0;
            var saved = await store.SaveAsync(Guid.NewGuid(), stream, "image/png", stream.Length, CancellationToken.None);
            Assert.False(string.IsNullOrWhiteSpace(saved.StorageKey));
            Assert.True(saved.SizeBytes > 0);

            await using var reopened = await store.OpenAsync(saved.StorageKey, CancellationToken.None);
            var buffer = new byte[8];
            var read = await reopened.ReadAsync(buffer, CancellationToken.None);
            Assert.Equal(8, read);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, buffer);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Rejects_invalid_photo_content_and_path_traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"creatorpay-profile-photos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new CreatorProfilePhotoStore(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CreatorProfilePhotos:RootPath"] = root
            }).Build());

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not an image"));
                await store.SaveAsync(Guid.NewGuid(), stream, "image/png", stream.Length, CancellationToken.None);
            });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await store.OpenAsync("../escape", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
