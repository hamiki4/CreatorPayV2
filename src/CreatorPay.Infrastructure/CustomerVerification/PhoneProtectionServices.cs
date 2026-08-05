using System.Security.Cryptography;
using System.Text;
using CreatorPay.Application.CustomerVerification;
using Microsoft.Extensions.Options;
namespace CreatorPay.Infrastructure.CustomerVerification;

public sealed class EthiopianPhoneNumberNormalizer : IPhoneNumberNormalizer
{
    public string Normalize(string value) => EthiopianMobileNumber.Normalize(value);
    public string Mask(string n) => $"+251 {n[4]}** *** {n[^3..]}";
}
public sealed class PhoneHashService(IOptions<CustomerVerificationOptions> o) : IPhoneHashService { readonly byte[] key = Encoding.UTF8.GetBytes(Require(o.Value.HmacSecret, "HMAC secret", 32)); public string Hash(string n) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(n))); static string Require(string x, string name, int min) { if (x.Length < min) throw new InvalidOperationException($"Customer verification {name} must be at least {min} characters."); return x; } }
public sealed class PhoneEncryptionService(IOptions<CustomerVerificationOptions> o) : IPhoneEncryptionService { readonly byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(Require(o.Value.EncryptionKey))); public byte[] Encrypt(string value) { var nonce = RandomNumberGenerator.GetBytes(12); var plain = Encoding.UTF8.GetBytes(value); var cipher = new byte[plain.Length]; var tag = new byte[16]; using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag); return [.. nonce, .. tag, .. cipher]; } public string Decrypt(byte[] value) { var plain = new byte[value.Length - 28]; using var aes = new AesGcm(key, 16); aes.Decrypt(value[..12], value[28..], value[12..28], plain); return Encoding.UTF8.GetString(plain); } static string Require(string x) { if (x.Length < 32) throw new InvalidOperationException("Customer verification encryption key must be at least 32 characters."); return x; } }
public sealed class DevelopmentCustomerVerificationProvider(IOptions<CustomerVerificationOptions> o) : ICustomerVerificationProvider { readonly byte[] key = Encoding.UTF8.GetBytes(o.Value.HmacSecret); public (string Code, string Hash) CreateCode() { var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"); return (code, Hash(code)); } public bool Verify(string code, string hash) => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Hash(code)), Convert.FromHexString(hash)); string Hash(string code) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("otp:" + code))); }
