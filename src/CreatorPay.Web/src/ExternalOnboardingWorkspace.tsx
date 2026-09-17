import { FormEvent, useState } from "react";
import { businessTypes, normalizeEthiopianPhone } from "./AuthWorkspace";
import { api } from "./apiClient";
import { getExternalSession, loadExternalSession, setExternalSession } from "./externalSession";

type SocialPlatform = "TikTok" | "Instagram" | "YouTube" | "Facebook";
type SocialRow = { platform: SocialPlatform; profileUrl: string; audienceCount: string };

const platforms: { value: SocialPlatform; label: string; url: string; audience: string }[] = [
  { value: "TikTok", label: "TikTok", url: "Profile URL", audience: "Follower count" },
  { value: "Instagram", label: "Instagram", url: "Profile URL", audience: "Follower count" },
  { value: "YouTube", label: "YouTube", url: "Channel URL", audience: "Subscriber count" },
  { value: "Facebook", label: "Facebook", url: "Profile/Page URL", audience: "Follower count" },
];
const customerBlank = { displayName: "" };
const creatorBlank = { firstName: "", lastName: "", displayName: "", city: "Addis Ababa", zone: "", biography: "", contentCategories: "" };
const businessBlank = { tradingName: "", businessType: "", primaryContactName: "", businessContactPhone: "", businessContactEmail: "", businessAddress: "", city: "Addis Ababa", region: "Addis Ababa", country: "Ethiopia", timeZone: "Africa/Addis_Ababa" };

function AccountIdentity({ email, phone }: { email?: string | null; phone?: string | null }) {
  return <fieldset className="account-identity full"><legend>Weymela account</legend>
    <label>Account email<input type="email" value={email ?? ""} readOnly aria-readonly="true" /></label>
    <label>Account phone<input value={phone ?? ""} readOnly aria-readonly="true" /></label>
  </fieldset>;
}

