using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Authentication;

public sealed class PasswordPolicyValidator(IOptions<PasswordOptions> options)
{
    private static readonly HashSet<string> Weak = new(StringComparer.OrdinalIgnoreCase) { "password", "password123!", "qwerty123!", "letmein123!", "creatorpay1!" };
    public IReadOnlyList<string> Validate(string password)
    {
        var o = options.Value; var errors = new List<string>();
        if (password.Length < o.MinimumLength) errors.Add($"Password must be at least {o.MinimumLength} characters.");
        if (password.Length > o.MaximumLength) errors.Add($"Password must not exceed {o.MaximumLength} characters.");
        if (o.RequireUppercase && !password.Any(char.IsUpper)) errors.Add("Password must contain an uppercase letter.");
        if (o.RequireLowercase && !password.Any(char.IsLower)) errors.Add("Password must contain a lowercase letter.");
        if (o.RequireDigit && !password.Any(char.IsDigit)) errors.Add("Password must contain a number.");
        if (o.RequireNonAlphanumeric && !password.Any(c => !char.IsLetterOrDigit(c))) errors.Add("Password must contain a non-alphanumeric character.");
        if (Weak.Contains(password)) errors.Add("Password is too common.");
        return errors;
    }
}
