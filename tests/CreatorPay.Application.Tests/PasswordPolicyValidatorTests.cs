using CreatorPay.Application.Authentication;
using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Tests;

public sealed class PasswordPolicyValidatorTests
{
    private readonly PasswordPolicyValidator _validator = new(Options.Create(new PasswordOptions()));
    [Fact] public void Strong_password_is_accepted() => Assert.Empty(_validator.Validate("SecurePass1!"));
    [Theory]
    [InlineData("short1A!")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSymbols123A")]
    public void Weak_password_is_rejected(string password) => Assert.NotEmpty(_validator.Validate(password));
}
