using CreatorPay.Application.CustomerVerification;
using CreatorPay.Infrastructure.CustomerVerification;
using Microsoft.Extensions.Options;
namespace CreatorPay.Infrastructure.Tests;

public sealed class PhoneProtectionTests
{
    readonly EthiopianPhoneNumberNormalizer normalizer = new();
    [Theory][InlineData("0912345678", "+251912345678")][InlineData("0712345678", "+251712345678")][InlineData("+251912345678", "+251912345678")][InlineData("251912345678", "+251912345678")][InlineData("+251712345678", "+251712345678")] public void Ethiopian_formats_normalize(string input, string expected) => Assert.Equal(expected, normalizer.Normalize(input));
    [Theory][InlineData("")][InlineData("0812345678")][InlineData("+2519123")][InlineData("+12025550123")][InlineData("091234567A")] public void Invalid_numbers_are_rejected(string input) => Assert.Throws<ArgumentException>(() => normalizer.Normalize(input));
    [Fact] public void Mask_never_contains_full_number() => Assert.Equal("+251 9** *** 678", normalizer.Mask(normalizer.Normalize("0912345678")));
    [Fact] public void Safaricom_mask_identifies_the_prefix_without_exposing_the_number() => Assert.Equal("+251 7** *** 678", normalizer.Mask(normalizer.Normalize("0712345678")));
    [Fact] public void Hmac_is_deterministic_and_secret_keyed() { var s = new PhoneHashService(Options.Create(new CustomerVerificationOptions { HmacSecret = new string('h', 32) })); var a = s.Hash("+251912345678"); Assert.Equal(a, s.Hash("+251912345678")); Assert.NotEqual(a, s.Hash("+251912345679")); }
    [Fact] public void Encryption_is_authenticated_and_recoverable() { var s = new PhoneEncryptionService(Options.Create(new CustomerVerificationOptions { EncryptionKey = new string('e', 32) })); var encrypted = s.Encrypt("+251912345678"); Assert.DoesNotContain("251912345678", Convert.ToBase64String(encrypted)); Assert.Equal("+251912345678", s.Decrypt(encrypted)); encrypted[^1] ^= 1; Assert.ThrowsAny<Exception>(() => s.Decrypt(encrypted)); }
    [Fact] public void Otp_is_hashed_and_single_comparison_is_supported() { var s = new DevelopmentCustomerVerificationProvider(Options.Create(new CustomerVerificationOptions { HmacSecret = new string('h', 32) })); var (code, hash) = s.CreateCode(); Assert.DoesNotContain(code, hash); Assert.True(s.Verify(code, hash)); Assert.False(s.Verify(code == "000000" ? "000001" : "000000", hash)); }
}
