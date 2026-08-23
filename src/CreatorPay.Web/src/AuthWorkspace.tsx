import { FormEvent, useEffect, useRef, useState } from "react";
import { brand } from "./brand";
import { workspaceRoute } from "./authSession";
import {takeCreatorQrPath} from './creatorQrDeepLink'
import { ForgotPin, pinUnlock } from './PinExperience'
import {getTrustedPhone,setSessionTokens,setTrustedPhone} from './sessionStore'
import {NATIVE_BACK_EVENT} from './mobileLifecycle'
import { PasswordInput } from './PasswordInput'
import { ApiError } from "./apiClient";

const base = (import.meta.env.VITE_API_URL ?? "").replace(/\/$/, "");
type Mode = "welcome" | "login" | "pin" | "signup" | "customer" | "creator" | "merchant";
type Tokens = {
  accessToken: string;
  refreshToken: string;
  user: { role: string; phoneNumber?: string };
};
export const businessTypes = [
  { value: "Restaurant / Café", en: "Restaurant / Café" },
  { value: "Grocery / Mini-market", en: "Grocery / Mini-market" },
  { value: "Clothing / Boutique", en: "Clothing / Boutique" },
  { value: "Beauty / Salon", en: "Beauty / Salon" },
  { value: "Furniture", en: "Furniture" },
  { value: "Electronics", en: "Electronics" },
  { value: "Hotel / Travel", en: "Hotel / Travel" },
  { value: "Professional Services", en: "Professional Services" },
  { value: "Other", en: "Other" },
] as const;
const text = {
  welcome: "Welcome to Weymela",
  signIn: "Sign In",
  customer: "Customer Sign Up",
  creator: "Creator Sign Up",
  business: "Business Sign Up",
  phone: "Phone",
  email: "Email",
  password: "Password",
  confirm: "Confirm Password",
  failed: "Sign up failed. Please try again.",
  invalid: "Invalid phone number or password.",
  customerSuccess: "Customer sign up successful. Sign in with your phone and password.",
  creatorSuccess: "Creator sign up successful. Your account is pending Platform review.",
  businessSuccess: "Business sign up successful. Your account is pending Platform review.",
};
const authRestrictionMessages = {
  AccountDeactivated: "Your account is deactivated. Please contact Weymela support.",
  AccountSuspended: "Your account is suspended. Please contact Weymela support.",
  AccountLocked: "Your account is locked. Please contact Weymela support.",
} as const;
function authErrorMessage(status: number, body: unknown, fallback: string) {
  if (status === 403 && body && typeof body === "object" && "title" in body) {
    const title = String((body as { title?: unknown }).title ?? "");
    if (title in authRestrictionMessages) return authRestrictionMessages[title as keyof typeof authRestrictionMessages];
  }
  if (body && typeof body === "object" && "detail" in body && typeof (body as { detail?: unknown }).detail === "string") return (body as { detail: string }).detail;
  return fallback;
}
export const authText = () => text;
export const publicPasswordRules = [
  (v: string) => v.length >= 8,
  (v: string) => /[A-Z]/.test(v),
  (v: string) => /[a-z]/.test(v),
  (v: string) => /\d/.test(v),
  (v: string) => /[^A-Za-z0-9]/.test(v),
];
export const ethiopianPhoneMessage =
  "Enter a valid Ethiopian mobile number, for example 0911234567, 0712345678, or +251911234567.";
