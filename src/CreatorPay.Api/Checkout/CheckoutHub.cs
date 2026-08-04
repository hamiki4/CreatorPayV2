using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace CreatorPay.Api.Checkout;

[Authorize] public sealed class CheckoutHub : Hub { public override async Task OnConnectedAsync() { var customer = Context.User?.FindFirst("customer_id")?.Value; if (customer != null) await Groups.AddToGroupAsync(Context.ConnectionId, $"customer:{customer}"); var merchant = Context.User?.FindFirst("merchant_id")?.Value; if (merchant != null) await Groups.AddToGroupAsync(Context.ConnectionId, $"merchant:{merchant}"); await base.OnConnectedAsync(); } }
