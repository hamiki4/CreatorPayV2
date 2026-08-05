namespace CreatorPay.Application.CustomerVerification;

public static class EthiopianMobileNumber
{
    public const string ValidationMessage = "Enter a valid Ethiopian mobile number, for example 0911234567, 0712345678, or +251911234567.";

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var compact = new string(value.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        if (compact.Length == 10 && compact[0] == '0' && compact[1] is '7' or '9' && compact.All(IsAsciiDigit))
        {
            normalized = "+251" + compact[1..];
            return true;
        }

        if (compact.Length == 13 && compact.StartsWith("+251", StringComparison.Ordinal) && compact[4] is '7' or '9' && compact[4..].All(IsAsciiDigit))
        {
            normalized = compact;
            return true;
        }

        return false;
    }

    public static string Normalize(string? value) => TryNormalize(value, out var normalized)
        ? normalized
        : throw new ArgumentException(ValidationMessage, nameof(value));

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';
}