export function normalizeEthiopianPhone(value: string) {
  const compact = value.replace(/[\s-]/g, "");
  if (/^0[79]\d{8}$/.test(compact)) return `+251${compact.slice(1)}`;
  if (/^\+251[79]\d{8}$/.test(compact)) return compact;
  if (/^251[79]\d{8}$/.test(compact)) return `+${compact}`;
  return null;
}
const loginBlank = () => ({ email: "", password: "" }),
  shopperBlank = () => ({
    displayName: "",
    email: "",
    phoneNumber: "",
    password: "",
    confirmation: "",
  }),
  creatorBlank = () => ({
    firstName: "",
    lastName: "",
    displayName: "",
    phoneNumber: "",
    email: "",
    password: "",
    confirmation: "",
    preferredLanguage: "en",
    city: "Addis Ababa",
    platform: "TikTok",
    profileUrl: "",
    followerCount: 0,
    termsAccepted: false,
  }),
  businessBlank = () => ({
    tradingName: "",
    businessType: "",
    primaryContactName: "",
    phoneNumber: "",
    email: "",
    password: "",
    confirmation: "",
    businessAddress: "",
    city: "Addis Ababa",
    region: "Not provided",
    country: "Ethiopia",
    timeZone: "Africa/Addis_Ababa",
    taxRegistrationNumber: null,
    businessRegistrationNumber: null,
    preferredLanguage: "en",
    termsAccepted: false,
  });
