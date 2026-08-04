namespace CreatorPay.Application.CustomerVerification;

// Retained only to validate secrets needed to read historical Milestone 13 records.
// Milestone 20.1 checkout does not use phone/OTP repeat-use approval.
public sealed class CustomerVerificationOptions
{
    public const string SectionName = "CustomerVerification";
    public string HmacSecret { get; set; } = "";
    public string EncryptionKey { get; set; } = "";
}

public interface IPhoneNumberNormalizer { string Normalize(string value); string Mask(string normalized); }
public interface IPhoneHashService { string Hash(string normalized); }
public interface IPhoneEncryptionService { byte[] Encrypt(string normalized); string Decrypt(byte[] protectedValue); }
public interface ICustomerVerificationProvider { (string Code, string Hash) CreateCode(); bool Verify(string code, string hash); }
