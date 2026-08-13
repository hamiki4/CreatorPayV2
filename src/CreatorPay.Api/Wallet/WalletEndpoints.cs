using CreatorPay.Application.Authentication;
using CreatorPay.Application.Wallet;
using CreatorPay.Infrastructure.Wallet;
using System.Globalization;
using System.Text.Json;

namespace CreatorPay.Api.Wallet;

public static class WalletEndpoints
{
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder e)
    {
        var m = e.MapGroup("/api/v1/merchant").RequireAuthorization("MerchantAdminOnly");
        m.MapGet("/wallet", async (ICurrentUserService u, IWalletService s, CancellationToken c) => Results.Ok(await s.GetWalletAsync(u.MerchantId!.Value, c)));
        m.MapGet("/wallet/entries", async (ICurrentUserService u, IWalletService s, CancellationToken c) => Results.Ok(await s.GetEntriesAsync(u.MerchantId!.Value, c)));
        m.MapGet("/deposits", (ICurrentUserService u, IWalletService s, CancellationToken c) => Run(() => s.GetDepositsAsync(u.MerchantId, false, c)));
        m.MapPost("/deposits", SubmitWithProof).DisableAntiforgery();
        m.MapGet("/deposits/{id:guid}", (Guid id, ICurrentUserService u, IWalletService s, CancellationToken c) => Run(() => s.GetDepositAsync(id, u.MerchantId, c)));
        m.MapGet("/deposits/{id:guid}/proof", (Guid id, ICurrentUserService u, IWalletService s, IDepositProofStorage storage, CancellationToken c) => Proof(id, u.MerchantId, s, storage, c));
        m.MapGet("/confirmed-sales", (string? search, Guid? creatorId, Guid? cashierId, DateTime? dateFromUtc, DateTime? dateToUtc, string? status, int? page, int? pageSize, string? creatorName, string? cashierName, ICurrentUserService u, IWalletService s, CancellationToken c) => Run(() => s.GetConfirmedSalesAsync(u.MerchantId!.Value, new(search, creatorId, cashierId, dateFromUtc, dateToUtc, status, page ?? 1, pageSize ?? 100, creatorName, cashierName), c)));
        m.MapGet("/purchases", (ICurrentUserService u, IWalletService s, CancellationToken c) => Run(() => s.GetPurchasesAsync(u.MerchantId, null, 100, c)));
        m.MapGet("/purchases/{id:guid}", (Guid id, ICurrentUserService u, IWalletService s, CancellationToken c) => Run(() => s.GetPurchaseAsync(id, u.MerchantId, null, c)));
        var a = e.MapGroup("/api/v1/admin").RequireAuthorization("PlatformAdminOnly");
        a.MapGet("/deposits", (IWalletService s, CancellationToken c) => Run(() => s.GetDepositsAsync(null, false, c)));
        a.MapGet("/deposits/pending", (IWalletService s, CancellationToken c) => Run(() => s.GetDepositsAsync(null, true, c)));
        a.MapGet("/deposits/{id:guid}", (Guid id, IWalletService s, CancellationToken c) => Run(() => s.GetDepositAsync(id, null, c)));
        a.MapGet("/deposits/{id:guid}/proof", (Guid id, IWalletService s, IDepositProofStorage storage, CancellationToken c) => Proof(id, null, s, storage, c));
        a.MapPost("/deposits/{id:guid}/approve", (Guid id, HttpRequest r, ICurrentUserService u, IWalletService s, CancellationToken c) => Run(() => s.ApproveDepositAsync(id, u.UserAccountId!.Value, Key(r), c)));
        a.MapPost("/deposits/{id:guid}/reject", (Guid id, RejectDepositRequest x, ICurrentUserService u, IWalletService s, CancellationToken c) => Run(() => s.RejectDepositAsync(id, u.UserAccountId!.Value, x, c)));
        a.MapGet("/purchases", (IWalletService s, CancellationToken c) => Run(() => s.GetPurchasesAsync(null, null, 100, c)));
        a.MapGet("/purchases/{id:guid}", (Guid id, IWalletService s, CancellationToken c) => Run(() => s.GetPurchaseAsync(id, null, null, c)));
        var p = e.MapGroup("/api/v1/cashier/purchases").RequireAuthorization("CashierOnly");
        p.MapGet("/recent", (ICurrentUserService u, IWalletService s, CancellationToken c) => Run(() => s.GetPurchasesAsync(u.MerchantId, u.CashierId, 25, c)));
        p.MapGet("/{id:guid}", (Guid id, ICurrentUserService u, IWalletService s, CancellationToken c) => Run(() => s.GetPurchaseAsync(id, u.MerchantId, u.CashierId, c)));
        return e;
    }
    static string Key(HttpRequest r) => r.Headers["Idempotency-Key"].ToString();
    static async Task<IResult> SubmitWithProof(HttpRequest request, ICurrentUserService user, IWalletService service, IDepositProofStorage storage, CancellationToken ct)
    {
        try
        {
            var key = Key(request); if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A valid Idempotency-Key header is required.");
            var existing = (await service.GetDepositsAsync(user.MerchantId, false, ct)).FirstOrDefault(x => x.ExternalReference == key);
            if (existing is not null) return Results.Ok(existing);
            var form = await request.ReadFormAsync(ct); if (!decimal.TryParse(form["amount"], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) throw new ArgumentException("Deposit amount is invalid.");
            var file = form.Files.GetFile("proof") ?? throw new ArgumentException("Payment proof is required.");
            await using var memory = new MemoryStream(); await file.CopyToAsync(memory, ct); memory.Position = 0;
            var proof = await storage.SaveAsync(user.MerchantId!.Value, memory, file.FileName, file.ContentType, file.Length, ct);
            return Results.Json(await service.SubmitDepositAsync(user.MerchantId.Value, user.UserAccountId!.Value, key, new(amount, "ETB", key, JsonSerializer.Serialize(proof)), ct), statusCode: 201);
        }
        catch (Exception x) { return Error(x); }
    }
    static async Task<IResult> Proof(Guid id, Guid? merchantId, IWalletService service, IDepositProofStorage storage, CancellationToken ct)
    {
        try { var deposit = await service.GetDepositAsync(id, merchantId, ct); var descriptor = JsonSerializer.Deserialize<DepositProofDescriptor>(deposit.ProofMetadata ?? "") ?? throw new KeyNotFoundException("Payment proof was not found."); var proof = await storage.OpenAsync(descriptor.StorageKey, ct); return Results.File(proof.Content, proof.ContentType, descriptor.FileName, enableRangeProcessing: false); }
        catch (Exception x) { return Error(x); }
    }
    static async Task<IResult> Run<T>(Func<Task<T>> f, int status = 200) { try { var value = await f(); return status == 201 ? Results.Json(value, statusCode: 201) : Results.Ok(value); } catch (Exception x) { return Error(x); } }
    static IResult Error(Exception x) => x switch { IdempotencyConflictException => Results.Problem(statusCode: 409, title: "Idempotency conflict", detail: x.Message), PurchaseRejectedException p => Results.Problem(statusCode: 422, title: p.Code, detail: p.Message), ArgumentException => Results.Problem(statusCode: 400, title: "Invalid request", detail: x.Message), KeyNotFoundException => Results.Problem(statusCode: 404, title: "Not found", detail: x.Message), UnauthorizedAccessException => Results.Problem(statusCode: 403, title: "Forbidden", detail: "Access to this payment proof is not allowed."), _ => Results.Problem(statusCode: 409, title: "Operation rejected", detail: "This deposit operation is temporarily unavailable.") };
}
