import { FormEvent, useEffect, useState } from "react";
import { api } from "./apiClient";
import { daysLeftText, relationshipState } from "./relationshipTime";
import { currentPartnerships } from "./partnershipState";
import { rankMatches, useTypeahead } from "./typeahead";
import { ProfileAvatar } from "./profileMedia";
import { NavIcon } from "./navIcons";
type Creator = {
  id: string;
  publicCreatorId: string;
  displayName: string;
  city: string;
  phoneNumber?: string;
  biography: string;
  contentCategories: string;
  socialPlatform?: string;
  socialProfileUrl?: string;
  followerCount?: number;
  profileImageUrl?: string;
};
export type BusinessRelationship = {
  id: string;
  creatorId: string;
  creatorPublicId?: string;
  creatorName: string;
  creatorSocialPlatform?: string;
  creatorSocialProfileUrl?: string;
  status: string;
  requestedAtUtc: string;
  startDateUtc?: string;
  endDateUtc?: string;
  activatedAtUtc?: string;
  expiresAtUtc?: string;
  promotionActive: boolean;
  relationshipState?: string;
  activationRequired?: boolean;
  initiatedBy: "Business" | "Creator" | "Unknown";
  creatorCity?: string;
  creatorProfileImageUrl?: string;
  creatorPhoneNumber?: string;
  promotionVideo?: {
    id: string;
    videoUrl: string;
    platform: string;
    status: string;
    submittedAtUtc: string;
    reviewedAtUtc?: string;
    rejectionReason?: string;
  };
};
const displayDate = (value?: string) => value ? new Intl.DateTimeFormat("en-GB", {day: "2-digit", month: "2-digit", year: "numeric"}).format(new Date(value)) : "—";
const socialPlatforms = new Map([
  ["tiktok", "TikTok"],
  ["instagram", "Instagram"],
  ["youtube", "YouTube"],
  ["facebook", "Facebook"],
]);
function socialMedia(platform?: string, profileUrl?: string) {
  if (!profileUrl) return { label: "Not provided" };
  let url: URL;
  try {
    url = new URL(profileUrl);
  } catch {
    return { label: "Not provided" };
  }
  if (url.protocol !== "http:" && url.protocol !== "https:") return { label: "Not provided" };
  const label = platform ? socialPlatforms.get(platform.toLowerCase()) : undefined;
  return { href: url.toString(), label: label ?? "View Profile" };
}
function SocialMediaLink({ platform, profileUrl }: { platform?: string; profileUrl?: string }) {
  const link = socialMedia(platform, profileUrl);
  return "href" in link ? (
    <a
      className="social-media-link"
      href={link.href}
      target="_blank"
      rel="noopener noreferrer"
      style={{ color: '#2563eb', textDecoration: 'none' }}
    >
      {link.label}
    </a>
  ) : (
    <span className="social-media-link">{link.label}</span>
  );
}
function CreatorMeta({ platform, profileUrl }: { platform?: string; profileUrl?: string }) {
  return (
    <span className="creator-meta">
      <SocialMediaLink platform={platform} profileUrl={profileUrl} />
    </span>
  );
}
function CreatorIdentity({
  name,
  photoUrl,
  phoneNumber,
  city,
}: {
  name: string;
  photoUrl?: string;
  phoneNumber?: string;
  city?: string;
}) {
  return (
    <div className="creator-heading">
      <ProfileAvatar name={name} photoUrl={photoUrl} />
      <div className="creator-identity-text">
        <strong>{name}</strong>
        {phoneNumber && <small style={{ display: 'block' }}>{phoneNumber}</small>}
        {city && <small className="business-creator-city"><NavIcon name="mapPin" size={14} />{city}</small>}
      </div>
    </div>
  );
}
const status = (value: string) =>
  value === "Approved"
    ? "Active"
    : value === "Rejected"
      ? "Declined"
      : value === "Revoked"
        ? "Deactivated"
        : value === "Pending"
          ? "Pending"
          : "Inactive";