export function ExternalOnboardingWorkspace({ kind }: { kind: "customer" | "creator" | "business" }) {
  const session = getExternalSession();
  const expected = kind === "customer" ? "Customer" : kind === "creator" ? "Creator" : "MerchantAdmin";
  const [customer, setCustomer] = useState(customerBlank);
  const [creator, setCreator] = useState(creatorBlank);
  const [socials, setSocials] = useState<SocialRow[]>([{ platform: "TikTok", profileUrl: "", audienceCount: "" }]);
  const [business, setBusiness] = useState(businessBlank);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  if (!session?.isOnboarding || session.role !== expected) {
    location.replace(session?.destination ?? "/");
    return null;
  }
  function addSocial() {
    const platform = platforms.find(item => !socials.some(row => row.platform === item.value))?.value;
    if (platform) setSocials([...socials, { platform, profileUrl: "", audienceCount: "" }]);
  }
  function updateSocial(index: number, value: Partial<SocialRow>) {
    setSocials(socials.map((row, rowIndex) => rowIndex === index ? { ...row, ...value } : row));
  }
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setMessage("");
    try {
      let body: object;
      if (kind === "customer") body = customer;
      else if (kind === "creator") {
        if (socials.length === 0) throw new Error("Add at least one social profile.");
        const socialProfiles = socials.map(row => {
          if (!row.profileUrl.trim() || row.audienceCount === "")
            throw new Error(`Complete both fields for ${row.platform}.`);
          const audienceCount = Number(row.audienceCount);
          if (!Number.isSafeInteger(audienceCount) || audienceCount < 0)
            throw new Error(`${row.platform} audience count must be a non-negative integer.`);
          return { platform: row.platform, profileUrl: row.profileUrl.trim(), audienceCount };
        });
        body = { ...creator, socialProfiles };
      } else {
        const businessContactPhone = business.businessContactPhone.trim();
        body = { ...business, businessContactPhone: businessContactPhone
          ? normalizeEthiopianPhone(businessContactPhone) ?? businessContactPhone : null,
          businessContactEmail: business.businessContactEmail.trim() || null };
      }
      const result = await api<{ destination: string }>(`/api/v1/integration/v3/onboarding/${kind}`, { method: "POST", body: JSON.stringify(body) });
      const refreshed = await loadExternalSession(); setExternalSession(refreshed.session);
      location.replace(result.destination);
    } catch (error) { setMessage((error as Error).message); }
    finally { setBusy(false); }
  }
  const field = (state: Record<string, unknown>, set: (value: any) => void, key: string, label: string, type = "text", required = true) => <label>{label}<input type={type} required={required} value={String(state[key] ?? "")} onChange={(event) => set({ ...state, [key]: event.target.value })} /></label>;
  return <main className={`auth external-onboarding external-onboarding-${kind}`}><section className="auth-card"><p className="eyebrow">WEYMELA</p><h1>{kind === "customer" ? "Customer Sign Up" : kind === "creator" ? "Creator Sign Up" : "Business Sign Up"}</h1><p className="auth-intro">Complete your {kind === "business" ? "Business" : kind === "creator" ? "Creator" : "Customer"} profile. Your Weymela sign-in is already secured.</p>
    <form className="form" onSubmit={submit}>
      {kind === "customer" && field(customer, setCustomer, "displayName", "Preferred name")}
      {kind === "creator" && <>
        <AccountIdentity email={session.accountEmail} phone={session.accountPhone} />
        {field(creator, setCreator, "firstName", "Legal First Name")}{field(creator, setCreator, "lastName", "Father's / Last Name")}{field(creator, setCreator, "displayName", "Public Display Name")}{field(creator, setCreator, "city", "Primary City")}{field(creator, setCreator, "biography", "Biography", "text", false)}{field(creator, setCreator, "contentCategories", "Content Categories", "text", false)}
        <fieldset className="social-profiles full"><legend>Social profiles</legend>{socials.map((row, index) => {
          const definition = platforms.find(item => item.value === row.platform)!;
          return <div className="social-profile-row" key={row.platform}>
            <label>Platform<select value={row.platform} onChange={event => updateSocial(index, { platform: event.target.value as SocialPlatform })}>{platforms.map(item => <option key={item.value} value={item.value} disabled={socials.some((current, currentIndex) => currentIndex !== index && current.platform === item.value)}>{item.label}</option>)}</select></label>
            <label>{definition.url}<input type="url" required value={row.profileUrl} onChange={event => updateSocial(index, { profileUrl: event.target.value })} /></label>
            <label>{definition.audience}<input type="number" min="0" step="1" required value={row.audienceCount} onChange={event => updateSocial(index, { audienceCount: event.target.value })} /></label>
            <button type="button" className="quiet remove-social" disabled={socials.length === 1} onClick={() => setSocials(socials.filter((_, rowIndex) => rowIndex !== index))}>Remove {row.platform}</button>
          </div>;
        })}<button type="button" className="quiet add-social" disabled={socials.length === platforms.length} onClick={addSocial}>Add social platform</button></fieldset>
      </>}
      {kind === "business" && <>
        <AccountIdentity email={session.accountEmail} phone={session.accountPhone} />
        {field(business, setBusiness, "tradingName", "Trading Name")}<label>Business Type<select required value={business.businessType} onChange={(event) => setBusiness({ ...business, businessType: event.target.value })}><option value="">Select business type</option>{businessTypes.map((item) => <option key={item.value} value={item.value}>{item.en}</option>)}</select></label>{field(business, setBusiness, "primaryContactName", "Primary Contact Name")}
        <fieldset className="business-contact full"><legend>Business contact information</legend>{field(business, setBusiness, "businessContactPhone", "Business Contact Phone", "tel", false)}{field(business, setBusiness, "businessContactEmail", "Business Contact Email", "email", false)}</fieldset>
        {field(business, setBusiness, "businessAddress", "Business Address")}{field(business, setBusiness, "city", "Primary City")}{field(business, setBusiness, "region", "Region")}
      </>}
      {message && <p className="error" role="alert">{message}</p>}
      <div className="actions full onboarding-actions"><button disabled={busy}>{busy ? "Saving…" : "Continue"}</button><a className="quiet onboarding-back-link" href="/onboarding">Back to profiles</a></div>
    </form></section></main>;
}
