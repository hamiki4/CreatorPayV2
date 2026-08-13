using CreatorPay.Application.CustomerVerification;

namespace CreatorPay.Application.Tests;

public sealed class EthiopianMobileNumberTests
{
    [Theory]
    [InlineData("0911234567", "+251911234567")]
    [InlineData("0712345678", "+251712345678")]
    [InlineData("+251911234567", "+251911234567")]
    [InlineData("+251711234567", "+251711234567")]
    [InlineData("251911234567", "+251911234567")]
    [InlineData("091-123-4567", "+251911234567")]
    [InlineData("+251 911 234 567", "+251911234567")]
    public void Supported_formats_normalize_to_E164(string input, string expected)
        => Assert.Equal(expected, EthiopianMobileNumber.Normalize(input));

    [Theory]
    [InlineData("0911")]
    [InlineData("07123")]
    [InlineData("0812345678")]
    [InlineData("0512345678")]
    [InlineData("1234567890")]
    [InlineData("+251811234567")]
    [InlineData("+25191123456")]
    [InlineData("+2519112345678")]
    [InlineData("(091)1234567")]
    [InlineData("091123456፯")]
    public void Unsupported_formats_are_rejected(string input)
    {
        var error = Assert.Throws<ArgumentException>(() => EthiopianMobileNumber.Normalize(input));
        Assert.Equal(EthiopianMobileNumber.ValidationMessage, error.Message.Split(" (Parameter", StringSplitOptions.None)[0]);
    }

    [Fact]
    public void Local_and_international_equivalents_have_the_same_duplicate_key()
        => Assert.Equal(EthiopianMobileNumber.Normalize("0911234567"), EthiopianMobileNumber.Normalize("+251911234567"));
}
