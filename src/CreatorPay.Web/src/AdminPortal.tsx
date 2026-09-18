import { FormEvent, ReactNode, useEffect, useState } from "react";
import { ReportingWorkspace } from "./ReportingWorkspace";
import { clearAuthState, handleUnauthorized } from "./authSession";
import { getAccessToken } from "./sessionStore";
import { rankMatches, useTypeahead } from "./typeahead";
import { api as authenticatedApi } from "./apiClient";
import { AdminAccountCreate } from "./AdminAccountCreate";
import { businessTypes } from "./AuthWorkspace";
import { formatDateTime, formatUserFacingText } from "./displayFormat";
import { NavIcon, NavIconName } from "./navIcons";
import {getExternalSession,isExternalSession,signOutExternalSession,switchExternalProfile} from './externalSession'
import {AdminPromotions} from './PromotionWorkspace'
import {AdminUgc} from './UgcWorkspace'
import {ProductAdminDashboard,V3AdminAccounts,V3AdminNotifications,V3AuditTrail,V3FinancialSettings,V3FundingDeposits,V3Payouts,V3PlatformRevenue,V3Wallets} from './ProductAdminWorkspaces'
import {v3Post,v3Request} from './v3ProductApi'

type Row = Record<string, unknown>;
type Page<T = Row> = { items: T[]; page: number; total: number; totalPages: number };
type Ops = Record<string, ReactNode>;
type AdminRole = "PlatformAdmin" | "OperationsAdmin";
type AccountRole = "PlatformAdmin" | "OperationsAdmin" | "MerchantAdmin" | "Supervisor" | "Creator" | "Customer" | "Cashier";
type CreateRole = "PlatformAdmin" | "OperationsAdmin" | "MerchantAdmin" | "Cashier" | "Creator" | "Customer";
type PageId =
  | "dashboard"
  | "reports"
  | "creator-review"
  | "business-review"
  | "promotions"
  | "ugc"
  | "admin-accounts"
  | "password-reset-requests"
  | "business-accounts"
  | "creator-accounts"
  | "customer-accounts"
  | "cashier-accounts"
  | "financial-settings"
  | "deposits"
  | "wallets"
  | "payouts"
  | "fraud"
  | "platform-revenue"
  | "audit"
  | "system"
  | "notifications"
  | "disputes"
  | "reversals";

type AdminCounts = {
  pendingCreatorApprovals?: number;
  pendingMerchantApprovals?: number;
  pendingDeposits?: number;
  pendingPayouts?: number;
  openFraudAlerts?: number;
  openSupportRequests?: number;
};

type NavItem = {
  id: PageId;
  label: string;
  roles: AdminRole[];
  countKey?: keyof AdminCounts;
  icon: NavIconName;
};

type Notice = {
  notificationId: string;
  type: string;
  title: string;
  body: string;
  createdAtUtc: string;
  readAtUtc?: string;
  data?: Record<string, string>;
};

type AccountPageProps = {
  title: string;
  description: string;
  roles: AccountRole[];
  createRoles: CreateRole[];
  columns: string[];
  fixedRole?: CreateRole;
  allowCreate?: boolean;
  allowRemove?: boolean;
  allowBusinessTypeEdit?: boolean;
  showBusinessFilter?: boolean;
  showRoleFilter?: boolean;
  searchPlaceholder?: string;
};

const apiBase = (import.meta.env.VITE_API_URL ?? "").replace(/\/$/, "");
const token = getAccessToken;
const adminRoles: AdminRole[] = ["PlatformAdmin", "OperationsAdmin"];
const hiddenPages: PageId[] = ["disputes", "reversals"];
const operationsBlockedPages: PageId[] = ["dashboard", "reports", "admin-accounts", "financial-settings", "platform-revenue", "audit", "system"];
const supportedPages = new Set<PageId>([
  "dashboard",
  "reports",
  "creator-review",
  "business-review",
  "promotions",
  "ugc",
  "admin-accounts",
  "password-reset-requests",
  "business-accounts",
  "creator-accounts",
  "customer-accounts",
  "cashier-accounts",
  "financial-settings",
  "deposits",
  "wallets",
  "payouts",
  "fraud",
  "platform-revenue",
  "audit",
  "system",
  ...hiddenPages,
]);

const platformNav: NavItem[] = [
  { id: "dashboard", label: "Dashboard", roles: ["PlatformAdmin"], icon: "home" },
  { id: "reports", label: "Reports", roles: ["PlatformAdmin"], icon: "sales" },
  { id: "creator-review", label: "Creator Review", roles: adminRoles, countKey: "pendingCreatorApprovals", icon: "userPlus" },
  { id: "business-review", label: "Business Review", roles: adminRoles, countKey: "pendingMerchantApprovals", icon: "ads" },
  { id: "promotions", label: "Promotions", roles: adminRoles, icon: "ads" },
  { id: "ugc", label: "UGC", roles: adminRoles, icon: "video" },
  { id: "admin-accounts", label: "Admin Accounts", roles: ["PlatformAdmin"], icon: "shield" },
  { id: "password-reset-requests", label: "Password Reset Requests", roles: adminRoles, countKey: "openSupportRequests", icon: "requests" },
  { id: "business-accounts", label: "Business Accounts", roles: adminRoles, icon: "checkout" },
  { id: "creator-accounts", label: "Creator Accounts", roles: adminRoles, icon: "creators" },
  { id: "customer-accounts", label: "Customer Accounts", roles: adminRoles, icon: "profile" },
  { id: "cashier-accounts", label: "Cashier Accounts", roles: adminRoles, icon: "cashier" },
  { id: "financial-settings", label: "Financial Settings", roles: ["PlatformAdmin"], icon: "settings" },
  { id: "deposits", label: "Deposits / Funding", roles: adminRoles, countKey: "pendingDeposits", icon: "wallet" },
  { id: "wallets", label: "Wallets", roles: adminRoles, icon: "wallet" },
  { id: "payouts", label: "Payouts", roles: adminRoles, countKey: "pendingPayouts", icon: "payout" },
  { id: "platform-revenue", label: "Platform Revenue", roles: ["PlatformAdmin"], icon: "sales" },
  { id: "fraud", label: "Fraud", roles: adminRoles, countKey: "openFraudAlerts", icon: "shield" },
  { id: "notifications", label: "Notifications", roles: adminRoles, icon: "notifications" },
  { id: "system", label: "System", roles: ["PlatformAdmin"], icon: "settings" },
  { id: "audit", label: "Audit / System", roles: ["PlatformAdmin"], icon: "shield" },
];

