using CreatorPay.Application.CustomerVerification;
using CreatorPay.Infrastructure.CustomerVerification;
using Microsoft.Extensions.Options;
namespace CreatorPay.Infrastructure.Tests;

public sealed class PhoneProtectionTests
{
    readonly EthiopianPhoneNumberNormalizer normalizer = new();
    [Theory][InlineData("0912345678")][InlineData("+251912345678")][InlineData("251912345678")] public void Ethiopian_formats_normalize(string input) => Assert.Equal("+251912345678", normalizer.Normalize(input));
    [Theory][InlineData("")][InlineData("0812345678")][InlineData("+2519123")][InlineData("+12025550123")][InlineData("091234567A")] public void Invalid_numbers_are_rejected(string input) => Assert.Throws<ArgumentException>(() => normalizer.Normalize(input));
    [Fact] public void Mask_never_contains_full_number() => Assert.Equal("+251 9** *** 678", normalizer.Mask(normalizer.Normalize("0912345678")));
    [Fact] public void Hmac_is_deterministic_and_secret_keyed() { var s = new PhoneHashService(Options.Create(new CustomerVerificationOptions { HmacSecret = "test-secret-that-is-at-least-32-characters" })); var a = s.Hash("+251912345678"); Assert.Equal(a, s.Hash("+251912345678")); Assert.NotEqual(a, s.Hash("+251912345679")); }
    [Fact] public void Encryption_is_authenticated_and_recoverable() { var s = new PhoneEncryptionService(Options.Create(new CustomerVerificationOptions { EncryptionKey = "test-encryption-key-at-least-32-characters" })); var encrypted = s.Encrypt("+251912345678"); Assert.DoesNotContain("251912345678", Convert.ToBase64String(encrypted)); Assert.Equal("+251912345678", s.Decrypt(encrypted)); encrypted[^1] ^= 1; Assert.ThrowsAny<Exception>(() => s.Decrypt(encrypted)); }
    [Fact] public void Otp_is_hashed_and_single_comparison_is_supported() { var s = new DevelopmentCustomerVerificationProvider(Options.Create(new CustomerVerificationOptions { HmacSecret = "test-secret-that-is-at-least-32-characters" })); var (code, hash) = s.CreateCode(); Assert.DoesNotContain(code, hash); Assert.True(s.Verify(code, hash)); Assert.False(s.Verify(code == "000000" ? "000001" : "000000", hash)); }
}
