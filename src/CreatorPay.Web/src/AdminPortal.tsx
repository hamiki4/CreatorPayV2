import { ReactNode, useEffect, useMemo, useState } from "react";
import { ReportingWorkspace } from "./ReportingWorkspace";
import { clearAuthState, handleUnauthorized } from "./authSession";
import { getAccessToken } from "./sessionStore";
import { rankMatches } from "./typeahead";
import { api as authenticatedApi } from "./apiClient";
import { AdminAccountCreate } from "./AdminAccountCreate";

type Row = Record<string, unknown>;
type Page<T = Row> = { items: T[]; page: number; total: number; totalPages: number };
type Ops = Record<string, ReactNode>;
type AdminRole = "PlatformAdmin" | "OperationsAdmin";
type AccountRole = "PlatformAdmin" | "OperationsAdmin" | "MerchantAdmin" | "Creator" | "Customer" | "Cashier";
type PageId =
  | "dashboard"
  | "reports"
  | "creator-review"
  | "business-review"
  | "admin-accounts"
  | "business-accounts"
  | "creator-accounts"
  | "customer-accounts"
  | "cashier-accounts"
  | "commission"
  | "deposits"
  | "wallets"
  | "payouts"
  | "fraud"
  | "system";

type AdminCounts = {
  pendingCreatorApprovals?: number;
  pendingMerchantApprovals?: number;
  pendingDeposits?: number;
  pendingPayouts?: number;
  openFraudAlerts?: number;
};

type NavItem = {
  id: PageId;
  label: string;
  roles: AdminRole[];
  badgeKey?: keyof AdminCounts;
};

const apiBase = (import.meta.env.VITE_API_URL ?? "").replace(/\/$/, "");
const token = getAccessToken;
const adminRoles: AdminRole[] = ["PlatformAdmin", "OperationsAdmin"];

const platformNav: NavItem[] = [
  { id: "dashboard", label: "Dashboard", roles: ["PlatformAdmin"] },
  { id: "reports", label: "Reports", roles: ["PlatformAdmin"] },
  { id: "creator-review", label: "Creator Review", roles: adminRoles, badgeKey: "pendingCreatorApprovals" },
  { id: "business-review", label: "Business Review", roles: adminRoles, badgeKey: "pendingMerchantApprovals" },
  { id: "admin-accounts", label: "Admin Accounts", roles: ["PlatformAdmin"] },
  { id: "business-accounts", label: "Business Accounts", roles: adminRoles },
  { id: "creator-accounts", label: "Creator Accounts", roles: adminRoles },
  { id: "customer-accounts", label: "Customer Accounts", roles: adminRoles },
  { id: "cashier-accounts", label: "Cashier Accounts", roles: adminRoles },
  { id: "commission", label: "Commission", roles: ["PlatformAdmin"] },
  { id: "deposits", label: "Deposits", roles: adminRoles, badgeKey: "pendingDeposits" },
  { id: "wallets", label: "Wallets", roles: adminRoles },
  { id: "payouts", label: "Payouts", roles: adminRoles, badgeKey: "pendingPayouts" },
  { id: "fraud", label: "Fraud", roles: adminRoles, badgeKey: "openFraudAlerts" },
  { id: "system", label: "System", roles: ["PlatformAdmin"] },
];

function currentRole(): AdminRole | "" {
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

function isRecognizedPage(path: string) {
  return platformNav.some((item) => item.id === path);
}

function navForRole(role: AdminRole): NavItem[] {
  return platformNav.filter((item) => item.roles.includes(role));
}

async function api<T>(path: string): Promise<T> {
  const response = await fetch(`${apiBase}${path}`, {
    headers: { Authorization: `Bearer ${token()}` },
  });
  if (handleUnauthorized(response.status)) throw Error("Your session has expired.");
  const body = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw Error(
      response.status === 403
        ? "Administrator permission is required."
        : body.detail ?? body.error ?? `Request failed (${response.status}).`,
    );
  }
  return body as T;
}

