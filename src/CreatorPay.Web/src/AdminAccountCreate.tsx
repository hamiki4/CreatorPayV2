import { FormEvent, useEffect, useMemo, useState } from "react";
import { api, ApiError } from "./apiClient";

type MerchantOption = {
  id: string;
  tradingName?: string;
  publicMerchantId?: string;
  status?: string;
};

type Page<T = Record<string, unknown>> = {
  items: T[];
};

const roles = [
  { value: "PlatformAdmin", label: "PlatformAdmin" },
  { value: "MerchantAdmin", label: "MerchantAdmin / Business" },
  { value: "Cashier", label: "Cashier" },
  { value: "Creator", label: "Creator" },
  { value: "Customer", label: "Customer" },
] as const;

export function AdminAccountCreate({ onCreated }: { onCreated: () => void }) {
  const [role, setRole] = useState<(typeof roles)[number]["value"]>("PlatformAdmin");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [businesses, setBusinesses] = useState<MerchantOption[]>([]);
  const [merchantId, setMerchantId] = useState("");
  const [form, setForm] = useState({
    email: "",
    phoneNumber: "",
    password: "",
    confirmation: "",
    firstName: "",
    lastName: "",
    displayName: "",
    legalBusinessName: "",
    tradingName: "",
    businessType: "Other",
    primaryContactName: "",
    businessAddress: "",
    city: "Addis Ababa",
    region: "Addis Ababa",
    country: "Ethiopia",
    timeZone: "Africa/Addis_Ababa",
    preferredLanguage: "en",
    biography: "",
    contentCategories: "",
    zone: "",
  });

  useEffect(() => {
    api<Page<MerchantOption>>("/api/v1/admin/merchants?page=1&pageSize=100")
      .then((value) => setBusinesses(value.items))
      .catch(() => setBusinesses([]));
  }, []);

  const activeBusinesses = useMemo(
    () =>
      businesses.filter((business) =>
        ["Active", "LowBalance", "LowBalanceRestricted", "ApprovedUnfunded"].includes(
          String(business.status ?? ""),
        ),
      ),
    [businesses],
  );

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
      onCreated();
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : (error as Error).message);
    } finally {
      setBusy(false);
    }
  }

  function changeRole(next: string) {
    setRole(next as (typeof roles)[number]["value"]);
    setMerchantId("");
    setMessage("");
  }

  return (
    <section className="panel form" aria-labelledby="create-account-title">
      <h2 id="create-account-title">Create Account</h2>
      <p>PlatformAdmin only. Temporary credentials are created directly and audited.</p>
      {message && <aside role="status">{message}</aside>}
      <form onSubmit={submit}>
        <label>
          Role
          <select value={role} onChange={(e) => changeRole(e.target.value)}>
            {roles.map((item) => (
              <option key={item.value} value={item.value}>
                {item.label}
              </option>
            ))}
          </select>
        </label>

        <label>
          Email
          <input
            required={role === "PlatformAdmin"}
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
          />
        </label>

        <label>
          Phone
          <input
            value={form.phoneNumber}
            onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
            required={role !== "PlatformAdmin"}
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
              <input value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} />
            </label>
            <label>
              Preferred language
              <input
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
              <input
                required
                value={form.businessType}
                onChange={(e) => setForm({ ...form, businessType: e.target.value })}
              />
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
              <input required value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} />
            </label>
            <label>
              Region
              <input required value={form.region} onChange={(e) => setForm({ ...form, region: e.target.value })} />
            </label>
            <label>
              Country
              <input required value={form.country} onChange={(e) => setForm({ ...form, country: e.target.value })} />
            </label>
            <label>
              Time zone
              <input required value={form.timeZone} onChange={(e) => setForm({ ...form, timeZone: e.target.value })} />
            </label>
          </>
        )}

        <label>
          Temporary password
          <input
            required
            type="password"
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
          />
        </label>
        <label>
          Confirm password
          <input
            required
            type="password"
            value={form.confirmation}
            onChange={(e) => setForm({ ...form, confirmation: e.target.value })}
          />
        </label>

        <button disabled={busy}>{busy ? "Creating…" : "Create Account"}</button>
      </form>
    </section>
  );
}
