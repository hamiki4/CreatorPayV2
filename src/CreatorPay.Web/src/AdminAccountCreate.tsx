import { FormEvent, useEffect, useMemo, useState } from "react";
import { ApiError, api } from "./apiClient";
import { businessTypes } from "./AuthWorkspace";

type AdminRole =
  | "PlatformAdmin"
  | "OperationsAdmin"
  | "MerchantAdmin"
  | "Cashier"
  | "Creator"
  | "Customer";

type MerchantOption = {
  id: string;
  tradingName?: string;
  publicMerchantId?: string;
  status?: string;
};

type Page<T = Record<string, unknown>> = {
  items: T[];
};

const roleLabels: Record<AdminRole, string> = {
  PlatformAdmin: "PlatformAdmin",
  OperationsAdmin: "OperationsAdmin",
  MerchantAdmin: "MerchantAdmin / Business",
  Cashier: "Cashier",
  Creator: "Creator",
  Customer: "Customer",
};

const defaultForm = {
  email: "",
  phoneNumber: "",
  password: "",
  confirmation: "",
  firstName: "",
  lastName: "",
  displayName: "",
  legalBusinessName: "",
  tradingName: "",
  businessType: "",
  primaryContactName: "",
  businessAddress: "",
  city: "",
  region: "",
  country: "",
  timeZone: "",
  preferredLanguage: "",
  biography: "",
  contentCategories: "",
  zone: "",
};

