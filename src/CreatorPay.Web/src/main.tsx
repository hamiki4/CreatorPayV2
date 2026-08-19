import {
  StrictMode,
  FormEvent,
  useEffect,
  useState,
  Component,
  ReactNode,
} from "react";
import { createRoot } from "react-dom/client";
import "./styles.css";
import { MerchantQrWorkspace } from "./QrWorkspace";
import { CommissionWorkspace } from "./CommissionWorkspace";
import {
  AdminDepositWorkspace,
  MerchantWalletWorkspace,
} from "./WalletWorkspace";
import { AdminPayoutWorkspace } from "./EarningsWorkspace";
import {
  NotificationCenter,
  NotificationOperations,
} from "./NotificationWorkspace";
import { AdminRiskWorkspace } from "./RiskWorkspace";
import { CashierCheckoutWorkspace } from "./CashierCheckoutWorkspace";
import {creatorQrPath,creatorQrPayload,preserveCreatorQrPath} from './creatorQrDeepLink'
import { AdminPortal } from "./AdminPortal";
import { CustomerWorkspace, ShopperOfferPage } from "./CustomerWorkspace";
import { AuthWorkspace } from "./AuthWorkspace";
import { PinEnrollmentGate } from "./PinExperience";
import { PasswordInput } from "./PasswordInput";
import { brand } from "./brand";
import { MerchantWorkspace } from "./MerchantWorkspace";
import {
  clearAuthState,
  handleUnauthorized,
  isWorkspacePathAllowed,
  workspaceRoute,
} from "./authSession";
import { installNativeBackNavigation, installSessionLifecycle } from "./mobileLifecycle";
import { isNativePlatform } from "./runtimePlatform";
import {
  getAccessToken,
  getRefreshToken,
  hydrateSession,
  setSessionTokens,
} from "./sessionStore";
import { canonicalOriginMigration } from "./canonicalOrigin";
import { revokePushNotifications } from './pushNotifications'
import { OnboardingStatus } from "./OnboardingStatus";
import { ContactSupport, HelpCenter, HelpLink, LegalPage } from "./PublicPages";
import { PilotExperience } from "./PilotExperience";
import { CreatorDashboard } from "./CreatorDashboard";
import { BusinessDashboard } from "./BusinessDashboard";

