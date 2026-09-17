import { FormEvent, useState } from "react";
import { businessTypes, normalizeEthiopianPhone } from "./AuthWorkspace";
import { api } from "./apiClient";
import { getExternalSession, loadExternalSession, setExternalSession } from "./externalSession";

const customerBlank = { displayName: "" };
const creatorBlank = { firstName: "", lastName: "", displayName: "", phoneNumber: "", email: "", city: "Addis Ababa", zone: "", biography: "", contentCategories: "", primarySocialPlatform: "TikTok", socialProfileUrl: "", followerCount: 0 };
const businessBlank = { tradingName: "", businessType: "", primaryContactName: "", phoneNumber: "", email: "", businessAddress: "", city: "Addis Ababa", region: "Addis Ababa", country: "Ethiopia", timeZone: "Africa/Addis_Ababa" };

export function ExternalOnboardingWorkspace({ kind }: { kind: "customer" | "creator" | "business" }) {
  const session = getExternalSession();
  const expected = kind === "customer" ? "Customer" : kind === "creator" ? "Creator" : "MerchantAdmin";
  const [customer, setCustomer] = useState(customerBlank);
  const [creator, setCreator] = useState(creatorBlank);
  const [business, setBusiness] = useState(businessBlank);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  if (!session?.isOnboarding || session.role !== expected) {
    location.replace(session?.destination ?? "/");
    return null;
  }
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setMessage("");
    try {
      const body = kind === "customer" ? customer : kind === "creator"
        ? { ...creator, phoneNumber: normalizeEthiopianPhone(creator.phoneNumber) ?? creator.phoneNumber, email: creator.email || null }
        : { ...business, phoneNumber: normalizeEthiopianPhone(business.phoneNumber) ?? business.phoneNumber, email: business.email || null };
      const result = await api<{ destination: string }>(`/api/v1/integration/v3/onboarding/${kind}`, { method: "POST", body: JSON.stringify(body) });
      const refreshed = await loadExternalSession(); setExternalSession(refreshed.session);
      location.assign(result.destination);
    } catch (error) { setMessage((error as Error).message); }
    finally { setBusy(false); }
  }
  const field = (state: Record<string, unknown>, set: (value: any) => void, key: string, label: string, type = "text", required = true) => <label>{label}<input type={type} required={required} value={String(state[key] ?? "")} onChange={(event) => set({ ...state, [key]: type === "number" ? Number(event.target.value) : event.target.value })} /></label>;
  return <main className={`auth external-onboarding external-onboarding-${kind}`}><section className="auth-card"><p className="eyebrow">WEYMELA</p><h1>{kind === "customer" ? "Customer Sign Up" : kind === "creator" ? "Creator Sign Up" : "Business Sign Up"}</h1><p className="auth-intro">Complete your {kind === "business" ? "Business" : kind === "creator" ? "Creator" : "Customer"} profile. Your Weymela sign-in is already secured.</p>
    <form className="form" onSubmit={submit}>
      {kind === "customer" && field(customer, setCustomer, "displayName", "Preferred name")}
      {kind === "creator" && <>{field(creator, setCreator, "firstName", "Legal First Name")}{field(creator, setCreator, "lastName", "Father's / Last Name")}{field(creator, setCreator, "displayName", "Public Display Name")}{field(creator, setCreator, "phoneNumber", "Contact phone")}{field(creator, setCreator, "email", "Contact email (optional)", "email", false)}{field(creator, setCreator, "city", "Primary City")}{field(creator, setCreator, "biography", "Biography", "text", false)}{field(creator, setCreator, "contentCategories", "Content categories", "text", false)}<label>Primary Social Platform<select value={creator.primarySocialPlatform} onChange={(event) => setCreator({ ...creator, primarySocialPlatform: event.target.value })}><option>TikTok</option><option>Instagram</option><option>YouTube</option><option>Facebook</option></select></label>{field(creator, setCreator, "socialProfileUrl", "Social Profile URL", "url")}{field(creator, setCreator, "followerCount", "Estimated Follower Count", "number")}</>}
      {kind === "business" && <>{field(business, setBusiness, "tradingName", "Trading Name")}<label>Business Type<select required value={business.businessType} onChange={(event) => setBusiness({ ...business, businessType: event.target.value })}><option value="">Select business type</option>{businessTypes.map((item) => <option key={item.value} value={item.value}>{item.en}</option>)}</select></label>{field(business, setBusiness, "primaryContactName", "Primary Contact Name")}{field(business, setBusiness, "phoneNumber", "Business contact phone")}{field(business, setBusiness, "email", "Business contact email (optional)", "email", false)}{field(business, setBusiness, "businessAddress", "Business Address")}{field(business, setBusiness, "city", "Primary City")}{field(business, setBusiness, "region", "Region")}</>}
      <small className="full">Contact details describe this profile and do not create another login.</small>{message && <p className="error" role="alert">{message}</p>}<button disabled={busy}>{busy ? "Saving…" : "Continue"}</button>
    </form></section></main>;
}
