using CreatorPay.Application.Authentication;
using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Tests;

public sealed class PasswordPolicyValidatorTests
{
    private readonly PasswordPolicyValidator _validator = new(Options.Create(new PasswordOptions()));
    [Theory]
    [InlineData("Secure1!")]
    [InlineData("SecurePass1!")]
    public void Strong_password_is_accepted(string password) => Assert.Empty(_validator.Validate(password));
    [Theory]
    [InlineData("Sh0rt!", "at least 8")]
    [InlineData("alllowercase1!", "uppercase")]
    [InlineData("ALLUPPERCASE1!", "lowercase")]
    [InlineData("NoDigitsHere!", "number")]
    [InlineData("NoSymbols123A", "non-alphanumeric")]
    public void Weak_password_is_rejected_with_specific_rule(string password, string expected) => Assert.Contains(_validator.Validate(password), x => x.Contains(expected, StringComparison.OrdinalIgnoreCase));
    [Fact] public void Password_over_128_characters_is_rejected() => Assert.Contains(_validator.Validate($"Aa1!{new string('x', 125)}"), x => x.Contains("128", StringComparison.Ordinal));
}