export function FindCreators({ refresh }: { refresh: () => void }) {
  const [q, setQ] = useState(""),
    [items, setItems] = useState<Creator[]>([]),
    [relationships, setRelationships] = useState<BusinessRelationship[]>([]),
    [message, setMessage] = useState(""),
    [loading, setLoading] = useState(true);
  const load = () =>
    api<BusinessRelationship[]>("/api/v1/merchant/partnerships")
      .then(setRelationships)
      .catch(console.error);
  const find = async (term: string) => {
    setLoading(true);
    setMessage("");
    try {
      return rankMatches(
        await api<Creator[]>(
          `/api/v1/merchant/creators/search?q=${encodeURIComponent(term)}`,
        ),
        term,
        (x) => [x.displayName, x.publicCreatorId, x.socialPlatform, x.city],
      );
    } catch (error) {
      console.error(error);
      setMessage("We couldn't load this information.");
      return [];
    } finally {
      setLoading(false);
    }
  };
  useTypeahead(q, find, setItems);
  const search = async (e?: FormEvent) => {
    e?.preventDefault();
    setItems(await find(q));
  };
  useEffect(() => {
    void load();
  }, []);
  async function invite(x: Creator) {
    try {
      await api("/api/v1/merchant/partnerships/invitations", {
        method: "POST",
        body: JSON.stringify({ creatorId: x.id, introductoryMessage: null }),
      });
      setMessage(`Invitation sent to ${x.displayName}.`);
      await load();
      refresh();
    } catch (error) {
      console.error(error);
      setMessage("We couldn't send that invitation.");
    }
  }
  async function reactivate(x: Creator, relationship: BusinessRelationship) {
    try {
      await api(`/api/v1/merchant/partnerships/${relationship.id}/reactivate`, {
        method: "POST",
        body: JSON.stringify({ reason: "Reactivated by Business" }),
      });
      setMessage(`${x.displayName} is active for advertising.`);
      await load();
      refresh();
    } catch (error) {
      console.error(error);
      setMessage((error as Error).message);
    }
  }
  const currentRelationships = currentPartnerships(relationships, (x) => x.creatorId);
  function stateFor(creator: Creator) {
    const relationship = currentRelationships.find((x) => x.creatorId === creator.id);
    if (!relationship)
      return { label: "NO RELATIONSHIP", daysText: null, canInvite: true };
    if (relationship.status === "Pending")
      return { label: "Invitation Pending", daysText: null, canInvite: false };
    if (relationship.status === "Approved") {
      const state = relationshipState(relationship);
      return {
        label: state.label,
        daysText:
          state.daysLeft === null
            ? null
            : daysLeftText(state.daysLeft, state.tone),
        canInvite: false,
      };
    }
    if (["Revoked", "Suspended"].includes(relationship.status))
      return { label: "Deactivated", daysText: null, canInvite: false, canReactivate: true, relationship };
    if (relationship.status === "Blocked")
      return { label: "Blocked", daysText: null, canInvite: false };
    if (relationship.status === "Rejected")
      return { label: "Declined", daysText: null, canInvite: true, requestAgain: true };
    return { label: "Unavailable", daysText: null, canInvite: false };
  }
  return (
    <section className="creator-section business-find-creators">
      <h2>Find Creators</h2>
      <p>Search approved Creators and invite them to advertise.</p>
      <form className="business-search" onSubmit={search}>
        <label>
          Creator name or public ID
          <input
            type="search"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Search by name or public ID"
          />
        </label>
        <button disabled={loading}>{loading ? "Searching…" : "Search"}</button>
      </form>
      {message && (
        <p
          className={
            message.includes("sent") ? "success-note" : "friendly-error"
          }
        >
          {message}
        </p>
      )}
      {!loading && items.length === 0 ? (
        <p className="compact-empty">No approved Creators found.</p>
      ) : (
        <div className="discovery-list">
          {items.map((x) => {
            const state = stateFor(x);
            return (
              <article className="business-card business-discovery-card" key={x.id}>
                <div className="creator-card">
                  <CreatorIdentity
                    name={x.displayName}
                    photoUrl={x.profileImageUrl}
                    phoneNumber={x.phoneNumber}
                    city={x.city}
                  />
                </div>
                <div className="business-card-meta">
                  <CreatorMeta platform={x.socialPlatform} profileUrl={x.socialProfileUrl} />
                  <span className="status-badge">{state.label}</span>
                  {state.daysText !== null && <small className="business-card-days">{state.daysText}</small>}
                </div>
                <div className="business-card-actions">
                  {state.canInvite && (
                    <button onClick={() => void invite(x)}>
                      {state.label === "Declined" ? "Request Again" : "Invite to Advertise"}
                    </button>
                  )}
                  {state.canReactivate && (
                    <button type="button" onClick={() => void reactivate(x, state.relationship)}>
                      Reactivate
                    </button>
                  )}
                </div>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}

export function ActiveCreators({
  items,
  refresh: _refresh,
}: {
  items: BusinessRelationship[];
  refresh: () => void;
}) {
  const visible = items.filter((x) => relationshipState(x).label === "Active");
  return (
    <section className="creator-section">
      <h2>Active Ads</h2>
      {visible.length === 0 ? (
        <p className="compact-empty">No live Creator promotions yet.</p>
      ) : (
        <div className="active-ads">
          {visible.map((x) => {
            const state = relationshipState(x);
            return (
              <article
                className={`active-ad business-table-row business-active-grid relationship-${state.tone}`}
                key={x.id}
              >
                <div data-label="Creator"><CreatorIdentity name={x.creatorName} photoUrl={x.creatorProfileImageUrl} /></div>
                <span data-label="Creator ID">{x.creatorPublicId ?? x.creatorId}</span>
                <span data-label="Promo Video">{x.promotionVideo?.videoUrl?<a className="promo-video-link" href={x.promotionVideo.videoUrl} target="_blank" rel="noopener noreferrer">View Promo Video</a>:"—"}</span>
                <span data-label="Activated Date">{displayDate(x.activatedAtUtc)}</span>
                <span className="days-left" data-label="Days Left">{state.daysLeft===null?"—":daysLeftText(state.daysLeft,state.tone)}</span>
                <span className="table-status" data-label="Status"><span className="status-badge">Active</span></span>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}

export function AdvertisingRequests({
  items,
  refresh,
}: {
  items: BusinessRelationship[];
  refresh: () => void;
}) {
  const [message, setMessage] = useState("");
  const [rejectionReasons, setRejectionReasons] = useState<Record<string,string>>({});
  const requests = currentPartnerships(items, (x) => x.creatorId).filter(
    (x) => !["Approved", "Suspended", "Revoked"].includes(x.status),
  );
  const promoApprovals = items.filter((x) => x.promotionVideo?.status === "Pending");
  async function decide(x: BusinessRelationship, accept: boolean) {
    try {
      await api(
        `/api/v1/merchant/partnerships/${x.id}/${accept ? "approve" : "reject"}`,
        {
          method: "POST",
          body: JSON.stringify({
            reason: accept ? null : "Declined by Business",
          }),
        },
      );
      setMessage(
        accept
          ? `${x.creatorName} approved. Awaiting promo video submission.`
          : `Request from ${x.creatorName} declined.`,
      );
      refresh();
    } catch (error) {
      console.error(error);
      setMessage((error as Error).message);
    }
  }
  async function reviewVideo(x: BusinessRelationship, accept: boolean) {
    if (!x.promotionVideo) return;
    try {
      await api(
        `/api/v1/merchant/promotion-videos/${x.promotionVideo.id}/${accept ? "approve" : "reject"}`,
        {
          method: "POST",
          body: JSON.stringify({ reason: accept ? null : rejectionReasons[x.promotionVideo.id]?.trim() || null }),
        },
      );
      setMessage(accept ? `Promotion video for ${x.creatorName} approved.` : `Promotion video for ${x.creatorName} rejected.`);
      refresh();
    } catch (error) {
      console.error(error);
      setMessage((error as Error).message);
    }
  }
  return (
    <section className="creator-section">
      <h2>Requests</h2>
      {message && (
        <p
          className={
            message.includes("approved") ? "success-note" : "friendly-error"
          }
        >
          {message}
        </p>
      )}
      {requests.length === 0 && promoApprovals.length === 0 ? (
        <p className="compact-empty">No pending requests or invitations.</p>
      ) : (
        <div className="request-groups">
          <div>
            <h3>Incoming Advertising Requests</h3>
          {requests
              .filter((x) => x.initiatedBy === "Creator")
              .map((x) => (
                <article key={x.id}>
                <div className="creator-card">
                  <CreatorIdentity
                    name={x.creatorName}
                    photoUrl={x.creatorProfileImageUrl}
                    phoneNumber={x.creatorPhoneNumber}
                    city={x.creatorCity}
                  />
                  <small>{displayDate(x.requestedAtUtc)}</small>
                </div>
                  <span data-label="Social Media">
                    <SocialMediaLink
                      platform={x.creatorSocialPlatform}
                      profileUrl={x.creatorSocialProfileUrl}
                    />
                  </span>
                  <span>{status(x.status)}</span>
                  {x.status === "Pending" && (
                    <div>
                      <button onClick={() => void decide(x, true)}>
                        Accept
                      </button>
                      <button
                        className="quiet"
                        onClick={() => void decide(x, false)}
                      >
                        Decline
                      </button>
                    </div>
                  )}
                </article>
              ))}
          </div>
          <div>
            <h3>Promo Video Approvals</h3>
            {promoApprovals.length === 0 ? (
              <p className="compact-empty">No promo videos waiting for approval.</p>
            ) : (
              promoApprovals.map((x) => (
                <article key={x.id}>
                  <div className="creator-card">
                    <CreatorIdentity
                      name={x.creatorName}
                      photoUrl={x.creatorProfileImageUrl}
                      phoneNumber={x.creatorPhoneNumber}
                      city={x.creatorCity}
                    />
                    <small>{displayDate(x.promotionVideo?.submittedAtUtc ?? x.requestedAtUtc)}</small>
                  </div>
                  <strong>Promo Video Approval</strong>
                  <span>Creator: {x.creatorName}</span>
                  <span>Creator ID: {x.creatorPublicId ?? x.creatorId}</span>
                  <span className="status-badge status-pending">Pending Your Approval</span>
                  <a
                    href={x.promotionVideo?.videoUrl ?? "#"}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="quiet"
                  >
                    View Promo Video
                  </a>
                  <label className="promo-rejection-reason">Rejection reason (optional)<input value={rejectionReasons[x.promotionVideo?.id ?? x.id]??""} maxLength={180} onChange={event=>setRejectionReasons(current=>({...current,[x.promotionVideo?.id ?? x.id]:event.target.value}))} placeholder="Short reason" /></label>
                  <div>
                    <button onClick={() => void reviewVideo(x, true)}>Approve</button>
                    <button className="danger" onClick={() => void reviewVideo(x, false)}>
                      Reject
                    </button>
                  </div>
                </article>
              ))
            )}
          </div>
          <div>
            <h3>Outgoing Invitations</h3>
          {requests
              .filter((x) => x.initiatedBy === "Business")
              .map((x) => (
                <article key={x.id}>
                <div className="creator-card">
                  <CreatorIdentity
                    name={x.creatorName}
                    photoUrl={x.creatorProfileImageUrl}
                    phoneNumber={x.creatorPhoneNumber}
                    city={x.creatorCity}
                  />
                  <small>{displayDate(x.requestedAtUtc)}</small>
                </div>
                  <span data-label="Social Media">
                    <SocialMediaLink
                      platform={x.creatorSocialPlatform}
                      profileUrl={x.creatorSocialProfileUrl}
                    />
                  </span>
                  <span>{status(x.status)}</span>
                </article>
              ))}
          </div>
        </div>
      )}
    </section>
  );
}