export function AdminAccountCreate({
  title,
  description,
  roles,
  fixedRole,
  onCreated,
}: {
  title: string;
  description: string;
  roles: AdminRole[];
  fixedRole?: AdminRole;
  onCreated: () => void;
}) {
  const initialRole = fixedRole ?? roles[0] ?? "PlatformAdmin";
  const [role, setRole] = useState<AdminRole>(initialRole);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [businesses, setBusinesses] = useState<MerchantOption[]>([]);
  const [merchantId, setMerchantId] = useState("");
  const [form, setForm] = useState(defaultForm);

  useEffect(() => {
    if (fixedRole) setRole(fixedRole);
  }, [fixedRole]);

  useEffect(() => {
    if (role !== "MerchantAdmin" && role !== "Cashier") return;
    api<Page<MerchantOption>>("/api/v1/admin/merchants?page=1&pageSize=100")
      .then((value) => setBusinesses(value.items))
      .catch(() => setBusinesses([]));
  }, [role]);

  const activeBusinesses = useMemo(
    () =>
      businesses.filter((business) =>
        ["Active", "LowBalance", "LowBalanceRestricted", "ApprovedUnfunded"].includes(
          String(business.status ?? ""),
        ),
      ),
    [businesses],
  );

  const showRoleSelect = !fixedRole && roles.length > 1;
  const showAdminFields = role === "PlatformAdmin" || role === "OperationsAdmin";
  const showCreatorFields = role === "Creator";
  const showCustomerFields = role === "Customer";
  const showCashierFields = role === "Cashier";
  const showMerchantFields = role === "MerchantAdmin" && !merchantId;
  const showMerchantSelect = role === "MerchantAdmin" || role === "Cashier";

  async function submit(event: FormEvent) {
    event.preventDefault();
    setMessage("");
    setBusy(true);
    try {
      await api("/api/v1/admin/accounts/create", {
        method: "POST",
        body: JSON.stringify({
          role,
          email: form.email || null,
          phoneNumber: form.phoneNumber || null,
          password: form.password,
          confirmation: form.confirmation,
          firstName: form.firstName || null,
          lastName: form.lastName || null,
          displayName: form.displayName || null,
          legalBusinessName: form.legalBusinessName || null,
          tradingName: form.tradingName || null,
          businessType: form.businessType || null,
          primaryContactName: form.primaryContactName || null,
          businessAddress: form.businessAddress || null,
          city: form.city || null,
          region: form.region || null,
          country: form.country || null,
          timeZone: form.timeZone || null,
          preferredLanguage: form.preferredLanguage || null,
          biography: form.biography || null,
          contentCategories: form.contentCategories || null,
          zone: form.zone || null,
          merchantId: merchantId || null,
        }),
      });
      setMessage("Account created and audited.");
      setForm(defaultForm);
      setMerchantId("");
      onCreated();
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : (error as Error).message);
    } finally {
      setBusy(false);
    }
  }

  function changeRole(next: string) {
    setRole(next as AdminRole);
    setMerchantId("");
    setMessage("");
  }

  return (
    <section className="panel form admin-create-card" aria-labelledby="create-account-title">
      <h2 id="create-account-title">{title}</h2>
      <p>{description}</p>
      {message && <aside role="status">{message}</aside>}
      <form onSubmit={submit}>
        {showRoleSelect ? (
          <label>
            Role
            <select value={role} onChange={(e) => changeRole(e.target.value)}>
              {roles.map((item) => (
                <option key={item} value={item}>
                  {roleLabels[item]}
                </option>
              ))}
            </select>
          </label>
        ) : (
          <input type="hidden" value={role} readOnly />
        )}

        <label>
          Email
          <input
            required={showAdminFields}
            placeholder="name@example.com"
            autoComplete="off"
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
          />
        </label>

        <label>
          Phone
          <input
            placeholder="+251 9…"
            value={form.phoneNumber}
            onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
            required={!showAdminFields}
          />
        </label>

        {showCashierFields && (
          <>
            <label>
              First name
              <input
                required
                value={form.firstName}
                onChange={(e) => setForm({ ...form, firstName: e.target.value })}
              />
            </label>
            <label>
              Last name
              <input
                required
                value={form.lastName}
                onChange={(e) => setForm({ ...form, lastName: e.target.value })}
              />
            </label>
          </>
        )}

        {showCreatorFields && (
          <>
            <label>
              First name
              <input
                required
                value={form.firstName}
                onChange={(e) => setForm({ ...form, firstName: e.target.value })}
              />
            </label>
            <label>
              Last name
              <input
                required
                value={form.lastName}
                onChange={(e) => setForm({ ...form, lastName: e.target.value })}
              />
            </label>
            <label>
              Display name
              <input
                required
                value={form.displayName}
                onChange={(e) => setForm({ ...form, displayName: e.target.value })}
              />
            </label>
            <label>
              City
              <input placeholder="Addis Ababa" value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} />
            </label>
            <label>
              Preferred language
              <input
                placeholder="en"
                value={form.preferredLanguage}
                onChange={(e) => setForm({ ...form, preferredLanguage: e.target.value })}
              />
            </label>
          </>
        )}

        {showCustomerFields && (
          <label>
            Display name
            <input
              required
              value={form.displayName}
              onChange={(e) => setForm({ ...form, displayName: e.target.value })}
            />
          </label>
        )}

        {showMerchantSelect && (
          <label>
            Business
            <select value={merchantId} onChange={(e) => setMerchantId(e.target.value)}>
              <option value="">Create new business</option>
              {activeBusinesses.map((business) => (
                <option key={business.id} value={business.id}>
                  {business.tradingName ?? business.publicMerchantId ?? business.id}
                </option>
              ))}
            </select>
          </label>
        )}

        {showMerchantFields && (
          <>
            <label>
              Legal business name
              <input
                required
                value={form.legalBusinessName}
                onChange={(e) => setForm({ ...form, legalBusinessName: e.target.value })}
              />
            </label>
            <label>
              Trading name
              <input
                required
                value={form.tradingName}
                onChange={(e) => setForm({ ...form, tradingName: e.target.value })}
              />
            </label>
            <label>
              Business type
              <select
                required
                value={form.businessType}
                onChange={(e) => setForm({ ...form, businessType: e.target.value })}
              >
                <option value="" disabled>Select a Business Type</option>
                {businessTypes.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.en}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Primary contact name
              <input
                required
                value={form.primaryContactName}
                onChange={(e) => setForm({ ...form, primaryContactName: e.target.value })}
              />
            </label>
            <label>
              Business address
              <input
                required
                value={form.businessAddress}
                onChange={(e) => setForm({ ...form, businessAddress: e.target.value })}
              />
            </label>
            <label>
              City
              <input required placeholder="Addis Ababa" value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} />
            </label>
            <label>
              Region
              <input required placeholder="Addis Ababa" value={form.region} onChange={(e) => setForm({ ...form, region: e.target.value })} />
            </label>
            <label>
              Country
              <input required placeholder="Ethiopia" value={form.country} onChange={(e) => setForm({ ...form, country: e.target.value })} />
            </label>
            <label>
              Time zone
              <input required placeholder="Africa/Addis_Ababa" value={form.timeZone} onChange={(e) => setForm({ ...form, timeZone: e.target.value })} />
            </label>
          </>
        )}

        <label>
          Temporary password
          <input
            required
            type="password"
            autoComplete="new-password"
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
          />
        </label>
        <label>
          Confirm password
          <input
            required
            type="password"
            autoComplete="new-password"
            value={form.confirmation}
            onChange={(e) => setForm({ ...form, confirmation: e.target.value })}
          />
        </label>

        <button disabled={busy}>{busy ? "Creating…" : "Create Account"}</button>
      </form>
    </section>
  );
}
