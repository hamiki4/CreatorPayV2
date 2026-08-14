import { FormEvent, useEffect, useRef, useState } from "react";
import { brand } from "./brand";
import { workspaceRoute } from "./authSession";
import {takeCreatorQrPath} from './creatorQrDeepLink'
import { ForgotPin, pinUnlock } from './PinExperience'

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
  customer: "Shopper Registration",
  creator: "Content Creator Registration",
  business: "Business Owner Registration",
  phone: "Phone",
  email: "Email",
  password: "Password",
  confirm: "Confirm Password",
  failed: "Registration failed. Please try again.",
  invalid: "Invalid phone number or password.",
};
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
    birthDay:"",birthMonth:"",birthYear:"",
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
    birthDay:"",birthMonth:"",birthYear:"",
  }),
  businessBlank = () => ({
    legalBusinessName: "",
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
    birthDay:"",birthMonth:"",birthYear:"",
  });
const dob=(x:{birthDay:string;birthMonth:string;birthYear:string})=>{const d=Number(x.birthDay),m=Number(x.birthMonth),y=Number(x.birthYear),v=new Date(Date.UTC(y,m-1,d));return y>=1900&&y<=new Date().getUTCFullYear()&&v.getUTCFullYear()===y&&v.getUTCMonth()===m-1&&v.getUTCDate()===d?`${y}-${String(m).padStart(2,"0")}-${String(d).padStart(2,"0")}`:null};