export function AuthWorkspace() {
  const trustedPhone=getTrustedPhone();
  const welcomeSeen=localStorage.getItem("weymela_welcome_seen")==="1";
  const [mode, setMode] = useState<Mode>(trustedPhone?"pin":welcomeSeen?"login":"welcome"),
    [login, setLogin] = useState(loginBlank),
    [shopper, setShopper] = useState(shopperBlank),
    [creator, setCreator] = useState(creatorBlank),
    [business, setBusiness] = useState(businessBlank),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    [forgot, setForgot] = useState(false),
    [resetStage, setResetStage] = useState<"request"|"waiting"|"approved">("request"),
    [resetReference,setResetReference]=useState(""),
    [reset, setReset] = useState({
      phoneNumber: "",
      newPassword: "",
      confirmation: "",
    }), [pinValue,setPinValue]=useState(""), [forgotPin,setForgotPin]=useState(false), [creatorSignupSettings,setCreatorSignupSettings]=useState<{minimumTikTokFollowers:number}|null>(null), [creatorSettingsLoading,setCreatorSettingsLoading]=useState(false);
  const shopperSubmitting = useRef(false);
  useEffect(()=>{
    if(mode==="welcome")localStorage.setItem("weymela_welcome_seen","1");
  },[mode]);
  useEffect(()=>{
    if(mode!=="creator"){setCreatorSignupSettings(null);setCreatorSettingsLoading(false);return}
    let active=true;
    setCreatorSettingsLoading(true);
    fetch(`${base}/api/v1/auth/signup-settings`)
      .then(async response=>{
        const value=await response.json().catch(()=>({minimumTikTokFollowers:0}));
        if(!active)return;
        if(!response.ok)throw new Error((value as {detail?:string}).detail??"Unable to load creator signup settings.");
        setCreatorSignupSettings(value as {minimumTikTokFollowers:number});
      })
      .catch(error=>{
        console.error(error);
        if(active)setCreatorSignupSettings({minimumTikTokFollowers:0});
      })
      .finally(()=>{if(active)setCreatorSettingsLoading(false)});
    return()=>{active=false}
  },[mode]);
  function changeMode(next: Mode) {
    if (next === mode) return;
    setLogin(loginBlank());
    setShopper(shopperBlank());
    setCreator(creatorBlank());
    setBusiness(businessBlank());
    setMessage("");
    setForgot(false);
    setResetStage("request");setResetReference("");
    setReset({ phoneNumber: "", newPassword: "", confirmation: "" });
    setPinValue(""); setForgotPin(false);
    shopperSubmitting.current = false;
    setMode(next);
  }
  useEffect(()=>{
    const back=(event:Event)=>{
      if(forgotPin){event.preventDefault();setForgotPin(false);return}
      if(forgot){event.preventDefault();setForgot(false);setResetStage("request");setResetReference("");return}
      if(mode==="customer"||mode==="creator"||mode==="merchant"||mode==="signup"){
        event.preventDefault();changeMode("login")
      }
    }
    addEventListener(NATIVE_BACK_EVENT,back)
    return()=>removeEventListener(NATIVE_BACK_EVENT,back)
  },[mode,forgot,forgotPin])
  async function post(path: string, body: unknown) {
    setBusy(true);
    setMessage("");
    try {
      const r = await fetch(`${base}${path}`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        }),
        v = await r.json().catch(() => ({}));
      if (!r.ok) {
        setMessage(r.status >= 500 ? text.failed : (v.detail ?? text.failed));
        return false;
      }
      return v as { message?: string; reference?:string; status?:string };
    } catch (error) {
      console.error(error);
      setMessage(text.failed);
      return false;
    } finally {
      setBusy(false);
    }
  }
  function phone(value: string) {
    const normalized = normalizeEthiopianPhone(value);
    if (!normalized) setMessage(ethiopianPhoneMessage);
    return normalized;
  }
  function validPassword(x: { password: string; confirmation: string }) {
    if (!publicPasswordRules.every((rule) => rule(x.password))) {
      setMessage(
        "Use at least 8 characters with uppercase, lowercase, a number, and a special character.",
      );
      return false;
    }
    if (x.password !== x.confirmation) {
      setMessage("Passwords do not match.");
      return false;
    }
    return true;
  }
  function minimumTikTokFollowersText() {
    const minimum = creatorSignupSettings?.minimumTikTokFollowers ?? 0;
    return `Minimum required TikTok followers: ${new Intl.NumberFormat('en-US').format(minimum)}.`;
  }
  async function signIn(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setMessage("");
    try {
      const r = await fetch(`${base}/api/v1/auth/login`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(login),
        }),
        v = await r.json().catch(() => ({}));
      if (!r.ok) {
        setMessage(r.status >= 500 ? "Sign in is temporarily unavailable." : authErrorMessage(r.status, v, text.invalid));
        return;
      }
      const x = v as Tokens;
      await setSessionTokens(x.accessToken,x.refreshToken);
      if(x.user.role!=="PlatformAdmin"&&x.user.phoneNumber)await setTrustedPhone(x.user.phoneNumber);
      const continuation=takeCreatorQrPath()
      location.assign(continuation??workspaceRoute(x.user.role));
    } catch (error) {
      console.error(error);
      setMessage("Sign in is temporarily unavailable.");
    } finally {
      setBusy(false);
    }
  }
  async function unlockPin(e:FormEvent){e.preventDefault();setBusy(true);setMessage("");try{const x=await pinUnlock(trustedPhone,pinValue);await setSessionTokens(x.accessToken,x.refreshToken);location.assign(workspaceRoute(x.user.role))}catch(error){setMessage(error instanceof ApiError&&error.title&&error.title in authRestrictionMessages?authRestrictionMessages[error.title as keyof typeof authRestrictionMessages]:(error as Error).message)}finally{setBusy(false)}}
  if(mode==="pin") return <main className="auth pin-auth"><section className="panel pin-panel"><p className="pin-brand">WEYMELA</p>{forgotPin?<ForgotPin phoneNumber={trustedPhone} onBack={()=>setForgotPin(false)}/>:<form onSubmit={unlockPin}><h1>Enter your 5-digit PIN</h1><label className="pin-entry"><span className="sr-only">5-digit PIN</span><input inputMode="numeric" autoComplete="current-password" pattern="[0-9]{5}" maxLength={5} required autoFocus value={pinValue} onChange={e=>setPinValue(e.target.value.replace(/\D/g,"").slice(0,5))}/><span className="pin-cells" aria-hidden="true">{Array.from({length:5},(_,i)=><i key={i}>{pinValue[i]?"•":""}</i>)}</span></label><button disabled={busy}>Sign In</button><button type="button" className="quiet" onClick={()=>setForgotPin(true)}>Forgot PIN</button><button type="button" className="quiet" onClick={()=>changeMode("signup")}>Sign Up to Weymela</button>{message&&<p role="alert">{message}</p>}</form>}</section></main>;
  const field = (
    state: Record<string, unknown>,
    set: (x: any) => void,
    key: string,
    label: string,
    type = "text",
  ) => type === "password" ? <PasswordInput
      label={label}
      required
      autoComplete={key === "password" && mode === "login" ? "current-password" : "new-password"}
      value={String(state[key] ?? "")}
      onChange={value => set({...state,[key]:value})}
    /> : (
    <label>
      {label}
      <input
        type={type}
        required={key !== "email" || mode === "login"}
        autoComplete={
          mode === "login" && key === "email"
            ? "tel"
            : key === "email"
              ? "email"
              : key === "phoneNumber"
              ? "tel"
              : key === "firstName" || key === "lastName" || key === "displayName" || key === "primaryContactName"
                ? "name"
                : key === "newPassword" || key === "confirmation"
                  ? "new-password"
                  : key === "password"
                    ? mode === "login" ? "current-password" : "new-password"
                    : undefined
        }
        value={String(state[key] ?? "")}
        onChange={(e) =>
          set({
            ...state,
            [key]: type === "number" ? Number(e.target.value) : e.target.value,
          })
        }
      />
    </label>
  );
  const passwords = (
    state: { password: string; confirmation: string },
    set: (x: any) => void,
  ) => (
    <>
      <PasswordInput label={text.password} required minLength={8} autoComplete="new-password" value={state.password} onChange={value=>set({...state,password:value})}/>
      <PasswordInput label={text.confirm} required minLength={8} autoComplete="new-password" value={state.confirmation} onChange={value=>set({...state,confirmation:value})}/>
      <small className="full">
        At least 8 characters with uppercase, lowercase, a number, and a special
        character.
      </small>
    </>
  );
  return (
    <main className="auth">
      <section className="auth-card" aria-busy={busy}>
        <p className="eyebrow">{brand.logoText}</p>
        <h1>{mode==="welcome"?"Welcome to Weymela":mode==="signup"?"Sign Up to Weymela":mode==="login"?"Sign In":mode==="customer"?text.customer:mode==="creator"?text.creator:text.business}</h1>
        {mode==="login"&&<p className="auth-intro">Welcome back. Keep shopping, promoting, and earning with Weymela.</p>}
        {mode==="signup"&&<p className="auth-intro">Welcome to Weymela — where customers save, creators earn, and businesses grow.</p>}
        {mode === "welcome" ? <><p className="auth-intro welcome-subtitle">Shop. Promote. Earn.</p><div className="signup-choices"><button onClick={()=>changeMode("customer")}>{text.customer}</button><button onClick={()=>changeMode("creator")}>{text.creator}</button><button onClick={()=>changeMode("merchant")}>{text.business}</button></div><button type="button" className="quiet auth-back" onClick={()=>changeMode("login")}>Already have an account? Sign In</button></> : mode === "signup" ? <><div className="signup-choices"><button onClick={()=>changeMode("customer")}>{text.customer}</button><button onClick={()=>changeMode("creator")}>{text.creator}</button><button onClick={()=>changeMode("merchant")}>{text.business}</button></div><button type="button" className="quiet auth-back" onClick={()=>changeMode("login")}>Back to Sign In</button></> : mode === "login" ? (
          forgot ? (
            <form
              className="form"
              onSubmit={async (e) => {
                e.preventDefault();
                if(resetStage==="request"){
                  const result=await post("/api/v1/auth/password-reset-requests",{phoneNumber:reset.phoneNumber});
                  if(result){setResetReference(result.reference??"");setResetStage("waiting");setMessage("Your password reset request is waiting for support approval.")}
                  return;
                }
                if(resetStage==="waiting"){
                  setBusy(true);const r=await fetch(`${base}/api/v1/auth/password-reset-requests/${encodeURIComponent(resetReference)}`);const v=await r.json().catch(()=>({}));setBusy(false);if(v.status==="Approved"){setResetStage("approved");setMessage("Your request was approved. Create a new password.")}else setMessage(v.message??"Your password reset request is waiting for support approval.");return
                }
                if(!validPassword({password:reset.newPassword,confirmation:reset.confirmation}))return;
                if(await post("/api/v1/auth/reset-password",{resetToken:resetReference,newPassword:reset.newPassword,confirmation:reset.confirmation})){setMessage("Password reset. You can sign in now.");setResetStage("request")}
              }}
            >
              <h2>Forgot Password</h2>
              <p className="full">Request help resetting your password.</p>
              {resetStage==="request"&&field(reset, setReset, "phoneNumber", "Phone Number", "tel")}
              {resetStage==="approved" && (
                <>
                  {field(
                    reset,
                    setReset,
                    "newPassword",
                    "New Password",
                    "password",
                  )}
                  {field(
                    reset,
                    setReset,
                    "confirmation",
                    "Confirm Password",
                    "password",
                  )}
                </>
              )}
              <button disabled={busy}>
                {resetStage==="request"?"Request Password Reset":resetStage==="waiting"?"Check Approval Status":"Reset Password"}
              </button>
              <button
                type="button"
                className="quiet"
                onClick={() => {
                  setForgot(false);
                  setResetStage("request");setResetReference("");
                  setReset({ phoneNumber: "", newPassword: "", confirmation: "" });
                  setMessage("");
                }}
              >
                Back to Sign In
              </button>
              <p className="full support-copy">
                Need help?{" "}
                <a href="mailto:support@weymela.com?subject=Weymela%20Support%20Request">
                  Contact Weymela Support
                </a>
              </p>
            </form>
          ) : (
            <form className="form" onSubmit={signIn}>
              {field(login, setLogin, "email", "Phone Number")}
              {field(login, setLogin, "password", text.password, "password")}
              <button disabled={busy}>{text.signIn}</button>
              <button
                type="button"
                className="quiet"
                onClick={() => {
                  setForgot(true);
                  setResetStage("request");setResetReference("");
                  setReset({ phoneNumber: "", newPassword: "", confirmation: "" });
                  setMessage("");
                }}
              >
                Forgot Password
              </button>
              <button type="button" className="quiet" onClick={()=>changeMode("signup")}>Sign Up to Weymela</button>
            </form>
          )
        ) : mode === "customer" ? (
          <form
            className="form"
            onSubmit={async (e) => {
              e.preventDefault();
              if (shopperSubmitting.current) return;
              const data = new FormData(e.currentTarget),
                credentials = {
                  ...shopper,
                  password: String(data.get("shopperPassword") ?? ""),
                  confirmation: String(data.get("shopperConfirmation") ?? ""),
                },
                p = phone(shopper.phoneNumber);
              if (!p || !validPassword(credentials)) return;
              shopperSubmitting.current = true;
              try {
                const result = await post("/api/v1/customers/register", {
                    ...credentials,
                    phoneNumber: p,
                  });
                if (result) {
                  setMessage(result.message ?? text.customerSuccess);
                }
              } finally {
                shopperSubmitting.current = false;
              }
            }}
          >
            <button type="button" className="quiet auth-back registration-back" onClick={()=>changeMode("login")}>Back to Sign In</button>
            {field(shopper, setShopper, "displayName", "Display Name")}
            {field(shopper, setShopper, "phoneNumber", text.phone, "tel")}
            {field(shopper, setShopper, "email", text.email, "email")}
            <PasswordInput label={text.password} id="shopper-password" name="shopperPassword" required minLength={8} autoComplete="new-password" value={shopper.password} onChange={value=>setShopper({...shopper,password:value})}/>
            <PasswordInput label={text.confirm} id="shopper-confirm-password" name="shopperConfirmation" required minLength={8} autoComplete="new-password" value={shopper.confirmation} onChange={value=>setShopper({...shopper,confirmation:value})}/>
            <small className="full">
              At least 8 characters with uppercase, lowercase, a number, and a
              special character.
            </small>
            <button disabled={busy}>Sign Up</button>
          </form>
        ) : mode === "creator" ? (
          <form
            className="form"
            onSubmit={async (e) => {
              e.preventDefault();
              const p = phone(creator.phoneNumber);
              if (!p || !validPassword(creator)) return;
              const {
                confirmation,
                platform,
                profileUrl,
                followerCount,
                ...request
              } = creator;
              const minimumTikTokFollowers = creatorSignupSettings?.minimumTikTokFollowers ?? 0;
              if (platform === "TikTok" && followerCount < minimumTikTokFollowers) {
                setMessage(`Minimum required TikTok followers: ${new Intl.NumberFormat('en-US').format(minimumTikTokFollowers)}.`);
                return;
              }
              const result = await post("/api/v1/creators/register", {
                  ...request,
                  phoneNumber: p,
                  confirmation,
                  biography: "Weymela creator",
                  contentCategories: "General",
                  socialProfiles: profileUrl
                    ? [
                        {
                          platform,
                          handle: creator.displayName,
                          profileUrl,
                          followerCount,
                          isPrimary: true,
                        },
                      ]
                    : [],
                });
              if (result) {
                setMessage(result.message ?? text.creatorSuccess);
              }
            }}
          >
            <button type="button" className="quiet auth-back registration-back" onClick={()=>changeMode("login")}>Back to Sign In</button>
            {field(creator, setCreator, "firstName", "Legal First Name")}
            {field(creator, setCreator, "lastName", "Father's Name")}
            {field(creator, setCreator, "displayName", "Public Display Name")}
            {field(creator, setCreator, "phoneNumber", text.phone, "tel")}
            {field(creator, setCreator, "email", text.email, "email")}
            {field(creator, setCreator, "city", "Primary City")}
            {field(creator, setCreator, "platform", "Primary Social Platform")}
            {field(
              creator,
              setCreator,
              "profileUrl",
              "Social Profile URL",
              "url",
            )}
            {field(
              creator,
              setCreator,
              "followerCount",
              "Estimated Follower Count",
              "number",
            )}
            {creator.platform === "TikTok" && (
              <small className="full">
                {creatorSettingsLoading ? "Loading TikTok follower requirement…" : minimumTikTokFollowersText()}
              </small>
            )}
            {passwords(creator, setCreator)}
            <label className="check full">
              <input
                type="checkbox"
                required
                checked={creator.termsAccepted}
                onChange={(e) =>
                  setCreator({ ...creator, termsAccepted: e.target.checked })
                }
              />
              I accept the Terms of Service and Privacy Notice
            </label>
            <button disabled={busy}>Sign Up</button>
          </form>
        ) : (
          <form
            className="form"
            onSubmit={async (e) => {
              e.preventDefault();
              const p = phone(business.phoneNumber);
              if (!p || !validPassword(business)) return;
              const { confirmation, ...request } = business;
              const result = await post("/api/v1/merchants/register", {
                  ...request,
                  phoneNumber: p,
                  documents: [],
                  confirmation,
                });
              if (result) {
                setMessage(result.message ?? text.businessSuccess);
              }
            }}
          >
            <button type="button" className="quiet auth-back registration-back" onClick={()=>changeMode("login")}>Back to Sign In</button>
            {field(business, setBusiness, "tradingName", "Trading Name")}
            <label>
              Business Type
              <select
                required
                value={business.businessType}
                onChange={(e) =>
                  setBusiness({ ...business, businessType: e.target.value })
                }
              >
                <option value="" disabled>
                  Select a business type
                </option>
                {businessTypes.map((x) => (
                  <option value={x.value} key={x.value}>
                    {x.en}
                  </option>
                ))}
              </select>
            </label>
            {field(
              business,
              setBusiness,
              "primaryContactName",
              "Primary Contact Name",
            )}
            {field(business, setBusiness, "phoneNumber", text.phone, "tel")}
            {field(business, setBusiness, "email", text.email, "email")}
            {field(
              business,
              setBusiness,
              "businessAddress",
              "Business Address",
            )}
            {field(business, setBusiness, "city", "Primary City")}
            {passwords(business, setBusiness)}
            <label className="check full">
              <input
                type="checkbox"
                required
                checked={business.termsAccepted}
                onChange={(e) =>
                  setBusiness({ ...business, termsAccepted: e.target.checked })
                }
              />
              I accept the Terms of Service and Privacy Notice
            </label>
            <button disabled={busy}>Sign Up</button>
          </form>
        )}
        {message && <aside role="status">{message}</aside>}
      </section>
    </main>
  );
}
