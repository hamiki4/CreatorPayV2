using System.Globalization;
using System.Text;
using System.Text.Json;
using CreatorPay.Api.Authentication;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Api.Reporting;

public static class ReportingEndpoints
{
    private static readonly HashSet<string> ReportTypes = ["transactions", "earnings", "operations"];
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reports = endpoints.MapGroup("/api/v1/reports").WithTags("Reporting").RequireAuthorization("AuthenticatedUser");
        reports.MapGet("/dashboard", Dashboard).RequireRateLimiting("admin-report");
        reports.MapGet("/saved-views", SavedViews);
        reports.MapPost("/saved-views", SaveView);
        reports.MapDelete("/saved-views/{id:guid}", DeleteView);
        reports.MapGet("/export", Export).RequireRateLimiting("admin-report");
        var policies = endpoints.MapGroup("/api/v1/admin/alert-thresholds").WithTags("Reporting policies").RequireAuthorization("PlatformAdminOnly");
        policies.MapGet("/", Thresholds);
        policies.MapPost("/", CreateThreshold);
        policies.MapPost("/{id:guid}/approve", ApproveThreshold);
        return endpoints;
    }

    private static async Task<IResult> Dashboard(DateTime? from, DateTime? to, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct)
    {
        var (start,end,error)=Range(from,to,31); if(error is not null)return error;
        var purchases=db.PurchaseTransactions.AsNoTracking().Where(x=>x.TransactionDateUtc>=start&&x.TransactionDateUtc<=end);
        if(user.Role==nameof(UserRole.MerchantAdmin)) purchases=purchases.Where(x=>x.MerchantId==user.MerchantId);
        else if(user.Role==nameof(UserRole.Creator)) purchases=purchases.Where(x=>x.CreatorId==user.CreatorId);
        else if(user.Role!=nameof(UserRole.PlatformAdmin)) return Results.Forbid();
        var trend=await purchases.GroupBy(x=>x.TransactionDateUtc.Date).Select(g=>new{date=g.Key,count=g.Count(),volume=g.Sum(x=>x.PurchaseAmount)}).OrderBy(x=>x.date).ToListAsync(ct);
        var earnings=db.CreatorEarnings.AsNoTracking().Where(x=>x.EarnedAtUtc>=start&&x.EarnedAtUtc<=end);
        if(user.Role==nameof(UserRole.Creator)) earnings=earnings.Where(x=>x.CreatorId==user.CreatorId);
        else if(user.Role==nameof(UserRole.MerchantAdmin)) earnings=from e in earnings join p in purchases on e.PurchaseTransactionId equals p.Id select e;
        var result=new { role=user.Role, range=new{from=start,to=end}, metrics=new { transactions=await purchases.CountAsync(ct), transactionVolume=await purchases.SumAsync(x=>(decimal?)x.PurchaseAmount,ct)??0, creatorEarnings=await earnings.SumAsync(x=>(decimal?)x.Amount,ct)??0, openAlerts=user.Role==nameof(UserRole.PlatformAdmin)?await db.OperationalAlerts.CountAsync(x=>x.Status!="Resolved",ct):0 }, trend };
        return Results.Ok(result);
    }

    private static async Task<IResult> SavedViews(ICurrentUserService user, ApplicationDbContext db, CancellationToken ct) => Results.Ok(await db.SavedReportViews.AsNoTracking().Where(x=>x.OwnerUserId==user.UserAccountId).OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.ReportType,x.FiltersJson,x.IsDefault,x.UpdatedAtUtc}).ToListAsync(ct));
    private static async Task<IResult> SaveView(SaveViewRequest request, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(request.Name)||request.Name.Length>100||!ReportTypes.Contains(request.ReportType))return Results.BadRequest(new{error="A valid name and report type are required."});
        try{using var doc=JsonDocument.Parse(request.FiltersJson);if(doc.RootElement.ValueKind!=JsonValueKind.Object)return Results.BadRequest(new{error="Filters must be a JSON object."});}catch(JsonException){return Results.BadRequest(new{error="Filters are not valid JSON."});}
        if(request.IsDefault)await db.SavedReportViews.Where(x=>x.OwnerUserId==user.UserAccountId&&x.ReportType==request.ReportType&&x.IsDefault).ExecuteUpdateAsync(x=>x.SetProperty(v=>v.IsDefault,false),ct);
        var view=new SavedReportView{Id=Guid.NewGuid(),OwnerUserId=user.UserAccountId!.Value,OwnerRole=user.Role!,Name=request.Name.Trim(),ReportType=request.ReportType,FiltersJson=request.FiltersJson,IsDefault=request.IsDefault,CreatedAtUtc=DateTime.UtcNow};db.Add(view);await db.SaveChangesAsync(ct);return Results.Created($"/api/v1/reports/saved-views/{view.Id}",new{view.Id});
    }
    private static async Task<IResult> DeleteView(Guid id, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct){var view=await db.SavedReportViews.SingleOrDefaultAsync(x=>x.Id==id&&x.OwnerUserId==user.UserAccountId,ct);if(view is null)return Results.NotFound();db.Remove(view);await db.SaveChangesAsync(ct);return Results.NoContent();}

    private static async Task<IResult> Export(string reportType,string format,DateTime? from,DateTime? to,ICurrentUserService user,HttpContext http,ApplicationDbContext db,CancellationToken ct)
    {
        format=format.ToLowerInvariant();if(!ReportTypes.Contains(reportType)||format is not ("csv" or "pdf"))return Results.BadRequest(new{error="Supported reports are transactions, earnings, and operations; formats are csv and pdf."});
        if(user.Role is not (nameof(UserRole.PlatformAdmin) or nameof(UserRole.MerchantAdmin) or nameof(UserRole.Creator)))return Results.Forbid();
        if(reportType=="operations"&&user.Role!=nameof(UserRole.PlatformAdmin))return Results.Forbid();
        var (start,end,error)=Range(from,to,366);if(error is not null)return error;var rows=new List<string[]>();string[] headers;
        if(reportType=="earnings") { headers=["Earning ID","Date (UTC)","Amount","Currency","Status"];var q=db.CreatorEarnings.AsNoTracking().Where(x=>x.EarnedAtUtc>=start&&x.EarnedAtUtc<=end);if(user.Role==nameof(UserRole.Creator))q=q.Where(x=>x.CreatorId==user.CreatorId);else if(user.Role==nameof(UserRole.MerchantAdmin)){var ids=db.PurchaseTransactions.Where(x=>x.MerchantId==user.MerchantId).Select(x=>x.Id);q=q.Where(x=>ids.Contains(x.PurchaseTransactionId));}rows=await q.OrderByDescending(x=>x.EarnedAtUtc).Take(10000).Select(x=>new[]{x.Id.ToString(),x.EarnedAtUtc.ToString("O"),x.Amount.ToString(CultureInfo.InvariantCulture),x.CurrencyCode,x.Status.ToString()}).ToListAsync(ct); }
        else if(reportType=="operations") { headers=["Alert type","Severity","Status","Detected (UTC)"];rows=await db.OperationalAlerts.AsNoTracking().Where(x=>x.DetectedAtUtc>=start&&x.DetectedAtUtc<=end).OrderByDescending(x=>x.DetectedAtUtc).Take(10000).Select(x=>new[]{x.AlertType,x.Severity,x.Status,x.DetectedAtUtc.ToString("O")}).ToListAsync(ct); }
        else { headers=["Transaction ID","Date (UTC)","Amount","Currency","Status"];var q=db.PurchaseTransactions.AsNoTracking().Where(x=>x.TransactionDateUtc>=start&&x.TransactionDateUtc<=end);if(user.Role==nameof(UserRole.MerchantAdmin))q=q.Where(x=>x.MerchantId==user.MerchantId);else if(user.Role==nameof(UserRole.Creator))q=q.Where(x=>x.CreatorId==user.CreatorId);rows=await q.OrderByDescending(x=>x.TransactionDateUtc).Take(10000).Select(x=>new[]{x.PublicTransactionId,x.TransactionDateUtc.ToString("O"),x.PurchaseAmount.ToString(CultureInfo.InvariantCulture),x.CurrencyCode,x.Status.ToString()}).ToListAsync(ct); }
        db.ReportExportAudits.Add(new(){Id=Guid.NewGuid(),RequestedByUserId=user.UserAccountId!.Value,RequesterRole=user.Role!,ReportType=reportType,Format=format,FiltersJson=JsonSerializer.Serialize(new{from=start,to=end}),RowCount=rows.Count,CorrelationId=http.TraceIdentifier,RequestedAtUtc=DateTime.UtcNow,CreatedAtUtc=DateTime.UtcNow});await db.SaveChangesAsync(ct);
        var filename=$"creatorpay-{reportType}-{DateTime.UtcNow:yyyyMMdd}.{format}";if(format=="csv")return Results.File(Csv(headers,rows),"text/csv; charset=utf-8",filename);return Results.File(Pdf(reportType,headers,rows),"application/pdf",filename);
    }

    private static async Task<IResult> Thresholds(ApplicationDbContext db,CancellationToken ct)=>Results.Ok(await db.AlertThresholdPolicies.AsNoTracking().OrderBy(x=>x.AlertType).ThenByDescending(x=>x.Version).ToListAsync(ct));
    private static async Task<IResult> CreateThreshold(ThresholdRequest request,ICurrentUserService user,ApplicationDbContext db,CancellationToken ct){if(request.Threshold<0||request.WindowMinutes<1||request.CooldownMinutes<1||string.IsNullOrWhiteSpace(request.AlertType))return Results.BadRequest(new{error="Threshold and positive windows are required."});var version=(await db.AlertThresholdPolicies.Where(x=>x.AlertType==request.AlertType).MaxAsync(x=>(int?)x.Version,ct)??0)+1;var p=new AlertThresholdPolicy{Id=Guid.NewGuid(),AlertType=request.AlertType.Trim(),Version=version,Threshold=request.Threshold,WindowMinutes=request.WindowMinutes,CooldownMinutes=request.CooldownMinutes,Severity=request.Severity,CreatedByUserId=user.UserAccountId!.Value,Status="Draft",CreatedAtUtc=DateTime.UtcNow};db.Add(p);await db.SaveChangesAsync(ct);return Results.Created($"/api/v1/admin/alert-thresholds/{p.Id}",p);}
    private static async Task<IResult> ApproveThreshold(Guid id,ReviewRequest request,ICurrentUserService user,ApplicationDbContext db,CancellationToken ct){var p=await db.AlertThresholdPolicies.SingleOrDefaultAsync(x=>x.Id==id,ct);if(p is null)return Results.NotFound();if(p.Status!="Draft")return Results.Conflict(new{error="Only draft policies can be approved."});if(p.CreatedByUserId==user.UserAccountId)return Results.BadRequest(new{error="Policy review requires a different Platform Admin."});await db.AlertThresholdPolicies.Where(x=>x.AlertType==p.AlertType&&x.Status=="Approved").ExecuteUpdateAsync(x=>x.SetProperty(v=>v.Status,"Superseded"),ct);p.Status="Approved";p.ApprovedByUserId=user.UserAccountId;p.ApprovedAtUtc=DateTime.UtcNow;p.ReviewReason=request.Reason;await db.SaveChangesAsync(ct);return Results.NoContent();}
    private static (DateTime Start,DateTime End,IResult? Error) Range(DateTime? from,DateTime? to,int maxDays){var end=DateTime.SpecifyKind(to??DateTime.UtcNow,DateTimeKind.Utc);var start=DateTime.SpecifyKind(from??end.AddDays(-30),DateTimeKind.Utc);return start>end||(end-start).TotalDays>maxDays?(start,end,Results.BadRequest(new{error=$"Date range must be ordered and no longer than {maxDays} days."})):(start,end,null);}
    private static byte[] Csv(string[] headers,List<string[]> rows){static string E(string x)=>$"\"{x.Replace("\"","\"\"")}\"";var b=new StringBuilder("\uFEFF").AppendLine(string.Join(',',headers.Select(E)));foreach(var r in rows)b.AppendLine(string.Join(',',r.Select(E)));return Encoding.UTF8.GetBytes(b.ToString());}
    private static byte[] Pdf(string title,string[] headers,List<string[]> rows){var lines=new[]{title.ToUpperInvariant(),string.Join(" | ",headers)}.Concat(rows.Take(250).Select(x=>string.Join(" | ",x))).ToArray();var content=new StringBuilder("BT /F1 8 Tf 36 800 Td 10 TL ");foreach(var l in lines){var safe=l.Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)");content.Append('(').Append(safe[..Math.Min(safe.Length,140)]).Append(") Tj T* ");}content.Append("ET");var stream=content.ToString();var objects=new[]{"<< /Type /Catalog /Pages 2 0 R >>","<< /Type /Pages /Kids [3 0 R] /Count 1 >>","<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",$"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream","<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"};var s=new StringBuilder("%PDF-1.4\n");var offsets=new List<int>();for(var i=0;i<objects.Length;i++){offsets.Add(Encoding.ASCII.GetByteCount(s.ToString()));s.Append($"{i+1} 0 obj\n{objects[i]}\nendobj\n");}var xref=Encoding.ASCII.GetByteCount(s.ToString());s.Append($"xref\n0 {objects.Length+1}\n0000000000 65535 f \n");foreach(var o in offsets)s.Append($"{o:0000000000} 00000 n \n");s.Append($"trailer << /Size {objects.Length+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");return Encoding.ASCII.GetBytes(s.ToString());}
    public sealed record SaveViewRequest(string Name,string ReportType,string FiltersJson,bool IsDefault);
    public sealed record ThresholdRequest(string AlertType,decimal Threshold,int WindowMinutes,int CooldownMinutes,string Severity);
    public sealed record ReviewRequest(string Reason);
}