export function AuthWorkspace() {
  const trustedPhone=localStorage.getItem("weymela_trusted_phone")??"";
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
      birthDay:"",birthMonth:"",birthYear:"",
      newPassword: "",
      confirmation: "",
    }), [pinValue,setPinValue]=useState(""), [forgotPin,setForgotPin]=useState(false);
  const shopperSubmitting = useRef(false);
  useEffect(()=>{
    if(mode==="welcome")localStorage.setItem("weymela_welcome_seen","1");
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
    setReset({ phoneNumber: "", birthDay:"",birthMonth:"",birthYear:"", newPassword: "", confirmation: "" });
    setPinValue(""); setForgotPin(false);
    shopperSubmitting.current = false;
    setMode(next);
  }
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
        setMessage(
          r.status >= 500
            ? "Sign in is temporarily unavailable."
            : (v.detail ?? text.invalid),
        );
        return;
      }
      const x = v as Tokens;
      localStorage.setItem("creatorpay_access_token", x.accessToken);
      localStorage.setItem("creatorpay_refresh_token", x.refreshToken);
      if(x.user.role!=="PlatformAdmin"&&x.user.phoneNumber)localStorage.setItem("weymela_trusted_phone",x.user.phoneNumber);
      const continuation=takeCreatorQrPath()
      location.assign(continuation??workspaceRoute(x.user.role));
    } catch (error) {
      console.error(error);
      setMessage("Sign in is temporarily unavailable.");
    } finally {
      setBusy(false);
    }
  }
  async function unlockPin(e:FormEvent){e.preventDefault();setBusy(true);setMessage("");try{const x=await pinUnlock(trustedPhone,pinValue);localStorage.setItem("creatorpay_access_token",x.accessToken);localStorage.setItem("creatorpay_refresh_token",x.refreshToken);location.assign(workspaceRoute(x.user.role))}catch(error){setMessage((error as Error).message)}finally{setBusy(false)}}
  if(mode==="pin") return <main className="auth pin-auth"><section className="panel pin-panel"><p className="pin-brand">WEYMELA</p>{forgotPin?<ForgotPin phoneNumber={trustedPhone} onBack={()=>setForgotPin(false)}/>:<form onSubmit={unlockPin}><h1>Enter your 5-digit PIN</h1><label className="pin-entry"><span className="sr-only">5-digit PIN</span><input inputMode="numeric" autoComplete="current-password" pattern="[0-9]{5}" maxLength={5} required autoFocus value={pinValue} onChange={e=>setPinValue(e.target.value.replace(/\D/g,"").slice(0,5))}/><span className="pin-cells" aria-hidden="true">{Array.from({length:5},(_,i)=><i key={i}>{pinValue[i]?"•":""}</i>)}</span></label><button disabled={busy}>Sign In</button><button type="button" className="quiet" onClick={()=>setForgotPin(true)}>Forgot PIN</button><button type="button" className="quiet" onClick={()=>changeMode("signup")}>Sign Up to Weymela</button>{message&&<p role="alert">{message}</p>}</form>}</section></main>;
  const field = (
    state: Record<string, unknown>,
    set: (x: any) => void,
    key: string,
    label: string,
    type = "text",
  ) => (
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
      <label>
        {text.password}
        <input
          type="password"
          required
          minLength={8}
          value={state.password}
          onChange={(e) => set({ ...state, password: e.target.value })}
        />
      </label>
      <label>
        {text.confirm}
        <input
          type="password"
          required
          minLength={8}
          value={state.confirmation}
          onChange={(e) => set({ ...state, confirmation: e.target.value })}
        />
      </label>
      <small className="full">
        At least 8 characters with uppercase, lowercase, a number, and a special
        character.
      </small>
    </>
  );
  const birthFields=(state:any,set:(x:any)=>void)=><fieldset className="birth-date"><legend>Birth Date</legend>{[["birthDay","Day",31],["birthMonth","Month",12],["birthYear","Year",new Date().getUTCFullYear()]].map(([key,label,max])=><label key={String(key)}>{label}<input type="number" inputMode="numeric" min={key==="birthYear"?1900:1} max={max} required value={state[key as string]} onChange={e=>set({...state,[key as string]:e.target.value})}/></label>)}</fieldset>;
  return (
    <main className="auth">
      <section className="auth-card" aria-busy={busy}>
        <p className="eyebrow">{brand.logoText}</p>
        <h1>{mode==="welcome"?"Welcome to Weymela":mode==="signup"?"Sign Up to Weymela":mode==="login"?"Sign In":"Create your account"}</h1>
        {mode==="login"&&<p className="auth-intro">Welcome back. Keep shopping, promoting, and earning with Weymela.</p>}
        {mode==="signup"&&<p className="auth-intro">Welcome to Weymela — where shoppers save, creators earn, and businesses grow.</p>}
        {mode === "welcome" ? <><p className="auth-intro welcome-subtitle">Shop. Promote. Earn.</p><div className="signup-choices"><button onClick={()=>changeMode("customer")}>{text.customer}</button><button onClick={()=>changeMode("creator")}>{text.creator}</button><button onClick={()=>changeMode("merchant")}>{text.business}</button></div><button type="button" className="quiet auth-back" onClick={()=>changeMode("login")}>Already have an account? Sign In</button></> : mode === "signup" ? <><div className="signup-choices"><button onClick={()=>changeMode("customer")}>{text.customer}</button><button onClick={()=>changeMode("creator")}>{text.creator}</button><button onClick={()=>changeMode("merchant")}>{text.business}</button></div><button type="button" className="quiet auth-back" onClick={()=>changeMode("login")}>Back to Sign In</button></> : mode === "login" ? (
          forgot ? (
            <form
              className="form"
              onSubmit={async (e) => {
                e.preventDefault();
                if(resetStage==="request"){
                  const birthDate=dob(reset);if(!birthDate){setMessage("Enter a valid birth date using Day, Month, and Year.");return}
                  const result=await post("/api/v1/auth/password-reset-requests",{phoneNumber:reset.phoneNumber,birthDate});
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
              {resetStage==="request"&&<>{field(reset, setReset, "phoneNumber", "Phone Number", "tel")}{birthFields(reset,setReset)}</>}
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
                  setReset({ phoneNumber: "", birthDay:"",birthMonth:"",birthYear:"", newPassword: "", confirmation: "" });
                  setMessage("");
                }}
              >
                Back to Sign In
              </button>
              <p className="full support-copy">Need help? Contact <a href="mailto:admin@weymela.com">Weymela Support</a> at admin@weymela.com</p>
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
                  setReset({ phoneNumber: "", birthDay:"",birthMonth:"",birthYear:"", newPassword: "", confirmation: "" });
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
              const birthDate=dob(credentials);if(!birthDate){setMessage("Enter a valid birth date using Day, Month, and Year.");return}
              shopperSubmitting.current = true;
              try {
                const result = await post("/api/v1/customers/register", {
                    ...credentials,
                    phoneNumber: p,
                    birthDate,
                  });
                if (result) {
                  setMessage(result.message ?? "Shopper account created. Sign in with your phone and password.");
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
            {birthFields(shopper,setShopper)}
            <label>
              {text.password}
              <input
                id="shopper-password"
                name="shopperPassword"
                type="password"
                required
                minLength={8}
                autoComplete="new-password"
                value={shopper.password}
                onChange={(e) =>
                  setShopper({ ...shopper, password: e.target.value })
                }
              />
            </label>
            <label>
              {text.confirm}
              <input
                id="shopper-confirm-password"
                name="shopperConfirmation"
                type="password"
                required
                minLength={8}
                autoComplete="new-password"
                value={shopper.confirmation}
                onChange={(e) =>
                  setShopper({ ...shopper, confirmation: e.target.value })
                }
              />
            </label>
            <small className="full">
              At least 8 characters with uppercase, lowercase, a number, and a
              special character.
            </small>
            <button disabled={busy}>Create shopper account</button>
          </form>
        ) : mode === "creator" ? (
          <form
            className="form"
            onSubmit={async (e) => {
              e.preventDefault();
              const p = phone(creator.phoneNumber);
              if (!p || !validPassword(creator)) return;
              const birthDate=dob(creator);if(!birthDate){setMessage("Enter a valid birth date using Day, Month, and Year.");return}
              const {
                confirmation,
                platform,
                profileUrl,
                followerCount,
                ...request
              } = creator;
              const result = await post("/api/v1/creators/register", {
                  ...request,
                  phoneNumber: p,
                  confirmation,
                  birthDate,
                  biography: "Pilot creator",
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
                setMessage(result.message ?? "Content Creator registration received. Your account is pending Platform review.");
              }
            }}
          >
            <button type="button" className="quiet auth-back registration-back" onClick={()=>changeMode("login")}>Back to Sign In</button>
            {field(creator, setCreator, "firstName", "Legal First Name")}
            {field(creator, setCreator, "lastName", "Father's Name")}
            {field(creator, setCreator, "displayName", "Public Display Name")}
            {field(creator, setCreator, "phoneNumber", text.phone, "tel")}
            {field(creator, setCreator, "email", text.email, "email")}
            {birthFields(creator,setCreator)}
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
            <button disabled={busy}>Create content creator account</button>
          </form>
        ) : (
          <form
            className="form"
            onSubmit={async (e) => {
              e.preventDefault();
              const p = phone(business.phoneNumber);
              if (!p || !validPassword(business)) return;
              const { confirmation, ...request } = business;
              const birthDate=dob(business);if(!birthDate){setMessage("Enter a valid birth date using Day, Month, and Year.");return}
              const result = await post("/api/v1/merchants/register", {
                  ...request,
                  phoneNumber: p,
                  documents: [],
                  confirmation,
                  birthDate,
                });
              if (result) {
                setMessage(result.message ?? "Business Owner registration received. Your account is pending Platform review.");
              }
            }}
          >
            <button type="button" className="quiet auth-back registration-back" onClick={()=>changeMode("login")}>Back to Sign In</button>
            {field(
              business,
              setBusiness,
              "legalBusinessName",
              "Legal Business Name",
            )}
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
            {birthFields(business,setBusiness)}
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
            <button disabled={busy}>Create business owner account</button>
          </form>
        )}
        {message && <aside role="status">{message}</aside>}
      </section>
    </main>
  );
}
