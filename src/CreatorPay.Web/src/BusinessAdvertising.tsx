import { FormEvent, useEffect, useState } from "react";
import { api } from "./apiClient";
import { daysLeftText, relationshipState } from "./relationshipTime";
import { currentPartnerships } from "./partnershipState";
import { rankMatches, useTypeahead } from "./typeahead";
type Creator = {
  id: string;
  publicCreatorId: string;
  displayName: string;
  city: string;
  biography: string;
  contentCategories: string;
  socialPlatform?: string;
  followerCount?: number;
};
export type BusinessRelationship = {
  id: string;
  creatorId: string;
  creatorName: string;
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
};
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
      return { label: "No relationship", days: "—", canInvite: true };
    if (relationship.status === "Pending")
      return { label: "Invitation Pending", days: "—", canInvite: false };
    if (relationship.status === "Approved") {
      const state = relationshipState(relationship);
      return {
        label: state.label === "Active" ? "Currently Advertising" : state.label,
        days:
          state.daysLeft === null
            ? "—"
            : daysLeftText(state.daysLeft, state.tone),
        canInvite: false,
      };
    }
    if (["Revoked", "Suspended"].includes(relationship.status))
      return { label: "Deactivated", days: "—", canInvite: false, canReactivate: true, relationship };
    if (relationship.status === "Blocked")
      return { label: "Blocked", days: "—", canInvite: false };
    if (relationship.status === "Rejected")
      return { label: "Declined", days: "—", canInvite: true, requestAgain: true };
    return { label: "Unavailable", days: "—", canInvite: false };
  }
  return (
    <section className="creator-section">
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
          <div className="discovery-row discovery-headings">
            <span>Creator</span>
            <span>Status</span>
            <span>Days Left</span>
            <span>Action</span>
          </div>
          {items.map((x) => {
            const state = stateFor(x);
            return (
              <article className="discovery-row" key={x.id}>
                <div>
                  <strong>{x.displayName}</strong>
                  <span>
                    {[
                      x.publicCreatorId,
                      x.socialPlatform,
                      x.followerCount != null
                        ? `${x.followerCount.toLocaleString()} followers`
                        : null,
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                  <small>
                    {[x.city, x.biography].filter(Boolean).join(" · ")}
                  </small>
                </div>
                <span data-label="Status">
                  <span className="status-badge">{state.label}</span>
                </span>
                <span data-label="Days Left">{state.days}</span>
                <span data-label="Action">
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
                </span>
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
          <div className="active-ad business-table-row business-active-grid headings">
            <span>Creator</span>
            <span>Status</span>
            <span>Days Left</span>
            <span>Action</span>
          </div>
          {visible.map((x) => {
            const state = relationshipState(x);
            return (
              <article
                className={`active-ad business-table-row business-active-grid relationship-${state.tone}`}
                key={x.id}
              >
                <strong data-label="Creator">{x.creatorName}</strong>
                <span className="table-status" data-label="Status">
                  <span className="status-badge">{state.label}</span>
                </span>
                <span className="days-left" data-label="Days Left">
                  {daysLeftText(state.daysLeft, state.tone)}
                </span>
                <span className="table-action" data-label="Action">
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
                  <div>
                    <strong>{x.creatorName}</strong>
                    <small>
                      {new Date(x.requestedAtUtc).toLocaleDateString()}
                    </small>
                  </div>
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
                  <div>
                    <strong>{x.creatorName}</strong>
                    <small>
                      {new Date(x.requestedAtUtc).toLocaleDateString()}
                    </small>
                  </div>
                  <span>{status(x.status)}</span>
                </article>
              ))}
          </div>
        </div>
      )}
    </section>
  );
}
