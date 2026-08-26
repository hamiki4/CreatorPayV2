import { useEffect, useState } from "react";
import { api } from "./apiClient";
import { daysLeftText, relationshipState } from "./relationshipTime";
import { currentPartnerships } from "./partnershipState";
import { rankMatches, useTypeahead } from "./typeahead";
import { ProfileAvatar } from "./profileMedia";
type Creator = {
  id: string;
  publicCreatorId: string;
  displayName: string;
  city: string;
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
  creatorProfileImageUrl?: string;
};
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
  const [items, setItems] = useState<Creator[]>([]),
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
  useTypeahead("", find, setItems);
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
      return { label: "No Relationship", daysText: null, canInvite: true };
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
                  <div className="creator-heading">
                    <ProfileAvatar
                      name={x.displayName}
                      photoUrl={x.profileImageUrl}
                    />
                    <div>
                      <strong>{x.displayName}</strong>
                      <small>{x.city}</small>
                    </div>
                  </div>
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
  refresh,
}: {
  items: BusinessRelationship[];
  refresh: () => void;
}) {
  const [message, setMessage] = useState(""),
    [reconciledId, setReconciledId] = useState<string>(),
    visible = items.filter((x) => relationshipState(x).label === "Active");
  useEffect(() => {
    const pending = items.find((x) => x.status === "Approved" && x.activationRequired && x.id !== reconciledId);
    if (!pending) return;
    setReconciledId(pending.id);
    void api(`/api/v1/merchant/partnerships/${pending.id}/reconcile-readiness`, { method: "POST" })
      .then(refresh)
      .catch((error) => console.error("Partnership readiness reconciliation failed", error));
  }, [items, reconciledId, refresh]);
  async function change(
    x: BusinessRelationship,
    action: "activate" | "deactivate" | "reactivate",
  ) {
    if (
      action === "deactivate" &&
      !confirm(
        "Deactivate this advertising relationship? New attributed sales will stop immediately.",
      )
    )
      return;
    const endpoint = action === "deactivate" ? "suspend" : action;
    try {
      await api(`/api/v1/merchant/partnerships/${x.id}/${endpoint}`, {
        method: "POST",
        body: JSON.stringify({
          reason:
            action === "deactivate"
              ? "Deactivated by Business"
              : action === "reactivate"
                ? "Reactivated by Business"
                : "Activated by Business",
        }),
      });
      setMessage(
        `${x.creatorName} ad is now ${action === "deactivate" ? "deactivated" : "active"}.`,
      );
      refresh();
    } catch (error) {
      console.error(error);
      setMessage((error as Error).message);
    }
  }
  return (
    <section className="creator-section">
      <h2>Active Ads</h2>
      {message && <p role="status">{message}</p>}
          {visible.length === 0 ? (
        <p className="compact-empty">
          No Creator advertising relationships yet.
        </p>
      ) : (
        <div className="active-ads">
          {visible.map((x) => {
            const state = relationshipState(x);
            return (
              <article
              className={`active-ad business-table-row business-active-grid relationship-${state.tone}`}
              key={x.id}
            >
                <div className="creator-card">
                  <div className="creator-heading">
                    <ProfileAvatar
                      name={x.creatorName}
                      photoUrl={x.creatorProfileImageUrl}
                    />
                    <div>
                      <strong>{x.creatorName}</strong>
                    </div>
                  </div>
                </div>
                <span className="creator-meta">
                  <SocialMediaLink
                    platform={x.creatorSocialPlatform}
                    profileUrl={x.creatorSocialProfileUrl}
                  />
                </span>
                <span className="table-status">
                  <span className="status-badge">{state.label}</span>
                </span>
                {state.daysLeft !== null && (
                  <span className="days-left">{daysLeftText(state.daysLeft, state.tone)}</span>
                )}
                <span className="table-action">
                  {state.label === "Active" ? (
                    <button
                      className="danger compact-action"
                      onClick={() => void change(x, "deactivate")}
                    >
                      Deactivate Ad
                    </button>
                  ) : null}
                </span>
                <small>
                  {x.activatedAtUtc
                    ? `Activated ${new Date(x.activatedAtUtc).toLocaleDateString()}`
                    : "Advertising relationship"}
                </small>
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
  const requests = currentPartnerships(items, (x) => x.creatorId).filter(
    (x) => !["Approved", "Suspended", "Revoked"].includes(x.status),
  );
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
          ? `${x.creatorName} approved and is active when Business readiness requirements are satisfied.`
          : `Request from ${x.creatorName} declined.`,
      );
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
      {requests.length === 0 ? (
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
                    <div className="creator-heading">
                      <ProfileAvatar
                        name={x.creatorName}
                        photoUrl={x.creatorProfileImageUrl}
                      />
                      <div>
                        <strong>{x.creatorName}</strong>
                        <small>
                          {new Date(x.requestedAtUtc).toLocaleDateString()}
                        </small>
                      </div>
                    </div>
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
            <h3>Outgoing Invitations</h3>
          {requests
              .filter((x) => x.initiatedBy === "Business")
              .map((x) => (
                <article key={x.id}>
                  <div className="creator-card">
                    <div className="creator-heading">
                      <ProfileAvatar
                        name={x.creatorName}
                        photoUrl={x.creatorProfileImageUrl}
                      />
                      <div>
                        <strong>{x.creatorName}</strong>
                        <small>
                          {new Date(x.requestedAtUtc).toLocaleDateString()}
                        </small>
                      </div>
                    </div>
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