export function AdminPortal({ operations }: { operations: Ops }) {
  const role = currentRole();
  const [summary, setSummary] = useState<AdminCounts>();
  const path = location.pathname.split("/")[2] ?? "";
  const page = isRecognizedPage(path) ? (path as PageId) : role === "PlatformAdmin" ? "dashboard" : "creator-review";
  const denied = !!role && isRecognizedPage(path) && !navForRole(role).some((item) => item.id === path);

  useEffect(() => {
    if (role !== "PlatformAdmin") return;
    let cancelled = false;
    api<AdminCounts>("/api/v1/admin/dashboard/summary")
      .then((value) => {
        if (!cancelled) setSummary(value);
      })
      .catch(() => {
        if (!cancelled) setSummary(undefined);
      });
    const refresh = () => {
      void api<AdminCounts>("/api/v1/admin/dashboard/summary")
        .then((value) => {
          if (!cancelled) setSummary(value);
        })
        .catch(() => undefined);
    };
    const timer = window.setInterval(refresh, 12_000);
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

  return (
    <div className="admin-shell">
      <style>{`
        .admin-sidebar nav a {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: .6rem;
        }
        .admin-nav-badge {
          display: inline-grid;
          place-items: center;
          min-width: 1.35rem;
          height: 1.35rem;
          padding: 0 .35rem;
          border-radius: 999px;
          background: #b42318;
          color: #fff;
          font-size: .72rem;
          font-weight: 800;
          line-height: 1;
        }
      `}</style>
      <a className="skip" href="#admin-content">
        Skip to content
      </a>
      <aside className="admin-sidebar">
        <a className="admin-brand" href={role === "PlatformAdmin" ? "/admin/dashboard" : "/admin/creator-review"}>
          Weymela <small>{role === "PlatformAdmin" ? "Platform" : "Operations"}</small>
        </a>
        <nav aria-label="Admin navigation">
          {navForRole(role).map((item) => {
            const count = item.badgeKey ? Math.max(0, Number(summary?.[item.badgeKey] ?? 0)) : 0;
            const label = item.label;
            const ariaLabel = count > 0 ? `${count} pending ${label}` : label;
            return (
              <a
                key={item.id}
                href={`/admin/${item.id}`}
                aria-current={item.id === page ? "page" : undefined}
                aria-label={ariaLabel}
              >
                {label}
                {count > 0 && <span className="admin-nav-badge" aria-hidden="true">{count > 99 ? "99+" : count}</span>}
              </a>
            );
          })}
        </nav>
      </aside>
      <div className="admin-main">
        <AdminHeader role={role} />
        <main id="admin-content" tabIndex={-1}>
          {denied ? (
            <Forbidden
              text={
                role === "OperationsAdmin" && path === "dashboard"
                  ? "Dashboard access is restricted to PlatformAdmin."
                  : role === "OperationsAdmin" && path === "reports"
                    ? "Reports access is restricted to PlatformAdmin."
                    : role === "OperationsAdmin" && path === "admin-accounts"
                      ? "Admin Accounts is restricted to PlatformAdmin."
                      : role === "OperationsAdmin" && path === "commission"
                        ? "Commission access is restricted to PlatformAdmin."
                        : role === "OperationsAdmin" && path === "system"
                          ? "System access is restricted to PlatformAdmin."
                          : "This page is unavailable."
              }
            />
          ) : page === "dashboard" ? (
            role === "PlatformAdmin" ? (
              <Dashboard summary={summary} />
            ) : (
              <Forbidden text="Dashboard access is restricted to PlatformAdmin." />
            )
          ) : page === "reports" ? (
            role === "PlatformAdmin" ? (
              <ReportingWorkspace />
            ) : (
              <Forbidden text="Reports access is restricted to PlatformAdmin." />
            )
          ) : page === "creator-review" ? (
            <CreatorReview />
          ) : page === "business-review" ? (
            <BusinessReview />
          ) : page === "admin-accounts" ? (
            role === "PlatformAdmin" ? (
              <AccountPage
                title="Admin Accounts"
                description="PlatformAdmin and OperationsAdmin accounts."
                roles={["PlatformAdmin", "OperationsAdmin"]}
                createRoles={["PlatformAdmin", "OperationsAdmin"]}
                resetRoles={["PlatformAdmin", "OperationsAdmin"]}
                allowCreate
                allowRemove={false}
                accountColumns={["email", "role", "status", "lastLoginAtUtc"]}
              />
            ) : (
              <Forbidden text="Admin Accounts is restricted to PlatformAdmin." />
            )
          ) : page === "business-accounts" ? (
            <AccountPage
              title="Business Accounts"
              description="Business accounts only."
              roles={["MerchantAdmin"]}
              createRoles={["MerchantAdmin"]}
              resetRoles={["MerchantAdmin"]}
              allowCreate={role === "PlatformAdmin"}
              allowRemove={role === "PlatformAdmin"}
              accountColumns={["businessName", "publicBusinessId", "email", "phone", "status", "lastLoginAtUtc", "walletBalance", "fundingStatus"]}
              showBusinessFilter
            />
          ) : page === "creator-accounts" ? (
            <AccountPage
              title="Creator Accounts"
              description="Creator accounts only."
              roles={["Creator"]}
              createRoles={["Creator"]}
              resetRoles={["Creator"]}
              allowCreate={role === "PlatformAdmin"}
              allowRemove={role === "PlatformAdmin"}
              accountColumns={["name", "email", "phone", "status", "lastLoginAtUtc"]}
            />
          ) : page === "customer-accounts" ? (
            <AccountPage
              title="Customer Accounts"
              description="Customer accounts only."
              roles={["Customer"]}
              createRoles={["Customer"]}
              resetRoles={["Customer"]}
              allowCreate={role === "PlatformAdmin"}
              allowRemove={role === "PlatformAdmin"}
              accountColumns={["name", "email", "phone", "status", "lastLoginAtUtc"]}
            />
          ) : page === "cashier-accounts" ? (
            <AccountPage
              title="Cashier Accounts"
              description="Cashier accounts only, with business assignment preserved."
              roles={["Cashier"]}
              createRoles={["Cashier"]}
              resetRoles={["Cashier"]}
              allowCreate={role === "PlatformAdmin"}
              allowRemove={role === "PlatformAdmin"}
              accountColumns={["name", "email", "phone", "businessName", "assignedLocation", "status", "lastLoginAtUtc"]}
              showBusinessFilter
            />
          ) : page === "commission" ? (
            role === "PlatformAdmin" ? (
              <section>
                <Title text="Commission" />
                {operations.commission ?? <Empty text="Commission workspace is unavailable." />}
              </section>
            ) : (
              <Forbidden text="Commission access is restricted to PlatformAdmin." />
            )
          ) : page === "deposits" ? (
            <section>
              <Title text="Deposits" />
              {operations.deposits ?? <Forbidden text="Deposit operations are temporarily unavailable." />}
            </section>
          ) : page === "wallets" ? (
            <section>
              <Title text="Wallets" />
              {operations.wallets ?? <Forbidden text="Wallet operations are temporarily unavailable." />}
            </section>
          ) : page === "payouts" ? (
            <section>
              <Title text="Payouts" />
              {operations.payouts ?? <Forbidden text="Payout operations are temporarily unavailable." />}
            </section>
          ) : page === "fraud" ? (
            <section>
              <Title text="Fraud" />
              {operations.fraud ?? operations.disputes ?? operations.reversals ?? <Forbidden text="Fraud operations are temporarily unavailable." />}
            </section>
          ) : page === "system" ? (
            role === "PlatformAdmin" ? (
              <SystemStatus />
            ) : (
              <Forbidden text="System access is restricted to PlatformAdmin." />
            )
          ) : (
            <Forbidden text="This page is unavailable." />
          )}
        </main>
      </div>
    </div>
  );
}

function AdminHeader({ role }: { role: AdminRole }) {
  function logout() {
    clearAuthState();
    location.assign("/");
  }
  return (
    <header className="admin-header">
      <div>
        <strong>Administrator</strong>
        <span>{role === "PlatformAdmin" ? "PlatformAdmin" : "OperationsAdmin"}</span>
      </div>
      <button className="quiet" onClick={logout}>
        Sign out
      </button>
    </header>
  );
}

function Title({ text, children }: { text: string; children?: ReactNode }) {
  return (
    <div className="admin-title">
      <div>
        <p className="eyebrow">Platform operations</p>
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
            {visible
              .filter((key) => data[key] !== undefined)
              .map((key) => (
                <article className="summary-card" key={key}>
                  <span>{label(key)}</span>
                  <strong>{String(data[key])}</strong>
                </article>
              ))}
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
  if (!apiBase.includes("api-pilot.")) return null;
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
    <section className="panel" aria-labelledby="pilot-actors-title">
      <h2 id="pilot-actors-title">PILOT test actors</h2>
      <p>Create one disposable, labeled batch for authenticated PILOT testing. Credentials are returned only to the protected response.</p>
      <button onClick={() => void create()} disabled={busy}>
        {busy ? "Creating…" : "Create PILOT Test Actors"}
      </button>
      <label>
        Existing batch ID
        <input
          value={batchId}
          onChange={(e) => setBatchId(e.target.value)}
          placeholder="Paste the PILOT-E2E batch ID"
          inputMode="text"
        />
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
      headers: {
        Authorization: `Bearer ${token()}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ creatorId, reason: reason.trim() || null }),
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
          <dl>
            {Object.entries(selected)
              .filter(([key]) => !["profileImage"].includes(key))
              .map(([key, value]) => (
                <div key={key}>
                  <dt>{label(key)}</dt>
                  <dd>
                    {key === "socialProfiles" && Array.isArray(value)
                      ? value.map((social: any, index: number) => (
                          <div key={index}>
                            <strong>{social.platform}</strong>{" "}
                            {social.profileUrl ? (
                              <a href={social.profileUrl} target="_blank" rel="noopener noreferrer">
                                Open social profile
                              </a>
                            ) : null}
                            <br />
                            Followers: {String(social.followerCount ?? 0)}
                            <br />
                            Verification: {String(social.verificationStatus ?? "")}
                          </div>
                        ))
                      : String(value ?? "—")}
                  </dd>
                </div>
              ))}
          </dl>
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
          {items.map((item) => (
            <article key={String(item.creatorId)}>
              <span>Pending Approval</span>
              <strong>{String(item.displayName ?? "")}</strong>
              <p>{String(item.email ?? "")}</p>
              <p>{new Date(String(item.registeredAtUtc)).toLocaleString()}</p>
              <button onClick={() => void open(item.creatorId)}>Review</button>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function BusinessReview() {
  const [items, setItems] = useState<Row[]>([]);
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
          <dl>
            {Object.entries(selected)
              .filter(([key]) => !["documents", "logo"].includes(key))
              .map(([key, value]) => (
                <div key={key}>
                  <dt>{label(key)}</dt>
                  <dd>{String(value ?? "—")}</dd>
                </div>
              ))}
          </dl>
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
                  {cashiers.map((item) => (
                    <tr key={String(item.id)}>
                      <td data-label="Cashier">{cell("cashierName", item.cashierName)}</td>
                      <td data-label="Email">{cell("email", item.email)}</td>
                      <td data-label="Phone">{cell("phone", item.phone)}</td>
                      <td data-label="Location">{cell("assignedLocation", item.assignedLocation)}</td>
                      <td data-label="Status">{cell("status", item.status)}</td>
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
          {items.map((item) => (
            <article key={String(item.merchantId)} onClick={() => void open(item.merchantId)}>
              <span>Pending Review</span>
              <strong>{String(item.tradingName)}</strong>
              <p>
                {String(item.email)}
                <br />
                {new Date(String(item.registeredAtUtc)).toLocaleString()}
              </p>
              <button>Review</button>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function AccountPage({
  title,
  description,
  roles,
  createRoles,
  resetRoles,
  allowCreate,
  allowRemove,
  accountColumns,
  showBusinessFilter = false,
}: {
  title: string;
  description: string;
  roles: AccountRole[];
  createRoles: ("PlatformAdmin" | "OperationsAdmin" | "MerchantAdmin" | "Creator" | "Customer" | "Cashier")[];
  resetRoles: string[];
  allowCreate: boolean;
  allowRemove: boolean;
  accountColumns: string[];
  showBusinessFilter?: boolean;
}) {
  const [data, setData] = useState<Page>();
  const [message, setMessage] = useState("");
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState({ q: "", status: "", business: "" });
  const [applied, setApplied] = useState(filters);
  const [input, setInput] = useState("");

  const load = async (targetPage = page, currentFilters = applied) => {
    const params = new URLSearchParams({ page: String(targetPage), pageSize: "100", roles: roles.join(",") });
    if (currentFilters.q.trim()) params.set("q", currentFilters.q.trim());
    if (currentFilters.status.trim()) params.set("status", currentFilters.status.trim());
    if (showBusinessFilter && currentFilters.business.trim()) params.set("business", currentFilters.business.trim());
    try {
      const result = await api<Page>(`/api/v1/admin/accounts?${params.toString()}`);
      setData({
        ...result,
        items: rankMatches(result.items, currentFilters.q, (row) => Object.values(row).map(String)),
      });
      setMessage("");
    } catch (e) {
      setMessage((e as Error).message);
    }
  };

  useEffect(() => {
    void load();
  }, [roles.join(",")]);

  async function act(id: unknown, action: string) {
    const requiresReason = true;
    const dangerous = action === "delete";
    if (!confirm(dangerous ? "Remove this account? This disables login and anonymizes personal data." : `${label(action)} this account?`)) return;
    const reason = prompt(dangerous ? "Reason for removing this account" : "Reason for this account change")?.trim();
    if (requiresReason && !reason) return;
    const response = await fetch(`${apiBase}/api/v1/admin/accounts/${id}/${action}`, {
      method: "POST",
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
    setMessage(dangerous ? "Account removed and anonymized." : "Account updated and audited.");
    await load();
  }

  const columns = useMemo(() => {
    const mapped = accountColumns.map((key) => key === "name" ? "cashierName" : key);
    return mapped;
  }, [accountColumns.join(",")]);

  return (
    <section>
      <Title text={title} />
      {allowCreate && (
        <AdminAccountCreate
          title={`Create ${title.replace(" Accounts", "")}`}
          description={description}
          roles={createRoles as any}
          fixedRole={createRoles.length === 1 ? (createRoles[0] as AccountRole) : undefined}
          onCreated={() => void load(1, applied)}
        />
      )}
      <p className="accounts-note">{description}</p>
      <PasswordResetRequests roles={resetRoles} />
      {message && <aside role="status">{message}</aside>}
      <form
        className="panel form account-filters"
        onSubmit={(e) => {
          e.preventDefault();
          const next = { ...filters, q: input };
          setApplied(next);
          setPage(1);
          void load(1, next);
        }}
      >
        <label>
          General Search
          <input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Name, email, phone, Business or public ID"
          />
        </label>
        <label>
          Status
          <select value={filters.status} onChange={(e) => setFilters({ ...filters, status: e.target.value })}>
            <option value="">All statuses</option>
            {["Active", "PendingVerification", "Suspended", "Closed", "Deleted", "Locked"].map((value) => (
              <option key={value} value={value}>
                {value === "Closed" ? "Deactivated" : value}
              </option>
            ))}
          </select>
        </label>
        {showBusinessFilter && (
          <label>
            Business
            <input
              value={filters.business}
              onChange={(e) => setFilters({ ...filters, business: e.target.value })}
              placeholder="Business name or public ID"
            />
          </label>
        )}
        <div className="actions">
          <button>Search</button>
          <button
            type="button"
            className="quiet"
            onClick={() => {
              const next = { q: "", status: "", business: "" };
              setInput("");
              setFilters(next);
              setApplied(next);
              setPage(1);
              void load(1, next);
            }}
          >
            Clear Filters
          </button>
        </div>
      </form>
      {!data ? (
        <Loading />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                {columns.map((key) => (
                  <th key={key}>{headerFor(key)}</th>
                ))}
                <th>Login Access</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((row, index) => {
                const role = String(row.role ?? "");
                const loginAccess = row.isLocked ? "Locked" : "Active";
                const canRemove = allowRemove && role !== "PlatformAdmin";
                return (
                  <tr key={String(row.id ?? index)}>
                    {columns.map((key) => {
                      if (key === "cashierName") return <td key={key} data-label={headerFor(key)}>{cell("cashierName", row.cashierName ?? row.name)}</td>;
                      return <td key={key} data-label={headerFor(key)}>{cell(key, row[key])}</td>;
                    })}
                    <td data-label="Login Access">{loginAccess}</td>
                    <td data-label="Actions">
                      <div className="actions">
                        {row.isLocked ? (
                          <button type="button" onClick={() => void act(row.id, "unlock")}>
                            Unlock
                          </button>
                        ) : (
                          <button type="button" className="quiet" onClick={() => void act(row.id, "lock")}>
                            Lock
                          </button>
                        )}
                        {role === "Cashier" ? (
                          row.status === "Active" ? (
                            <button type="button" className="danger" onClick={() => void act(row.id, "deactivate")}>
                              Disable Cashier
                            </button>
                          ) : row.status === "Suspended" || row.status === "Closed" ? (
                            <button type="button" onClick={() => void act(row.id, "reactivate")}>
                              Reactivate Cashier
                            </button>
                          ) : null
                        ) : row.status === "Active" ? (
                          <>
                            <button type="button" className="quiet" onClick={() => void act(row.id, "suspend")}>
                              Suspend
                            </button>
                            <button type="button" className="danger" onClick={() => void act(row.id, "deactivate")}>
                              Deactivate
                            </button>
                          </>
                        ) : row.status === "Suspended" || row.status === "Closed" ? (
                          <button type="button" onClick={() => void act(row.id, "reactivate")}>
                            Reactivate
                          </button>
                        ) : null}
                        {canRemove && (
                          <button type="button" className="danger" onClick={() => void act(row.id, "delete")}>
                            Remove Account
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <nav className="pagination" aria-label="Pagination">
        <button disabled={page === 1} onClick={() => { setPage(page - 1); void load(page - 1); }}>
          Previous
        </button>
        <span>
          Page {page} of {Math.max(1, data?.totalPages ?? 1)} · {data?.total ?? 0} records
        </span>
        <button disabled={!data || page >= data.totalPages} onClick={() => { setPage(page + 1); void load(page + 1); }}>
          Next
        </button>
      </nav>
    </section>
  );
}

function PasswordResetRequests({ roles }: { roles: string[] }) {
  const [items, setItems] = useState<Row[]>([]);
  const [message, setMessage] = useState("");
  const load = async () => {
    const responses = await Promise.all(
      roles.map(async (role) => api<Row[]>(`/api/v1/admin/password-reset-requests?role=${encodeURIComponent(role)}`)),
    );
    const merged = responses.flat().sort((a, b) => String(b.requestedAtUtc ?? "").localeCompare(String(a.requestedAtUtc ?? "")));
    setItems(merged);
  };

  useEffect(() => {
    void load().catch((e: Error) => setMessage(e.message));
  }, [roles.join(",")]);

  async function review(id: unknown, action: "approve" | "reject") {
    if (!confirm(`${action === "approve" ? "Authorize" : "Reject"} this password reset request?`)) return;
    const response = await fetch(`${apiBase}/api/v1/admin/password-reset-requests/${id}/${action}`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token()}` },
    });
    if (!response.ok) {
      setMessage("Unable to review this request.");
      return;
    }
    setMessage(action === "approve" ? "Reset approved." : "Reset rejected.");
    await load();
  }

  return (
    <section className="admin-reset-requests">
      <h2>Password Reset Requests</h2>
      {message && <aside role="status">{message}</aside>}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              {["Name", "Identifier", "Role", "Requested At", "Status", "Action"].map((heading) => (
                <th key={heading}>{heading}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={6}>No password reset requests.</td>
              </tr>
            ) : (
              items.map((item) => (
                <tr key={String(item.id)}>
                  <td>{cell("name", item.name)}</td>
                  <td>{cell("phone", item.phone)}</td>
                  <td>{cell("role", item.role)}</td>
                  <td>{cell("requestedAtUtc", item.requestedAtUtc)}</td>
                  <td>{cell("status", item.status)}</td>
                  <td>
                    <div className="actions">
                      {item.canApprove === true && (
                        <>
                          <button onClick={() => void review(item.id, "approve")}>Approve</button>
                          <button className="danger" onClick={() => void review(item.id, "reject")}>Reject</button>
                        </>
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
          <article className="summary-card"><span>API Health</span><strong>{String(data.health ?? "Unknown")}</strong></article>
          <article className="summary-card"><span>Database Health</span><strong>{String(data.database ?? "Unknown")}</strong></article>
          <article className="summary-card"><span>Worker Health</span><strong>{String((data.worker as Row | undefined)?.status ?? "Unknown")}</strong></article>
          <article className="summary-card"><span>Web Health</span><strong>Available</strong></article>
          <article className="summary-card"><span>Deployment Version</span><strong>{String(data.version ?? "Unknown")}</strong></article>
        </div>
      )}
    </section>
  );
}

function Forbidden({ text }: { text: string }) {
  return (
    <section className="panel">
      <h2>Permission denied</h2>
      <p>{text}</p>
    </section>
  );
}

function Loading() {
  return <p aria-live="polite">Loading…</p>;
}

function Empty({ text = "No records match the current filters." }: { text?: string }) {
  return (
    <div className="empty-state">
      <h2>Nothing here yet</h2>
      <p>{text}</p>
    </div>
  );
}

function ErrorBox({ text }: { text: string }) {
  return (
    <div className="error-panel" role="alert">
      <h2>Unable to load</h2>
      <p>{text}</p>
    </div>
  );
}

function headerFor(value: string) {
  if (value === "cashierName") return "Name";
  if (value === "loginAccess") return "Login Access";
  return label(value);
}

function adminDateTime(value: string) {
  return new Intl.DateTimeFormat("en-GB", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
  })
    .format(new Date(value))
    .replace(",", "");
}

function cell(key: string, value: unknown) {
  if (value == null) return "—";
  if (key.toLowerCase().includes("status")) return <span className="status">{String(value)}</span>;
  if (key.toLowerCase().includes("correlation")) return <code>{String(value)}</code>;
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (typeof value === "string" && (key.endsWith("Utc") || key === "lastDeposit")) return adminDateTime(value);
  return String(value);
}

function label(value: string) {
  return value.replace(/-/g, " ").replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, (c) => c.toUpperCase());
}