type Location = {
  id: string;
  name: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  region?: string;
  countryCode: string;
  timeZoneId: string;
  isActive: boolean;
};
type StaffLocation = {
  id: string;
  name: string;
  isActive: boolean;
  isPrimary: boolean;
};
type Staff = {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  username: string;
  phoneNumber: string;
  isActive: boolean;
  locations: StaffLocation[];
  businessName: string;
};
type User = { role: string; status?: string; merchantId?: string };
const strings = {
  en: {
    locations: "Locations",
    supervisors: "Supervisors",
    cashiers: "Cashiers",
    profile: "My profile",
    assigned: "Assigned locations",
    empty: "Nothing here yet.",
    loading: "Loading…",
    save: "Save",
    invite: "Send invitation",
  },
};
const t = strings.en;
const token = getAccessToken;
const apiBase = (import.meta.env.VITE_API_URL ?? "").replace(/\/$/, "");
function claims(): User {
  try {
    const value = JSON.parse(
      atob(token().split(".")[1].replace(/-/g, "+").replace(/_/g, "/")),
    );
    return {
      role:
        value["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ??
        value.role,
      status: value.account_status,
      merchantId: value.merchant_id,
    };
  } catch {
    return { role: "" };
  }
}
async function refreshAccess() {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return false;
  const response = await fetch(`${apiBase}/api/v1/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
  if (!response.ok) return false;
  const value = await response.json();
  await setSessionTokens(value.accessToken, value.refreshToken);
  return true;
}
async function api<T>(
  path: string,
  init?: RequestInit,
  retry = true,
): Promise<T> {
  const response = await fetch(`${apiBase}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token()}`,
      ...init?.headers,
    },
  });
  if (response.status === 401 && retry && (await refreshAccess()))
    return api<T>(path, init, false);
  if (handleUnauthorized(response.status))
    throw new Error("Your session has expired.");
  if (!response.ok) {
    const p = await response.json().catch(() => ({}));
    const correlation =
      p.correlationId ?? response.headers.get("X-Correlation-ID");
    throw new Error(
      `${p.detail ?? `Request failed (${response.status}).`}${response.status >= 500 && correlation ? ` Support ID: ${correlation}` : ""}`,
    );
  }
  return response.status === 204 ? (undefined as T) : response.json();
}
async function signOut() {
  const refreshToken = getRefreshToken();
  try {
    if (refreshToken)
      await fetch(`${apiBase}/api/v1/auth/logout`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken }),
      });
  } finally {
    await revokePushNotifications();
    await clearAuthState();
    location.assign("/");
  }
}
const SignOutButton = () => (
  <button className="quiet" onClick={() => void signOut()}>
    Sign out
  </button>
);

class ErrorBoundary extends Component<
  { children: ReactNode },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <main className="auth">
        <section className="panel">
          <h1>Something went wrong</h1>
          <p>Reload the page. If the issue continues, contact support.</p>
        </section>
      </main>
    ) : (
      this.props.children
    );
  }
}

function LocationForm({
  value,
  onDone,
}: {
  value?: Location;
  onDone: () => void;
}) {
  const [f, setF] = useState({
    name: value?.name ?? "",
    addressLine1: value?.addressLine1 ?? "",
    addressLine2: value?.addressLine2 ?? "",
    city: value?.city ?? "",
    region: value?.region ?? "",
    countryCode: value?.countryCode ?? "ET",
    timeZoneId:
      value?.timeZoneId ?? Intl.DateTimeFormat().resolvedOptions().timeZone,
  });
  const [error, setError] = useState("");
  async function submit(e: FormEvent) {
    e.preventDefault();
    setError("");
    try {
      await api(
        value
          ? `/api/v1/merchant/locations/${value.id}`
          : "/api/v1/merchant/locations",
        { method: value ? "PUT" : "POST", body: JSON.stringify(f) },
      );
      onDone();
    } catch (x) {
      setError((x as Error).message);
    }
  }
  return (
    <form onSubmit={submit} className="panel form">
      <h2>{value ? "Edit" : "Create"} location</h2>
      {Object.entries(f).map(([key, val]) => (
        <label key={key}>
          {key.replace(/([A-Z])/g, " $1")}
          <input
            required={!["addressLine2", "region"].includes(key)}
            value={val}
            onChange={(e) => setF({ ...f, [key]: e.target.value })}
          />
        </label>
      ))}
      {error && <p className="error">{error}</p>}
      <button>{t.save}</button>
    </form>
  );
}
function Locations() {
  const [items, setItems] = useState<Location[] | null>(null);
  const [editing, setEditing] = useState<Location | undefined>();
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState("");
  const load = () =>
    api<Location[]>("/api/v1/merchant/locations")
      .then(setItems)
      .catch((e) => setError(e.message));
  useEffect(() => {
    void load();
  }, []);
  async function status(x: Location) {
    await api(`/api/v1/merchant/locations/${x.id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive: !x.isActive }),
    });
    load();
  }
  if (creating || editing)
    return (
      <LocationForm
        value={editing}
        onDone={() => {
          setCreating(false);
          setEditing(undefined);
          load();
        }}
      />
    );
  return (
    <section>
      <div className="title">
        <h2>{t.locations}</h2>
        <button onClick={() => setCreating(true)}>New location</button>
      </div>
      {error && <p className="error">{error}</p>}
      {!items ? (
        <p>{t.loading}</p>
      ) : items.length === 0 ? (
        <p className="empty">{t.empty}</p>
      ) : (
        <div className="cards">
          {items.map((x) => (
            <article key={x.id}>
              <span>{x.isActive ? "Active" : "Inactive"}</span>
              <strong>{x.name}</strong>
              <p>
                {x.addressLine1}, {x.city}
              </p>
              <div>
                <button onClick={() => setEditing(x)}>Edit</button>
                <button className="quiet" onClick={() => status(x)}>
                  {x.isActive ? "Deactivate" : "Activate"}
                </button>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
function StaffArea({ kind }: { kind: "supervisors" | "cashiers" }) {
  const [items, setItems] = useState<Staff[] | null>(null);
  const [locations, setLocations] = useState<Location[]>([]);
  const [selected, setSelected] = useState<Staff>();
  const [creating, setCreating] = useState(false);
  const [message, setMessage] = useState("");
  const load = () =>
    api<Staff[]>(`/api/v1/merchant/${kind}`)
      .then(setItems)
      .catch((e) => setMessage(e.message));
  useEffect(() => {
    load();
    api<Location[]>("/api/v1/merchant/locations").then(setLocations);
  }, [kind]);
  async function status(x: Staff) {
    await api(`/api/v1/merchant/${kind}/${x.id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive: !x.isActive }),
    });
    setMessage(x.isActive ? "Cashier disabled." : "Cashier reactivated.");
    load();
  }
  if (creating)
    return (
      <CreateCashier
        done={() => {
          setCreating(false);
          setMessage("Cashier account created.");
          load();
        }}
      />
    );
  if (selected)
    return (
      <StaffDetail
        kind={kind}
        staff={selected}
        locations={locations}
        done={() => {
          setSelected(undefined);
          load();
        }}
      />
    );
  return (
    <section>
      <div className="title">
        <h2>Cashiers</h2>
        <button onClick={() => setCreating(true)}>Create Cashier</button>
      </div>
      {message && <aside>{message}</aside>}
      {!items ? (
        <p>{t.loading}</p>
      ) : items.length === 0 ? (
        <p className="compact-empty">No Cashiers yet.</p>
      ) : (
        <div className="active-ads cashier-list">
          <div className="active-ad business-table-row cashier-grid headings">
            <span>Cashier</span>
            <span>Username</span>
            <span>Location</span>
            <span>Status</span>
            <span>Action</span>
          </div>
          {items.map((x) => (
            <article
              className="active-ad business-table-row cashier-grid"
              key={x.id}
            >
              <strong data-label="Cashier">
                {x.firstName} {x.lastName}
              </strong>
              <span data-label="Username">
                {x.username && !x.username.includes("@")
                  ? x.username
                  : "Legacy account"}
              </span>
              <span data-label="Location">
                {x.locations.map((l) => l.name).join(", ") || "Not assigned"}
              </span>
              <span className="table-status" data-label="Status">
                <span className="status-badge">
                  {x.isActive ? "Active" : "Disabled"}
                </span>
              </span>
              <div className="actions table-action" data-label="Action">
                <button className="quiet" onClick={() => setSelected(x)}>
                  View
                </button>
                <button
                  className={x.isActive ? "danger" : "quiet"}
                  onClick={() => void status(x)}
                >
                  {x.isActive ? "Disable" : "Reactivate"}
                </button>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
function CreateCashier({ done }: { done: () => void }) {
  const [name, setName] = useState(""),
    [phoneNumber, setPhoneNumber] = useState(""),
    [temporaryPassword, setTemporaryPassword] = useState(""),
    [confirmation, setConfirmation] = useState(""),
    [error, setError] = useState("");
  async function submit(e: FormEvent) {
    e.preventDefault();
    setError("");
    if (temporaryPassword !== confirmation) {
      setError("Temporary passwords do not match.");
      return;
    }
    const parts = name.trim().split(/\s+/),
      firstName = parts.shift() ?? "",
      lastName = parts.join(" ") || "-";
    try {
      await api("/api/v1/merchant/cashiers", {
        method: "POST",
        body: JSON.stringify({
          firstName,
          lastName,
          phoneNumber,
          temporaryPassword,
          confirmation,
        }),
      });
      setTemporaryPassword("");
      setConfirmation("");
      done();
    } catch (x) {
      const detail = (x as Error).message;
      console.error("Cashier creation failed", detail);
      setError(
        detail.includes("405")
            ? "Unable to create Cashier."
            : "Please check the information and try again.",
      );
    }
  }
  return (
    <form className="panel form cashier-invite" onSubmit={submit}>
      <h2>Create Cashier</h2>
      <label>
        Cashier Name
        <input
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </label>
      <label>
        Phone Number
        <input
          required
          type="tel"
          value={phoneNumber}
          onChange={(e) => setPhoneNumber(e.target.value)}
        />
      </label>
      <PasswordInput label="Temporary Password" required minLength={8} autoComplete="new-password" value={temporaryPassword} onChange={setTemporaryPassword} />
      <PasswordInput label="Confirm Temporary Password" required minLength={8} autoComplete="new-password" value={confirmation} onChange={setConfirmation} />
      {error && <p className="error">{error}</p>}
      <button>Create Cashier</button>
    </form>
  );
}
function StaffDetail({
  kind,
  staff,
  locations,
  done,
}: {
  kind: string;
  staff: Staff;
  locations: Location[];
  done: () => void;
}) {
  const [ids, setIds] = useState(staff.locations.map((x) => x.id));
  const [primary, setPrimary] = useState(
    staff.locations.find((x) => x.isPrimary)?.id ?? "",
  );
  const [error, setError] = useState("");
  async function save() {
    try {
      await api(`/api/v1/merchant/${kind}/${staff.id}/locations`, {
        method: "PUT",
        body: JSON.stringify(
          kind === "cashiers"
            ? { locationIds: ids, primaryLocationId: primary || null }
            : { locationIds: ids },
        ),
      });
      done();
    } catch (x) {
      setError((x as Error).message);
    }
  }
  async function status() {
    await api(`/api/v1/merchant/${kind}/${staff.id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive: !staff.isActive }),
    });
    done();
  }
  return (
    <section className="panel">
      <button className="back" onClick={done}>
        ← Back
      </button>
      <h2>
        {staff.firstName} {staff.lastName}
      </h2>
      <p>
        {kind === "cashiers" ? (
          <>
            Username: {staff.username} · {staff.phoneNumber}
          </>
        ) : (
          <>
            {staff.email} · {staff.phoneNumber}
          </>
        )}
      </p>
      <h3>Location assignments</h3>
      {locations
        .filter((x) => x.isActive)
        .map((x) => (
          <div className="assignment" key={x.id}>
            <label className="check">
              <input
                type="checkbox"
                checked={ids.includes(x.id)}
                onChange={(e) =>
                  setIds(
                    e.target.checked
                      ? [...ids, x.id]
                      : ids.filter((id) => id !== x.id),
                  )
                }
              />
              {x.name}
            </label>
            {kind === "cashiers" && ids.includes(x.id) && (
              <label className="check">
                <input
                  type="radio"
                  name="primary"
                  checked={primary === x.id}
                  onChange={() => setPrimary(x.id)}
                />
                Primary
              </label>
            )}
          </div>
        ))}
      {error && <p className="error">{error}</p>}
      <div className="actions">
        <button onClick={save}>{t.save}</button>
        <button className="danger" onClick={status}>
          {staff.isActive ? "Deactivate" : "Activate"}
        </button>
      </div>
    </section>
  );
}
function AcceptInvitation() {
  const token = new URLSearchParams(location.search).get("token") ?? "",
    [preview, setPreview] = useState<{
      businessName: string;
      role: string;
      locationName: string;
    }>(),
    [f, setF] = useState({ password: "", confirmation: "" }),
    [message, setMessage] = useState("");
  useEffect(() => {
    api<{ businessName: string; role: string; locationName: string }>(
      `/api/v1/staff-invitations/preview?token=${encodeURIComponent(token)}`,
      { headers: { Authorization: "" } },
    )
      .then(setPreview)
      .catch((x) => setMessage((x as Error).message));
  }, [token]);
  async function submit(e: FormEvent) {
    e.preventDefault();
    try {
      await api("/api/v1/staff-invitations/accept", {
        method: "POST",
        body: JSON.stringify({ token, ...f }),
        headers: { Authorization: "" },
      });
      setMessage("Invitation accepted. You can now sign in.");
    } catch (x) {
      setMessage((x as Error).message);
    }
  }
  return (
    <main className="auth">
      <form className="panel form invitation-accept" onSubmit={submit}>
        <p className="eyebrow">Weymela</p>
        <h1>Cashier invitation</h1>
        {preview && (
          <div className="invitation-summary">
            <p>You've been invited to join:</p>
            <strong>{preview.businessName}</strong>
            <p>Role: {preview.role}</p>
            <p>Location: {preview.locationName}</p>
          </div>
        )}
        <PasswordInput label="Create Password" required minLength={8} value={f.password} onChange={password => setF({ ...f, password })} />
        <PasswordInput label="Confirm Password" required minLength={8} value={f.confirmation} onChange={confirmation => setF({ ...f, confirmation })} />
        <button disabled={!preview}>Accept Invitation</button>
        {message && (
          <aside role="status">
            {message}
            {message.includes("now sign in") && (
              <>
                {" "}
                <a href="/">Sign In</a>
              </>
            )}
          </aside>
        )}
      </form>
    </main>
  );
}
function StaffHome({ role }: { role: "supervisor" | "cashier" }) {
  const [data, setData] = useState<Staff>();
  const [error, setError] = useState("");
  useEffect(() => {
    api<Staff>(`/api/v1/${role}/me`)
      .then(setData)
      .catch((e) => setError(e.message));
  }, [role]);
  return (
    <section>
      <h2>{t.profile}</h2>
      {error && <p className="error">{error}</p>}
      {!data ? (
        <p>{t.loading}</p>
      ) : (
        <div className="panel">
          <h3>
            {data.firstName} {data.lastName}
          </h3>
          <p>
            {data.email}
            <br />
            {data.phoneNumber}
          </p>
          <h3>{t.assigned}</h3>
          {data.locations.length ? (
            data.locations.map((x) => (
              <p key={x.id}>
                {x.name}
                {x.isPrimary ? " · Primary" : ""}
              </p>
            ))
          ) : (
            <p>{t.empty}</p>
          )}
        </div>
      )}
    </section>
  );
}
function App({initialQrPayload}:{initialQrPayload?:string}={}) {
  const user = claims();
  if (user.role === "Cashier")
    return (
      <main className="app creator-app role-cashier">
        <a className="skip" href="#workspace-content">
          Skip to content
        </a>
        <header>
          <div>
            <p className="eyebrow">{brand.productName}</p>
            <h1>Cashier Dashboard</h1>
          </div>
          <div className="header-actions">
            <HelpLink category="Cashiers and Supervisors" />
            <SignOutButton />
          </div>
        </header>
        <div id="workspace-content" tabIndex={-1}>
          <CashierCheckoutWorkspace initialQrPayload={initialQrPayload}/>
        </div>
      </main>
    );
  if (user.role === "Supervisor")
    return (
      <main className="app">
        <a className="skip" href="#workspace-content">
          Skip to content
        </a>
        <header>
          <div>
            <p className="eyebrow">{brand.productName}</p>
            <h1>Supervisor Dashboard</h1>
          </div>
          <div className="header-actions">
            <HelpLink category="Cashiers and Supervisors" />
            <SignOutButton />
          </div>
        </header>
        <PilotExperience role="Supervisor" />
        <div id="workspace-content" tabIndex={-1}>
          <NotificationCenter />
          <StaffHome role="supervisor" />
          <MerchantQrWorkspace role="Supervisor" />
        </div>
      </main>
    );
  const creator = user.role === "Creator",
    active = user.status === "Active";
  return (
    <main className={`app role-${creator ? "creator" : "business"}${creator ? " creator-app" : ""}`}>
      <a className="skip" href="#workspace-content">
        Skip to content
      </a>
      <PilotExperience role={creator ? "Creator" : "Business Owner"} />
      <div id="workspace-content" tabIndex={-1}>
        {!active ? (
          <OnboardingStatus role={creator ? "Creator" : "MerchantAdmin"} />
        ) : creator ? (
          <CreatorDashboard onSignOut={()=>void signOut()} />
        ) : (
          <BusinessDashboard
            cashiers={<StaffArea kind="cashiers" />}
            wallet={<MerchantWalletWorkspace />}
            profile={<MerchantWorkspace />}
            onSignOut={()=>void signOut()}
          />
        )}
      </div>
    </main>
  );
}
function Root() {
  if (location.pathname === "/help") return <HelpCenter />;
  if (location.pathname === "/contact") return <ContactSupport />;
  if (location.pathname === "/terms") return <LegalPage kind="terms" />;
  if (location.pathname === "/privacy") return <LegalPage kind="privacy" />;
  if (location.pathname.includes("/staff-invitations/accept"))
    return <AcceptInvitation />;
  const offerCode = location.pathname.match(/^\/(?:offers|o)\/([^/]+)/)?.[1];
  if (offerCode)
    return <ShopperOfferPage code={decodeURIComponent(offerCode)} />;
  const user = claims();
  const qrPath=creatorQrPath(location.pathname,location.search)
  if(qrPath){
    if(!user.role){preserveCreatorQrPath(qrPath);history.replaceState(null,'','/');return <AuthWorkspace/>}
    if(user.role==='Cashier')return <App initialQrPayload={creatorQrPayload(qrPath)}/>
    return <main className="auth"><section className="panel"><p className="eyebrow">Weymela</p><h1>Creator QR</h1><p>Cashier checkout requires an active Cashier account.</p><a href={workspaceRoute(user.role)}>Return to your dashboard</a></section></main>
  }
  if (!user.role) return <AuthWorkspace />;
  if (!isWorkspacePathAllowed(user.role, location.pathname)) {
    location.replace(workspaceRoute(user.role));
    return null;
  }
  if (user.role === "Customer")
    return (
      <PinEnrollmentGate><main className="app role-shopper">
        <a className="skip" href="#workspace-content">
          Skip to content
        </a>
        <div id="workspace-content" tabIndex={-1}>
          {user.status === "Active" ? (
            <CustomerWorkspace onSignOut={()=>void signOut()} />
          ) : (
            <OnboardingStatus role="Customer" />
          )}
        </div>
      </main></PinEnrollmentGate>
    );
  return user.role === "PlatformAdmin" ? (
    <AdminPortal
      operations={{
        commission: <CommissionWorkspace role={user.role} />,
        deposits: <AdminDepositWorkspace />,
        payouts: <AdminPayoutWorkspace />,
        notifications: <NotificationOperations />,
        fraud: <AdminRiskWorkspace />,
        disputes: <AdminRiskWorkspace />,
        reversals: <AdminRiskWorkspace />,
      }}
    />
  ) : user.role === "Supervisor" ? (
    <App />
  ) : (
    <PinEnrollmentGate><App /></PinEnrollmentGate>
  );
}
function SessionGuard({ children }: { children: ReactNode }) {
  useEffect(() => {
    const removeBack = installNativeBackNavigation();
    const removeSession = token()
      ? installSessionLifecycle(async () => {
          if (!token()) return false;
          return refreshAccess();
        })
      : undefined;
    return () => { removeBack?.(); removeSession?.(); };
  }, []);
  return children;
}

function renderApplication() {
  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <ErrorBoundary>
        <SessionGuard>
          <Root />
        </SessionGuard>
      </ErrorBoundary>
    </StrictMode>,
  );
}

function registerServiceWorker() {
  if (isNativePlatform() || !("serviceWorker" in navigator)) return;
  window.addEventListener("load", () => {
    if (import.meta.env.DEV) {
      void Promise.all([
        navigator.serviceWorker
          .getRegistrations()
          .then((registrations) =>
            Promise.all(
              registrations.map((registration) => registration.unregister()),
            ),
          ),
        "caches" in window
          ? caches
              .keys()
              .then((keys) =>
                Promise.all(
                  keys
                    .filter((key) => key.startsWith("creatorpay-shell-"))
                    .map((key) => caches.delete(key)),
                ),
              )
          : Promise.resolve([]),
      ]);
      return;
    }
    void navigator.serviceWorker
      .register("/sw.js")
      .then((reg) => {
        reg.addEventListener("updatefound", () => {
          const worker = reg.installing;
          worker?.addEventListener("statechange", () => {
            if (
              worker.state === "installed" &&
              navigator.serviceWorker.controller
            )
              dispatchEvent(new CustomEvent("creatorpay-update-ready"));
          });
        });
      })
      .catch(() => {});
  });
}

void canonicalOriginMigration.then(() => hydrateSession())
  .then(() => {
    renderApplication();
    registerServiceWorker();
  })
  .catch((error) => {
    console.error("Authentication storage could not be initialized.", error);
    createRoot(document.getElementById("root")!).render(
      <main className="auth">
        <section className="panel" role="alert">
          <h1>Weymela could not start</h1>
          <p>Close and reopen the app. If this continues, contact support.</p>
        </section>
      </main>,
    );
  });
