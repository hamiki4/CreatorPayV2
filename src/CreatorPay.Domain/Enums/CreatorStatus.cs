namespace CreatorPay.Domain.Enums;

public enum CreatorStatus { Draft, PendingVerification, PendingApproval, PendingReview = PendingApproval, CorrectionRequested, Active, Suspended, Rejected, Closed }
public enum SocialPlatform { TikTok, Instagram, Facebook, Telegram, YouTube, Other }
public enum SocialProfileVerificationStatus { Unverified, Pending, Verified, Rejected }
