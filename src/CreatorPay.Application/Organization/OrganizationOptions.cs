namespace CreatorPay.Application.Organization;

public sealed class StaffInvitationOptions { public const string SectionName = "StaffInvitations"; public int LifetimeHours { get; set; } = 48; public bool ReturnDevelopmentToken { get; set; } public string WebBaseUrl { get; set; } = "http://localhost:5173"; }