const dashboardShortcuts: Record<string, { href: string; icon: NavIconName }> = {
  pendingCreatorApprovals: { href: "/admin/creator-review", icon: "userPlus" },
  pendingMerchantApprovals: { href: "/admin/business-review", icon: "ads" },
  pendingDeposits: { href: "/admin/deposits", icon: "wallet" },
  activeCreators: { href: "/admin/creator-accounts?status=Active", icon: "creators" },
  activeBusinesses: { href: "/admin/business-accounts?status=Active", icon: "checkout" },
  pendingPayouts: { href: "/admin/payouts", icon: "payout" },
  openFraudAlerts: { href: "/admin/fraud", icon: "shield" },
  failedCheckouts: { href: "/admin/fraud", icon: "checkout" },
  systemHealth: { href: "/admin/system", icon: "settings" },
};

async function api<T>(path: string): Promise<T> {
  const response = await fetch(`${apiBase}${path}`, {
    credentials: "include",
    headers: { Authorization: `Bearer ${token()}` },
  });
  if (handleUnauthorized(response.status)) throw Error("Your session has expired.");
  const body = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw Error(
      response.status === 403
        ? "Platform Admin permission is required."
        : body.detail ?? body.error ?? `Request failed (${response.status}).`,
    );
  }
  return body as T;
}

const notificationFallback = "This section is temporarily unavailable.";

async function notificationApi<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBase}${path}`, {
    ...init,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token()}`,
      ...init?.headers,
    },
  });
  if (response.status === 204) return undefined as T;
  const contentType = response.headers.get("content-type")?.toLowerCase() ?? "";
  if (!contentType.includes("json")) throw new Error(notificationFallback);
  const body = await response.json().catch(() => ({}));
  if (!response.ok) {
    const detail = typeof body === "object" && body !== null && "detail" in body && typeof body.detail === "string" ? body.detail : null;
    throw new Error(response.status >= 500 ? notificationFallback : detail ?? "We could not complete that request.");
  }
  return body as T;
}

function currentRole(): AdminRole | "" {
  const external = getExternalSession();
  if (external?.role === "PlatformAdmin" || external?.role === "OperationsAdmin") return external.role;
  try {
    const payload = JSON.parse(
      atob(token().split(".")[1].replace(/-/g, "+").replace(/_/g, "/")),
    );
    const value =
      payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ??
      payload.role;
    return value === "PlatformAdmin" || value === "OperationsAdmin" ? value : "";
  } catch {
    return "";
  }
}

function notificationTarget(notice: Notice) {
  return notice.data?.TargetPath ?? notice.data?.targetPath;
}

function authorizedNotificationTarget(notice: Notice, role: AdminRole) {
  const target = notificationTarget(notice);
  if (!target) return undefined;
  try {
    const url = new URL(target, location.origin);
    if (url.origin !== location.origin || !url.pathname.startsWith("/admin/")) return undefined;
    const page = url.pathname.split("/")[2] as PageId;
    const route = platformNav.find((item) => item.id === page);
    if (!route || !route.roles.includes(role)) return undefined;
    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return undefined;
  }
}

function notificationIcon(type: string) {
  return type === "SupportRequestReceived" ? "!" : "•";
}

export function AdminPortal({ operations }: { operations: Ops }) {
  const role = currentRole();
  const [summary, setSummary] = useState<AdminCounts>();
  const [menuOpen, setMenuOpen] = useState(false);
  const part = location.pathname.split("/")[2] ?? "dashboard";
  const page = supportedPages.has(part as PageId)
    ? (part as PageId)
    : role === "PlatformAdmin"
      ? "dashboard"
      : "creator-review";
  const route = platformNav.find((item) => item.id === page);
  const blockedByRole = role === "OperationsAdmin" && operationsBlockedPages.includes(page);
  const denied = !!role && !!route && !route.roles.includes(role);

  useEffect(() => {
    if (blockedByRole) location.replace("/admin/creator-review");
  }, [blockedByRole]);

  useEffect(() => {
    if (role !== "PlatformAdmin") return;
    let cancelled = false;
    const load = () =>
      api<AdminCounts>("/api/v1/admin/dashboard/summary")
        .then((value) => {
          if (!cancelled) setSummary(value);
        })
        .catch(() => {
          if (!cancelled) setSummary(undefined);
        });
    void load();
    const refresh = () => void load();
    const timer = window.setInterval(refresh, 60_000);
    window.addEventListener("focus", refresh);
    window.addEventListener("online", refresh);
    document.addEventListener("visibilitychange", refresh);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
      window.removeEventListener("focus", refresh);
      window.removeEventListener("online", refresh);
      document.removeEventListener("visibilitychange", refresh);
    };
  }, [role]);

  if (!role) {
    return (
      <main className="auth">
        <section className="panel">
          <h1>Authentication required</h1>
          <p>This dashboard requires an administrator account.</p>
        </section>
      </main>
    );
  }

  if (role !== "PlatformAdmin" && role !== "OperationsAdmin") {
    return (
      <main className="auth">
        <section className="panel">
          <h1>Permission denied</h1>
          <p>This dashboard requires the PlatformAdmin or OperationsAdmin role.</p>
        </section>
      </main>
    );
  }

  const navRoleLabel = role === "PlatformAdmin" ? "Platform" : "Operations";
  const visibleNav = platformNav.filter((item) => (role === "PlatformAdmin" || item.roles.includes(role))
    && (!isExternalSession() || item.id !== "password-reset-requests"));
  const externalAuthPage = isExternalSession() && page === "password-reset-requests";

  return (
    <div className={`admin-shell admin-shell--${role === "PlatformAdmin" ? "platform" : "operations"}`}>
      <a className="skip" href="#admin-content">
        Skip to content
      </a>
      {menuOpen && <button className="admin-menu-scrim" aria-label="Close admin menu" onClick={() => setMenuOpen(false)} />}
      <aside className={`admin-sidebar${menuOpen ? " is-open" : ""}`} id="admin-navigation">
        <div className="admin-sidebar-heading">
          <a className="admin-brand" href={role === "PlatformAdmin" ? "/admin/dashboard" : "/admin/creator-review"}>
            WEYMELA <small>{navRoleLabel} Admin</small>
          </a>
          <button type="button" className="admin-menu-close" aria-label="Close admin menu" onClick={() => setMenuOpen(false)}>
            <NavIcon name="close" />
          </button>
        </div>
        <nav aria-label="Admin navigation">
          {visibleNav.map((item) => {
            const count = item.countKey ? Math.max(0, Number(summary?.[item.countKey] ?? 0)) : 0;
            const aria = count > 0 ? `${count} pending ${item.label} requiring attention` : item.label;
            return (
              <a
                key={item.id}
                href={`/admin/${item.id}`}
                aria-current={item.id === page ? "page" : undefined}
                aria-label={aria}
                onClick={() => setMenuOpen(false)}
              >
                <span className="admin-nav-label"><NavIcon name={item.icon} />{item.label}</span>
                {count > 0 && (
                  <span className="admin-nav-badge" aria-hidden="true">
                    {count > 99 ? "99+" : count}
                  </span>
                )}
              </a>
            );
          })}
        </nav>
      </aside>
      <div className="admin-main">
        <AdminHeader role={role} showSearch={role === "PlatformAdmin"} onMenu={() => setMenuOpen(true)} />
        <main id="admin-content" tabIndex={-1}>
          {blockedByRole ? null : denied || externalAuthPage ? (
            <Forbidden
              text={
                "This page is unavailable."
              }
            />
          ) : page === "dashboard" ? (
            <ProductAdminDashboard />
          ) : page === "reports" ? (
            <ReportingWorkspace />
          ) : page === "creator-review" ? (
            <CreatorReview />
          ) : page === "business-review" ? (
            <BusinessReview />
          ) : page === "promotions" ? (
            <AdminPromotions />
          ) : page === "ugc" ? (
            <AdminUgc />
          ) : page === "password-reset-requests" ? (
            <PasswordResetRequests />
          ) : page === "admin-accounts" ? (
            <V3AdminAccounts />
          ) : page === "business-accounts" ? (
            <AccountPage
              title="Business Accounts"
              description="Business owners and supervisors."
              roles={["MerchantAdmin", "Supervisor"]}
              createRoles={["MerchantAdmin"]}
              columns={["name", "email", "phone", "businessName", "merchantBusinessType", "isLocked", "lastLoginAtUtc"]}
              fixedRole="MerchantAdmin"
              allowCreate={false}
              allowRemove={false}
              allowBusinessTypeEdit={role === "PlatformAdmin"}
            />
          ) : page === "creator-accounts" ? (
            <AccountPage
              title="Creator Accounts"
              description="Creator accounts and profile access."
              roles={["Creator"]}
              createRoles={["Creator"]}
              columns={["name", "email", "phone", "status", "isLocked", "lastLoginAtUtc"]}
              fixedRole="Creator"
              allowCreate={false}
              allowRemove={false}
            />
          ) : page === "customer-accounts" ? (
            <AccountPage
              title="Customer Accounts"
              description="Customer accounts and wallet access."
              roles={["Customer"]}
              createRoles={["Customer"]}
              columns={["name", "email", "phone", "status", "isLocked", "lastLoginAtUtc"]}
              fixedRole="Customer"
              allowCreate={false}
              allowRemove={false}
            />
          ) : page === "cashier-accounts" ? (
            <AccountPage
              title="Cashier Accounts"
              description="Cashier accounts and assignments."
              roles={["Cashier"]}
              createRoles={["Cashier"]}
              columns={["name", "email", "phone", "businessName", "assignedLocation", "status", "isLocked", "lastLoginAtUtc"]}
              fixedRole="Cashier"
              allowCreate={false}
              allowRemove={false}
            />
          ) : page === "financial-settings" ? (
            <V3FinancialSettings />
          ) : page === "deposits" ? (
            <V3FundingDeposits />
          ) : page === "wallets" ? (
            <V3Wallets />
          ) : page === "payouts" ? (
            <V3Payouts />
          ) : page === "platform-revenue" ? (
            <V3PlatformRevenue />
          ) : page === "fraud" ? (
            operations.fraud ?? <Empty />
          ) : page === "system" ? (
            <SystemStatus />
          ) : page === "audit" ? (
            <V3AuditTrail />
          ) : page === "notifications" ? (
            <V3AdminNotifications />
          ) : hiddenPages.includes(page) ? (
            operations[page] ?? <Empty />
          ) : (
            <Empty />
          )}
        </main>
      </div>
    </div>
  );
}

