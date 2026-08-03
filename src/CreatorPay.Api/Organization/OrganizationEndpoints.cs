using CreatorPay.Api.Authentication;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Organization;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Api.Organization;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var merchant=endpoints.MapGroup("/api/v1/merchant").WithTags("Merchant organization").RequireAuthorization("MerchantAdminOnly");
        merchant.MapGet("/locations",async(ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.GetLocationsAsync(Merchant(u),false,ct)));
        merchant.MapPost("/locations",async(LocationRequest r,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.CreateLocationAsync(Merchant(u),Actor(u),r,ct)));
        merchant.MapGet("/locations/{locationId:guid}",async(Guid locationId,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.GetLocationAsync(Merchant(u),locationId,ct)));
        merchant.MapPut("/locations/{locationId:guid}",async(Guid locationId,LocationRequest r,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.UpdateLocationAsync(Merchant(u),Actor(u),locationId,r,ct)));
        merchant.MapPatch("/locations/{locationId:guid}/status",async(Guid locationId,StatusRequest r,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.SetLocationStatusAsync(Merchant(u),Actor(u),locationId,r.IsActive,ct)));
        MapStaff(merchant,"supervisors",UserRole.Supervisor);
        MapStaff(merchant,"cashiers",UserRole.Cashier);
        merchant.MapPost("/staff-invitations/{invitationId:guid}/revoke",async(Guid invitationId,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.RevokeInvitationAsync(Merchant(u),Actor(u),invitationId,ct)));
        endpoints.MapPost("/api/v1/staff-invitations/accept",async(AcceptStaffInvitationRequest r,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.AcceptInvitationAsync(r,ct))).WithTags("Staff invitations").RequireRateLimiting("auth-sensitive");
        var supervisor=endpoints.MapGroup("/api/v1/supervisor").WithTags("Supervisor").RequireAuthorization("SupervisorOnly");
        supervisor.MapGet("/me",async(ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.GetCurrentStaffAsync(Merchant(u),UserRole.Supervisor,u.SupervisorId!.Value,ct)));
        supervisor.MapGet("/locations",async(ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>{var result=await s.GetCurrentStaffAsync(Merchant(u),UserRole.Supervisor,u.SupervisorId!.Value,ct);return result.Succeeded?Results.Ok(result.Value!.Locations):ToHttp(result);});
        var cashier=endpoints.MapGroup("/api/v1/cashier").WithTags("Cashier").RequireAuthorization("CashierOnly");
        cashier.MapGet("/me",async(ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.GetCurrentStaffAsync(Merchant(u),UserRole.Cashier,u.CashierId!.Value,ct)));
        cashier.MapGet("/locations",async(ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>{var result=await s.GetCurrentStaffAsync(Merchant(u),UserRole.Cashier,u.CashierId!.Value,ct);return result.Succeeded?Results.Ok(result.Value!.Locations.Where(x=>x.IsActive)):ToHttp(result);});
        return endpoints;
    }
    private static void MapStaff(RouteGroupBuilder group,string route,UserRole role)
    {
        group.MapGet($"/{route}",async(ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.GetStaffAsync(Merchant(u),role,ct)));
        group.MapPost($"/{route}/invitations",async(StaffInvitationRequest r,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.InviteAsync(Merchant(u),Actor(u),role,r,ct)));
        group.MapGet($"/{route}/{{staffId:guid}}",async(Guid staffId,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.GetStaffAsync(Merchant(u),role,staffId,ct)));
        group.MapPut($"/{route}/{{staffId:guid}}",async(Guid staffId,StaffProfileRequest r,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.UpdateStaffAsync(Merchant(u),Actor(u),role,staffId,r,ct)));
        if(role==UserRole.Supervisor)group.MapPut($"/{route}/{{staffId:guid}}/locations",async(Guid staffId,SupervisorLocationsRequest r,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.AssignSupervisorAsync(Merchant(u),Actor(u),staffId,r,ct)));
        else group.MapPut($"/{route}/{{staffId:guid}}/locations",async(Guid staffId,CashierLocationsRequest r,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.AssignCashierAsync(Merchant(u),Actor(u),staffId,r,ct)));
        group.MapPatch($"/{route}/{{staffId:guid}}/status",async(Guid staffId,StatusRequest r,ICurrentUserService u,IOrganizationService s,CancellationToken ct)=>ToHttp(await s.SetStaffStatusAsync(Merchant(u),Actor(u),role,staffId,r.IsActive,ct)));
    }
    private static Guid Merchant(ICurrentUserService u)=>u.MerchantId??throw new InvalidOperationException("Authenticated user has no merchant scope.");private static Guid Actor(ICurrentUserService u)=>u.UserAccountId!.Value;
    private static IResult ToHttp(OrganizationResult r)=>r.Succeeded?Results.Ok(new{succeeded=true}):Results.Problem(r.Error,statusCode:r.StatusCode,title:"Organization request failed");
    private static IResult ToHttp<T>(OrganizationResult<T> r)=>r.Succeeded?Results.Json(r.Value,statusCode:r.StatusCode):Results.Problem(r.Error,statusCode:r.StatusCode,title:"Organization request failed");
}
