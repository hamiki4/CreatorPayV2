using System.Security.Claims;
using CreatorPay.Application.Authentication;

namespace CreatorPay.Api.Authentication;

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    public Guid? UserAccountId => Parse(ClaimTypes.NameIdentifier); public string? Role => User?.FindFirstValue(ClaimTypes.Role);
    public Guid? MerchantId => Parse("merchant_id"); public Guid? CreatorId => Parse("creator_id"); public Guid? SupervisorId => Parse("supervisor_id"); public Guid? CashierId => Parse("cashier_id");
    private Guid? Parse(string type) => Guid.TryParse(User?.FindFirstValue(type), out var value) ? value : null;
}