function CreatorReview() {
  const [items, setItems] = useState<Row[]>([]);
  const [selected, setSelected] = useState<Row>();
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");

  const load = () =>
    api<Row[]>("/api/v1/creators/pending")
      .then(setItems)
      .catch((e: Error) => setMessage(e.message));

  useEffect(() => {
    void load();
  }, []);

  async function open(id: unknown) {
    try {
      setSelected(await api<Row>(`/api/v1/creators/${id}`));
    } catch (e) {
      setMessage((e as Error).message);
    }
  }

  async function decide(action: "approve" | "request-correction" | "reject") {
    if (!selected) return;
    const creatorId = selected.creatorId;
    const needsReason = action !== "approve";
    if (needsReason && !reason.trim()) {
      setMessage("A reason is required.");
      return;
    }

    const response = await fetch(`${apiBase}/api/v1/creators/${action}`, {
      method: "POST",
      credentials: "include",
      headers: {
        Authorization: `Bearer ${token()}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        creatorId,
        reason: reason.trim() || null,
      }),
    });
    const body = await response.json().catch(() => ({}));
    setMessage(response.ok ? "Creator decision saved and audited." : body.detail ?? "Creator decision failed.");

    if (response.ok) {
      setSelected(undefined);
      setReason("");
      load();
    }
  }

  return (
    <section>
      <Title text="Creator Review" />
      {message && <aside role="status">{message}</aside>}
      {selected ? (
        <div className="panel">
          <button className="back" onClick={() => setSelected(undefined)}>
            ← Pending creators
          </button>
          <h2>{String(selected.displayName ?? "")}</h2>
          <div className="review-groups">
            <section className="compact-panel"><h3>Creator profile</h3><dl>
              <div><dt>Legal name</dt><dd>{[selected.firstName,selected.lastName].filter(Boolean).join(" ")}</dd></div>
              <div><dt>Display name</dt><dd>{String(selected.displayName ?? "")}</dd></div>
              {Boolean(selected.city)&&<div><dt>City</dt><dd>{String(selected.city)}</dd></div>}
              {Boolean(selected.contentCategories)&&<div><dt>Content categories</dt><dd>{String(selected.contentCategories)}</dd></div>}
              {Boolean(selected.biography)&&<div><dt>Biography</dt><dd>{String(selected.biography)}</dd></div>}
              <div><dt>Application status</dt><dd>{String(selected.effectiveStatus ?? selected.creatorStatus ?? "Pending approval")}</dd></div>
              {Boolean(selected.effectiveStatusReason)&&<div><dt>Status detail</dt><dd>{String(selected.effectiveStatusReason)}</dd></div>}
            </dl></section>
            <section className="compact-panel"><h3>Account verification</h3><dl>
              <div><dt>Email</dt><dd>{String(selected.email ?? "")}</dd></div>
              <div><dt>Email verified</dt><dd>{selected.isEmailVerified?"Yes":"No"}</dd></div>
              <div><dt>Phone</dt><dd>{String(selected.phoneNumber ?? "")}</dd></div>
              <div><dt>Phone verified</dt><dd>{selected.isPhoneVerified?"Yes":"No"}</dd></div>
            </dl></section>
            <section className="compact-panel"><h3>Social profiles</h3>{Array.isArray(selected.socialProfiles)&&selected.socialProfiles.length>0?<div className="review-social-list">{selected.socialProfiles.map((social:any,index:number)=><article key={`${social.platform}-${index}`}><strong>{String(social.platform)}</strong>{social.profileUrl&&<a href={String(social.profileUrl)} target="_blank" rel="noopener noreferrer">Open profile</a>}<span>Self-reported {Number(social.followerCount??0).toLocaleString()}</span><span>{String(social.verificationStatus??"Not verified")}</span></article>)}</div>:<p>No social profiles supplied.</p>}</section>
          </div>
          <label>
            Decision reason
            <textarea value={reason} onChange={(e) => setReason(e.target.value)} />
          </label>
          <div className="actions">
            <button onClick={() => void decide("approve")}>Approve</button>
            <button className="quiet" onClick={() => void decide("request-correction")}>
              Request correction
            </button>
            <button className="danger" onClick={() => void decide("reject")}>
              Reject
            </button>
          </div>
        </div>
      ) : items.length === 0 ? (
        <Empty />
      ) : (
        <div className="cards">
          {items.map((x) => (
            <article key={String(x.creatorId)}>
              <span>Pending approval</span>
              <strong>{String(x.displayName ?? "")}</strong>
              <p>{String(x.email ?? "")}</p>
              <p>{formatDateTime(String(x.registeredAtUtc))}</p>
              <button onClick={() => void open(x.creatorId)}>Review</button>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function BusinessReview() {
  const [items, setItems] = useState<Row[]>();
  const [selected, setSelected] = useState<Row>();
  const [cashiers, setCashiers] = useState<Row[]>([]);
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");

  const load = () =>
    api<Row[]>("/api/v1/admin/merchants/pending")
      .then(setItems)
      .catch((e: Error) => setMessage(e.message));

  useEffect(() => {
    void load();
  }, []);

  async function open(id: unknown) {
    try {
      const [merchant, cashierPage] = await Promise.all([
        api<Row>(`/api/v1/admin/merchants/${id}`),
        api<Page>(`/api/v1/admin/merchants/${id}/cashiers?page=1&pageSize=100`),
      ]);
      setSelected(merchant);
      setCashiers(cashierPage.items);
    } catch (e) {
      setMessage((e as Error).message);
    }
  }

  async function decide(action: string) {
    if (!selected) return;
    const needsReason = action !== "approve" && action !== "reactivate";
    if (needsReason && !reason.trim()) {
      setMessage("A reason is required.");
      return;
    }

    const response = await fetch(`${apiBase}/api/v1/admin/merchants/${action}`, {
      method: "POST",
      credentials: "include",
      headers: {
        Authorization: `Bearer ${token()}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ merchantId: selected.merchantId, reason }),
    });
    const body = await response.json().catch(() => ({}));
    setMessage(response.ok ? "Decision saved and audited." : body.detail ?? "Decision failed.");

    if (response.ok) {
      setSelected(undefined);
      setReason("");
      load();
    }
  }

  return (
    <section>
      <Title text="Business Review" />
      {message && <aside role="status">{message}</aside>}
      {selected ? (
        <div className="panel">
          <button className="back" onClick={() => setSelected(undefined)}>
            ← Pending registrations
          </button>
          <h2>{String(selected.tradingName)}</h2>
          <div className="review-groups">
            <section className="compact-panel"><h3>Business profile</h3><dl>
              <div><dt>Trading Name</dt><dd>{String(selected.tradingName??"")}</dd></div>
              <div><dt>Business Type</dt><dd>{String(selected.businessType??"")}</dd></div>
              <div><dt>Primary Contact Name</dt><dd>{String(selected.primaryContactName??"")}</dd></div>
              <div><dt>Business Address</dt><dd>{String(selected.businessAddress??"")}</dd></div>
              <div><dt>City / Region</dt><dd>{[selected.city,selected.region].filter(Boolean).join(" · ")}</dd></div>
              <div><dt>Application status</dt><dd>{String(selected.effectiveStatus??selected.merchantStatus??"Pending approval")}</dd></div>
              {Boolean(selected.effectiveStatusReason)&&<div><dt>Status detail</dt><dd>{String(selected.effectiveStatusReason)}</dd></div>}
            </dl></section>
            <section className="compact-panel"><h3>Contact and account verification</h3><dl>
              <div><dt>Business Contact Email</dt><dd>{String(selected.email??"")}</dd></div>
              <div><dt>Business Contact Phone</dt><dd>{String(selected.phoneNumber??"")}</dd></div>
              <div><dt>Account email verified</dt><dd>{selected.isEmailVerified?"Yes":"No"}</dd></div>
              <div><dt>Account phone verified</dt><dd>{selected.isPhoneVerified?"Yes":"No"}</dd></div>
            </dl></section>
          </div>
          <h3>Cashiers</h3>
          {cashiers.length === 0 ? (
            <p>No Cashiers yet.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Cashier</th>
                    <th>Email</th>
                    <th>Phone</th>
                    <th>Location</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {cashiers.map((x) => (
                    <tr key={String(x.id)}>
                      <td data-label="Cashier">{cell("cashierName", x.cashierName)}</td>
                      <td data-label="Email">{cell("email", x.email)}</td>
                      <td data-label="Phone">{cell("phone", x.phone)}</td>
                      <td data-label="Location">{cell("assignedLocation", x.assignedLocation)}</td>
                      <td data-label="Status">{cell("status", x.status)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <label>
            Decision reason
            <textarea value={reason} onChange={(e) => setReason(e.target.value)} />
          </label>
          <div className="actions">
            <button onClick={() => void decide("approve")}>Approve</button>
            <button className="quiet" onClick={() => void decide("request-correction")}>
              Request correction
            </button>
            <button className="danger" onClick={() => void decide("reject")}>
              Reject
            </button>
          </div>
        </div>
      ) : !items ? (
        <Loading />
      ) : items.length === 0 ? (
        <Empty />
      ) : (
        <div className="cards">
          {items.map((x) => (
            <article key={String(x.merchantId)} onClick={() => void open(x.merchantId)}>
              <span>Pending approval</span>
              <strong>{String(x.tradingName)}</strong>
              <p>
                {String(x.email)}
                <br />
                {formatDateTime(String(x.registeredAtUtc))}
              </p>
              <button>Review</button>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function AdminHeader({ role, showSearch, onMenu }: { role: AdminRole | ""; showSearch: boolean; onMenu: () => void }) {
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<Notice[]>([]);
  const [unread, setUnread] = useState(0);
  const [message, setMessage] = useState("");
  const adminName = getExternalSession()?.displayName;

  function go(e: FormEvent) {
    e.preventDefault();
    location.href = `/admin/reports?q=${encodeURIComponent(q)}`;
  }

  function logout() {
    if(isExternalSession()){void signOutExternalSession();return}
    clearAuthState();
    location.assign("/");
  }

  useEffect(() => {
    let mounted = true;
    const load = async () => {
      try {
        let list:{items:Notice[]},count:{count:number};
        if(isExternalSession()){
          const page=await v3Request<{items:{id:string;title:string;message:string;route:string;createdAtUtc:string;readAtUtc?:string}[];unreadCount:number}>('/api/notifications');
          list={items:page.items.map(x=>({notificationId:x.id,type:'Weymela',title:x.title,body:x.message,createdAtUtc:x.createdAtUtc,readAtUtc:x.readAtUtc,data:{TargetPath:x.route}}))};count={count:page.unreadCount};
        }else [list,count]=await Promise.all([
          notificationApi<{ items: Notice[] }>("/api/v1/notifications?page=1&pageSize=30"),
          notificationApi<{ count: number }>("/api/v1/notifications/unread-count"),
        ]);
        if (!mounted) return;
        setItems(list.items);
        setUnread(count.count);
        setMessage("");
      } catch (error) {
        console.error(error);
        if (mounted) setMessage("Notifications are temporarily unavailable.");
      }
    };
    void load();
    const refresh = () => void load();
    const timer = window.setInterval(refresh, 60_000);
    window.addEventListener("focus", refresh);
    window.addEventListener("online", refresh);
    document.addEventListener("visibilitychange", refresh);
    return () => {
      mounted = false;
      window.clearInterval(timer);
      window.removeEventListener("focus", refresh);
      window.removeEventListener("online", refresh);
      document.removeEventListener("visibilitychange", refresh);
    };
  }, []);

  async function markRead(notice: Notice) {
    if (!notice.readAtUtc) {
      try {
        if(isExternalSession())await v3Post(`/api/notifications/${notice.notificationId}/read`);
        else await notificationApi(`/api/v1/notifications/${notice.notificationId}/read`, { method: "POST" });
      } catch (error) {
        console.error(error);
        setMessage("Notifications are temporarily unavailable.");
        return;
      }
      setItems((current) =>
        current.map((x) =>
          x.notificationId === notice.notificationId ? { ...x, readAtUtc: new Date().toISOString() } : x,
        ),
      );
      setUnread((current) => Math.max(0, current - 1));
    }
    const target = authorizedNotificationTarget(notice, role as AdminRole);
    setOpen(false);
    if (target) location.assign(target);
  }

  async function markAll() {
    try {
      if(isExternalSession())await v3Post('/api/notifications/read-all');
      else await notificationApi("/api/v1/notifications/read-all", { method: "POST" });
      setItems((current) => current.map((x) => (x.readAtUtc ? x : { ...x, readAtUtc: new Date().toISOString() })));
      setUnread(0);
    } catch (error) {
      console.error(error);
      setMessage("Notifications are temporarily unavailable.");
    }
  }

  return (
    <header className="admin-header">
      <button
        type="button"
        className="admin-menu-toggle"
        aria-label="Open admin menu"
        aria-controls="admin-navigation"
        onClick={onMenu}
      >
        <NavIcon name="menu" />
      </button>
      <div className="admin-mobile-identity" aria-label={`${role === "PlatformAdmin" ? "Platform" : "Operations"} Admin`}>
        <strong>{adminName ?? 'WEYMELA'}</strong>
        <small>{role === "PlatformAdmin" ? "Platform Admin" : "Operations Admin"}</small>
      </div>
      {showSearch && (
        <form role="search" onSubmit={go}>
          <input
            aria-label="Support search"
            minLength={3}
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Search accounts, Businesses, Creators, or reference"
          />
          <button>Search</button>
        </form>
      )}
      <span className="admin-role-label">{adminName&&<strong>{adminName}</strong>}<small>{role === "PlatformAdmin" ? "Platform Admin" : "Operations Admin"}</small></span>
      <div className="account-actions">
        <button
          type="button"
          className="icon-button admin-notification-trigger"
          aria-label={unread > 0 ? `Notifications, ${unread} unread` : "Notifications"}
          aria-expanded={open}
          onClick={() => {
            setOpen((current) => !current);
          }}
        >
          <NavIcon name="notifications" />
          {unread > 0 && <span className="notification-count" aria-hidden="true">{unread > 99 ? "99+" : unread}</span>}
        </button>
        {isExternalSession() && <button className="quiet" onClick={() => void switchExternalProfile()}>Switch profile</button>}
        <button className="quiet" onClick={logout}>
          Sign out
        </button>
      </div>
      {open && (
        <>
          <button className="drawer-scrim admin-notification-scrim" aria-label="Close notifications" onClick={() => setOpen(false)} />
          <aside className="notification-drawer" aria-label="Notifications">
            <div className="notification-drawer-header">
              <h2>Notifications</h2>
              <button className="icon-button admin-notification-close" aria-label="Close notifications" onClick={() => setOpen(false)}>
                <NavIcon name="close" />
              </button>
            </div>
            {message && <p className="friendly-error" role="alert">{message}</p>}
            {items.length === 0 && !message ? (
              <p className="compact-empty">No notifications yet.</p>
            ) : (
              <div className="notification-list">
                {items.map((notice) => (
                  <button
                    type="button"
                    className={`notification-item${notice.readAtUtc ? "" : " unread"}`}
                    key={notice.notificationId}
                    onClick={() => void markRead(notice)}
                  >
                    <span className="notification-type-icon" aria-hidden="true">
                      {notificationIcon(notice.type)}
                    </span>
                    <span>
                      <strong>{notice.title}</strong>
                      <small>{formatUserFacingText(notice.body)}</small>
                      <time dateTime={notice.createdAtUtc}>
                        {formatDateTime(notice.createdAtUtc)}
                      </time>
                    </span>
                    {!notice.readAtUtc && <i aria-label="Unread" />}
                  </button>
                ))}
              </div>
            )}
            {unread > 0 && (
              <button className="mark-all-read" onClick={() => void markAll()}>
                Mark all as read
              </button>
            )}
          </aside>
        </>
      )}
    </header>
  );
}

function Title({ text, children }: { text: string; children?: ReactNode }) {
  return (
    <div className="admin-title">
      <div>
        <p className="eyebrow">{currentRole() === "OperationsAdmin" ? "Operations Admin" : "Platform Admin"}</p>
        <h1>{text}</h1>
      </div>
      {children}
    </div>
  );
}

function Dashboard({ summary }: { summary?: AdminCounts }) {
  const [data, setData] = useState<Row>();
  const [error, setError] = useState("");
  const [days, setDays] = useState("0");

  useEffect(() => {
    const to = new Date();
    const from = new Date();
    if (days === "0") from.setHours(0, 0, 0, 0);
    else from.setDate(to.getDate() - Number(days));
    api<Row>(`/api/v1/admin/dashboard/summary?from=${from.toISOString()}&to=${to.toISOString()}`)
      .then(setData)
      .catch((e) => setError(e.message));
  }, [days]);

  const visible = [
    "pendingCreatorApprovals",
    "pendingMerchantApprovals",
    "pendingDeposits",
    "activeCreators",
    "activeBusinesses",
    "pendingPayouts",
    "openFraudAlerts",
    "failedCheckouts",
    "systemHealth",
  ];

  return (
    <section>
      <Title text="Dashboard">
        <label>
          Time period
          <select value={days} onChange={(e) => setDays(e.target.value)}>
            <option value="0">Today</option>
            <option value="7">This week</option>
            <option value="30">This month</option>
          </select>
        </label>
      </Title>
      {error ? (
        <ErrorBox text={error} />
      ) : !data ? (
        <Loading />
      ) : (
        <>
          <PilotTestActorPanel />
          <div className="summary-grid">
            {visible.filter((k) => data[k] !== undefined).map((k) => {
              const shortcut = dashboardShortcuts[k];
              return (
                <a className="summary-card admin-metric-card" key={k} href={shortcut.href} aria-label={`${label(k)}: ${String(data[k])}`}>
                  <span className="admin-metric-icon"><NavIcon name={shortcut.icon} /></span>
                  <span>{label(k)}</span>
                  <strong>{String(data[k])}</strong>
                  <span className="admin-metric-chevron" aria-hidden="true">›</span>
                </a>
              );
            })}
          </div>
        </>
      )}
    </section>
  );
}

function PilotTestActorPanel() {
  const [result, setResult] = useState<{
    batchId: string;
    label: string;
    actors?: Array<{ role: string; name: string; publicId: string; businessId?: string }>;
    temporaryCredentials?: Record<string, string>;
  }>();
  const [batchId, setBatchId] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const enabled = apiBase.includes("api-pilot.");
  if (!enabled) return null;

  async function create() {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      setResult(await authenticatedApi<typeof result>("/api/v1/admin/pilot-test-actors", { method: "POST", body: "{}" }));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Unable to create PILOT test actors.");
    } finally {
      setBusy(false);
    }
  }

  async function rotate() {
    if (busy || !batchId.trim()) return;
    setBusy(true);
    setError("");
    try {
      setResult(await authenticatedApi<typeof result>(`/api/v1/admin/pilot-test-actors/${batchId.trim()}/rotate-credentials`, { method: "POST" }));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Unable to rotate PILOT test credentials.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel admin-pilot-actors" aria-labelledby="pilot-actors-title">
      <h2 id="pilot-actors-title">PILOT test actors</h2>
      <p>Create one disposable, labeled batch for authenticated PILOT testing. Credentials are returned only to the protected response.</p>
      <button onClick={() => void create()} disabled={busy}>
        {busy ? "Creating…" : "Create PILOT Test Actors"}
      </button>
      <label>
        Existing batch ID
        <input value={batchId} onChange={(e) => setBatchId(e.target.value)} placeholder="Paste the PILOT-E2E batch ID" inputMode="text" />
      </label>
      <button className="quiet" onClick={() => void rotate()} disabled={busy || !batchId.trim()}>
        {busy ? "Rotating…" : "Rotate PILOT Test Credentials"}
      </button>
      {error && <p className="error" role="alert">{error}</p>}
      {result && (
        <div role="status">
          <p>
            <strong>{result.label}</strong>
            <br />
            Batch ID: <code>{result.batchId}</code>
          </p>
          {result.actors && (
            <ul>
              {result.actors.map((actor) => (
                <li key={`${actor.role}-${actor.publicId}`}>
                  {actor.role}: {actor.name} ({actor.publicId}){actor.businessId ? ` · Business ${actor.businessId}` : ""}
                </li>
              ))}
            </ul>
          )}
          {result.temporaryCredentials && (
            <>
              <p>Temporary credentials are shown only in this current session. Do not copy them into logs or source control.</p>
              <pre>{Object.entries(result.temporaryCredentials).map(([email, password]) => `${email}: ${password}`).join("\n")}</pre>
            </>
          )}
        </div>
      )}
    </section>
  );
}

function Table({ title, endpoint }: { title: string; endpoint: string }) {
  const [data, setData] = useState<Page>();
  const [error, setError] = useState("");
  const [page, setPage] = useState(1);
  const [input, setInput] = useState("");
  const [query, setQuery] = useState("");
  useTypeahead(input, async (term) => term, setQuery);

  useEffect(() => {
    api<Page>(`/api/v1/admin/${endpoint}?page=${page}&pageSize=25${query ? `&q=${encodeURIComponent(query)}` : ""}`)
      .then((value) => setData({ ...value, items: rankMatches(value.items, query, (row) => Object.values(row).map(String)) }))
      .catch((e) => setError(e.message));
  }, [endpoint, page, query]);

  function filter(e: FormEvent) {
    e.preventDefault();
    setPage(1);
    setQuery(input);
  }

  const cols = data?.items[0] ? Object.keys(data.items[0]).filter((x) => x !== "id").slice(0, 7) : [];

  return (
    <section>
      <Title text={title}>
        <form className="filter" onSubmit={filter}>
          <input type="search" aria-label="Filter results" value={input} onChange={(e) => setInput(e.target.value)} placeholder="Type 2+ characters to filter" />
          <button>Apply</button>
        </form>
      </Title>
      {error ? (
        <ErrorBox text={error} />
      ) : !data ? (
        <Loading />
      ) : !data.items.length ? (
        <Empty />
      ) : (
        <>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>{cols.map((c) => <th scope="col" key={c}>{label(c)}</th>)}</tr>
              </thead>
              <tbody>
                {data.items.map((row, i) => (
                  <tr key={String(row.id ?? i)}>
                    {cols.map((c) => <td key={c} data-label={label(c)}>{cell(c, row[c])}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <nav className="pagination" aria-label="Pagination">
            <button disabled={page === 1} onClick={() => setPage(page - 1)}>Previous</button>
            <span>Page {page} of {Math.max(1, data.totalPages)} · {data.total} records</span>
            <button disabled={page >= data.totalPages} onClick={() => setPage(page + 1)}>Next</button>
          </nav>
        </>
      )}
    </section>
  );
}

function AccountPage({
  title,
  description,
  roles,
  createRoles,
  columns,
  fixedRole,
  allowCreate = false,
  allowRemove = true,
  allowBusinessTypeEdit = false,
  showBusinessFilter = true,
  showRoleFilter = false,
  searchPlaceholder = "Name, email, phone, or Business",
}: AccountPageProps) {
  const requestedStatus = new URLSearchParams(location.search).get("status") ?? "";
  const [data, setData] = useState<Page>();
  const [message, setMessage] = useState("");
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState({ q: "", status: requestedStatus, business: "", role: "" });
  const [editing, setEditing] = useState<{ merchantId: string; name: string; businessType: string } | null>(null);
  const [editingBusinessType, setEditingBusinessType] = useState("Other");
  const [savingBusinessType, setSavingBusinessType] = useState(false);
  const pageSize = 25;

  const load = async (nextPage = 1, nextFilters = filters) => {
    const params = new URLSearchParams({ page: "1", pageSize: "100" });
    if (nextFilters.q) params.set("q", nextFilters.q);
    if (nextFilters.status) params.set("status", nextFilters.status);
    if (showBusinessFilter && nextFilters.business) params.set("business", nextFilters.business);
    const rolesToLoad = showRoleFilter && nextFilters.role ? [nextFilters.role as AccountRole] : roles;
    try {
      const responses = await Promise.all(
        rolesToLoad.map((roleName) => api<Page>(`/api/v1/admin/accounts?${params.toString()}&role=${encodeURIComponent(roleName)}`)),
      );
      const items = responses
        .flatMap((x) => x.items)
        .sort((a, b) => new Date(String(b.createdAtUtc ?? b.lastLoginAtUtc ?? 0)).getTime() - new Date(String(a.createdAtUtc ?? a.lastLoginAtUtc ?? 0)).getTime());
      const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
      const safePage = Math.min(Math.max(1, nextPage), totalPages);
      setPage(safePage);
      setData({
        ...responses[0],
        items: items.slice((safePage - 1) * pageSize, safePage * pageSize),
        page: safePage,
        total: items.length,
        totalPages,
      });
    } catch (e) {
      setMessage((e as Error).message);
    }
  };

  useEffect(() => {
    void load(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [roles.join(",")]);

  async function action(id: unknown, name: string) {
    if (!confirm(`${label(name)} this account?`)) return;
    const reason = prompt("Reason for this account change")?.trim();
    if (!reason) return;
    const response = await fetch(`${apiBase}/api/v1/admin/accounts/${id}/${name}`, {
      method: "POST",
      credentials: "include",
      headers: {
        Authorization: `Bearer ${token()}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ reason }),
    });
    if (!response.ok) {
      setMessage("Unable to change this account.");
      return;
    }
    setMessage(name === "delete" ? "Account deleted and anonymized." : "Account updated and audited.");
    await load(page, filters);
  }

  async function saveBusinessType() {
    if (!editing || savingBusinessType) return;
    if (!editingBusinessType.trim()) {
      setMessage("Business type is required.");
      return;
    }
    setSavingBusinessType(true);
    try {
      const response = await fetch(`${apiBase}/api/v1/admin/merchants/${editing.merchantId}/business-type`, {
        method: "PUT",
        credentials: "include",
        headers: {
          Authorization: `Bearer ${token()}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ businessType: editingBusinessType }),
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) {
        setMessage((body as { detail?: string }).detail ?? "Unable to update business type.");
        return;
      }
      setMessage("Business Type updated.");
      setEditing(null);
      await load(page, filters);
    } finally {
      setSavingBusinessType(false);
    }
  }

  const visibleCols = columns;
  const header = (value: string) =>
    value === "cashierName" || value === "name"
      ? "Name"
      : value === "merchantBusinessType"
        ? "Business Type"
        : label(value);

  return (
    <section>
      <Title text={title} />
      <p className="accounts-note">{description}</p>
      {allowCreate && (
        <AdminAccountCreate
          title="Create Account"
          description={`Create a new ${title.toLowerCase().replace(" accounts", "")}.`}
          roles={createRoles}
          fixedRole={fixedRole}
          onCreated={() => void load(1, filters)}
        />
      )}
      {editing && (
        <div className="modal-backdrop">
          <section
            className="help-dialog admin-business-type-editor"
            role="dialog"
            aria-modal="true"
            aria-labelledby="business-type-editor-title"
          >
            <h2 id="business-type-editor-title">Edit Business Type</h2>
            <p>
              <strong>{editing.name}</strong>
            </p>
            <p>Current Business Type: {editing.businessType}</p>
            <label>
              New Business Type
              <select value={editingBusinessType} onChange={(e) => setEditingBusinessType(e.target.value)}>
                {businessTypes.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.en}
                  </option>
                ))}
              </select>
            </label>
            <div className="actions">
              <button type="button" onClick={() => void saveBusinessType()} disabled={savingBusinessType}>
                {savingBusinessType ? "Saving…" : "Save"}
              </button>
              <button
                type="button"
                className="quiet"
                onClick={() => {
                  setEditing(null);
                  setEditingBusinessType("Other");
                }}
              >
                Cancel
              </button>
            </div>
          </section>
        </div>
      )}
      <form
        className="panel form account-filters"
        onSubmit={(e) => {
          e.preventDefault();
          void load(1, filters);
        }}
      >
        <label>
          General Search
          <input type="search" value={filters.q} onChange={(e) => setFilters({ ...filters, q: e.target.value })} placeholder={searchPlaceholder} />
        </label>
        <label>
          Status
          <select value={filters.status} onChange={(e) => setFilters({ ...filters, status: e.target.value })}>
            <option value="">All statuses</option>
            {["Active", "PendingVerification", "Suspended", "Closed", "Locked"].map((x) => (
              <option key={x} value={x}>
                {x === "Closed" ? "Deactivated" : x}
              </option>
            ))}
          </select>
        </label>
        {showRoleFilter && (
          <label>
            Role
            <select value={filters.role} onChange={(e) => setFilters({ ...filters, role: e.target.value })}>
              <option value="">All Roles</option>
              <option value="PlatformAdmin">Platform Admin</option>
              <option value="OperationsAdmin">Operations Admin</option>
            </select>
          </label>
        )}
        {showBusinessFilter && (
          <label>
            Business
                <input value={filters.business} onChange={(e) => setFilters({ ...filters, business: e.target.value })} placeholder="Business name" />
          </label>
        )}
        <div className="actions">
          <button>Search</button>
          <button
            type="button"
            className="quiet"
            onClick={() => {
              const next = { q: "", status: "", business: "", role: "" };
              setFilters(next);
              void load(1, next);
            }}
          >
            Clear Filters
          </button>
        </div>
      </form>
      {message && <aside role="status">{message}</aside>}
      {!data ? (
        <Loading />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                {visibleCols.map((x) => <th key={x}>{header(x)}</th>)}
                <th><span className="sr-only">Action</span></th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((x) => (
                <tr key={String(x.id)}>
                  {visibleCols.map((c) => <td key={c} data-label={header(c)}>{cell(c, x[c])}</td>)}
                  <td data-label="Action">
                    <div className="actions">
                      {x.isLocked ? (
                        <button onClick={() => void action(x.id, "unlock")}>Unlock</button>
                      ) : (
                        <button className="quiet" onClick={() => void action(x.id, "lock")}>
                          Lock
                        </button>
                      )}
                      {allowRemove && x.role !== "PlatformAdmin" && (
                        <button type="button" className="danger" onClick={() => void action(x.id, "delete")}>
                          Delete Account
                        </button>
                      )}
                      {allowBusinessTypeEdit && Boolean(x.merchantId) && (
                        <button
                          type="button"
                          className="quiet"
                          onClick={() => {
                            setEditing({
                              merchantId: String(x.merchantId),
                              name: String(x.name ?? x.businessName ?? "Business"),
                              businessType: String(x.merchantBusinessType ?? "Other"),
                            });
                            setEditingBusinessType(String(x.merchantBusinessType ?? "Other"));
                          }}
                        >
                          Edit Business Type
                        </button>
                      )}
                      {x.status === "Active" ? (
                        x.role === "Cashier" ? (
                          <button type="button" className="danger" onClick={() => void action(x.id, "deactivate")}>
                            Disable Cashier
                          </button>
                        ) : (
                          <>
                            <button type="button" className="quiet" onClick={() => void action(x.id, "suspend")}>
                              Suspend
                            </button>
                            <button type="button" className="danger" onClick={() => void action(x.id, "deactivate")}>
                              Deactivate
                            </button>
                          </>
                        )
                      ) : ["Suspended", "Closed"].includes(String(x.status)) ? (
                        <button type="button" onClick={() => void action(x.id, "reactivate")}>
                          {x.role === "Cashier" ? "Reactivate Cashier" : "Reactivate"}
                        </button>
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {data && (
        <nav className="pagination" aria-label={`${title} pagination`}>
          <button disabled={page === 1} onClick={() => void load(page - 1, filters)}>
            Previous
          </button>
          <span>
            Page {page} of {data.totalPages} · {data.total} records
          </span>
          <button disabled={page >= data.totalPages} onClick={() => void load(page + 1, filters)}>
            Next
          </button>
        </nav>
      )}
    </section>
  );
}

function AdminAccountsPage() {
  return (
    <AccountPage
      title="Admin Accounts"
      description="Platform and Operations administrator accounts."
      roles={["PlatformAdmin", "OperationsAdmin"]}
      createRoles={["PlatformAdmin", "OperationsAdmin"]}
      columns={["name", "email", "phone", "role", "status", "isLocked", "lastLoginAtUtc"]}
      allowCreate
      allowRemove={false}
      showBusinessFilter={false}
      showRoleFilter
      searchPlaceholder="Name, email, phone or admin ID"
    />
  );
}

function PasswordResetRequests() {
  const [items, setItems] = useState<Row[]>([]);
  const [message, setMessage] = useState("");

  const load = () =>
    api<Row[]>("/api/v1/admin/password-reset-requests")
      .then(setItems)
      .catch((e: Error) => setMessage(e.message));

  useEffect(() => {
    void load();
  }, []);

  async function review(id: unknown, action: "approve" | "reject") {
    if (!confirm(`${action === "approve" ? "Authorize" : "Reject"} this password reset request?`)) return;
    const response = await fetch(`${apiBase}/api/v1/admin/password-reset-requests/${id}/${action}`, {
      method: "POST",
      credentials: "include",
      headers: { Authorization: `Bearer ${token()}` },
    });
    if (!response.ok) {
      setMessage("Unable to review this request.");
      return;
    }
    setMessage(action === "approve" ? "Reset authorized. The user can now create a new password." : "Reset request rejected.");
    void load();
  }

  async function deleteRequest(id: unknown) {
    const reason = prompt("Reason for deleting this password reset request")?.trim();
    if (!reason) return;
    const response = await fetch(`${apiBase}/api/v1/admin/password-reset-requests/${id}/delete`, {
      method: "POST",
      credentials: "include",
      headers: {
        Authorization: `Bearer ${token()}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ reason }),
    });
    if (!response.ok) {
      const body = await response.json().catch(() => ({}));
      setMessage((body as { detail?: string }).detail ?? "Unable to delete this request.");
      return;
    }
    setMessage("Password reset request deleted.");
    void load();
  }

  return (
    <section id="password-reset-requests" className="admin-reset-requests">
      <h2>Password Reset Requests</h2>
      {message && <aside role="status">{message}</aside>}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>{["Name", "Phone", "Role", "Requested At"].map((x) => <th key={x}>{x}</th>)}<th><span className="sr-only">Status</span></th><th><span className="sr-only">Action</span></th></tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={6}>No password reset requests.</td>
              </tr>
            ) : (
              items.map((x) => (
                <tr key={String(x.id)}>
                  <td data-label="Name">{cell("name", x.name)}</td>
                  <td data-label="Phone">{cell("phone", x.phone)}</td>
                  <td data-label="Role">{cell("role", x.role)}</td>
                  <td data-label="Requested At">{cell("requestedAtUtc", x.requestedAtUtc)}</td>
                  <td data-label="Status">{cell("status", x.status)}</td>
                  <td data-label="Action">
                    <div className="actions">
                      {x.canApprove === true && (
                        <>
                          <button onClick={() => void review(x.id, "approve")}>Authorize Reset</button>
                          <button className="danger" onClick={() => void review(x.id, "reject")}>
                            Reject
                          </button>
                        </>
                      )}
                      {x.canDelete === true && (
                        <button className="quiet" onClick={() => void deleteRequest(x.id)}>
                          Delete Request
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function SystemStatus() {
  const [data, setData] = useState<Row>();
  const [error, setError] = useState("");
  useEffect(() => {
    api<Row>("/api/v1/admin/system").then(setData).catch((e) => setError(e.message));
  }, []);
  return (
    <section>
      <Title text="System" />
      {error ? (
        <ErrorBox text={error} />
      ) : !data ? (
        <Loading />
      ) : (
        <div className="summary-grid">
          <article className="summary-card">
            <span>API Health</span>
            <strong>{String(data.health ?? "Unknown")}</strong>
          </article>
          <article className="summary-card">
            <span>Database Health</span>
            <strong>{String(data.database ?? "Unknown")}</strong>
          </article>
          <article className="summary-card">
            <span>Worker Health</span>
            <strong>{String((data.worker as Row | undefined)?.status ?? "Unknown")}</strong>
          </article>
          <article className="summary-card">
            <span>Web Health</span>
            <strong>Available</strong>
          </article>
          <article className="summary-card">
            <span>Deployment Version</span>
            <strong>{String(data.version ?? "Unknown")}</strong>
          </article>
        </div>
      )}
    </section>
  );
}

function adminDateTime(value: string) {
  return formatDateTime(value);
}

function cell(k: string, v: unknown) {
  if (v == null) return "—";
  if (k.toLowerCase().includes("status")) return <span className="status">{String(v)}</span>;
  if (k.toLowerCase().includes("correlation")) return <code>{String(v)}</code>;
  if (typeof v === "boolean") return v ? "Yes" : "No";
  if (typeof v === "string" && (k.endsWith("Utc") || k === "lastDeposit")) return adminDateTime(v);
  return String(v);
}

function Loading() {
  return <p aria-live="polite">Loading…</p>;
}

function Empty() {
  return (
    <div className="empty-state">
      <h2>No records found.</h2>
      <p>Try changing the current filters.</p>
    </div>
  );
}

function ErrorBox({ text }: { text: string }) {
  const friendly = /session|permission/i.test(text) ? text : "Unable to load data.";
  return (
    <div className="error-panel" role="alert">
      <h2>Unable to load</h2>
      <p>{friendly}</p>
    </div>
  );
}

function Forbidden({ text }: { text: string }) {
  return (
    <section className="panel">
      <h2>Access restricted</h2>
      <p>{text}</p>
    </section>
  );
}

function label(x: string) {
  return x
    .replace(/-/g, " ")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/^./, (c) => c.toUpperCase());
}
