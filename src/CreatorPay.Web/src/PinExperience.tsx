import { FormEvent, ReactNode, useEffect, useState } from "react";
import { api, ApiError } from "./apiClient";
import { clearSession } from "./sessionStore";
import { PasswordInput } from "./PasswordInput";

type Status={isEligible:boolean;isPinEnrolled:boolean;isLocked:boolean;failedAttemptCount:number};
const validPin=(value:string)=>/^\d{5}$/.test(value);
export function PinEntry({value,onChange,label}:{value:string;onChange:(v:string)=>void;label:string}){return <label className="pin-entry"><span>{label}</span><input inputMode="numeric" autoComplete="new-password" pattern="[0-9]{5}" maxLength={5} required value={value} onChange={e=>onChange(e.target.value.replace(/\D/g,"").slice(0,5))}/><span className="pin-cells" aria-hidden="true">{Array.from({length:5},(_,i)=><i key={i}>{value[i]?"•":""}</i>)}</span></label>}

export function PinEnrollmentGate({children}:{children:ReactNode}){
  const [status,setStatus]=useState<Status|null>(null),[pin,setPin]=useState(""),[confirmation,setConfirmation]=useState(""),[message,setMessage]=useState(""),[busy,setBusy]=useState(false);
  const load=()=>api<Status>("/api/v1/auth/pin/status").then(setStatus).catch(e=>setMessage((e as Error).message));
  useEffect(()=>{void load()},[]);
  if(!status)return <main className="auth"><section className="panel"><p>{message||"Preparing secure sign in…"}</p></section></main>;
  if(!status.isEligible||status.isPinEnrolled)return <>{children}</>;
  async function enroll(e:FormEvent){e.preventDefault();if(!validPin(pin)||pin!==confirmation){setMessage("PIN must contain exactly 5 numeric digits and match confirmation.");return}setBusy(true);setMessage("");try{await api("/api/v1/auth/pin/enroll",{method:"POST",body:JSON.stringify({pin,confirmation})});await load()}catch(e){setMessage((e as ApiError).message)}finally{setBusy(false)}}
  return <main className="auth pin-auth"><section className="panel pin-panel"><p className="pin-brand">WEYMELA</p><h1>Create 5-digit PIN</h1><form onSubmit={enroll}><PinEntry label="Create 5-digit PIN" value={pin} onChange={setPin}/><PinEntry label="Confirm PIN" value={confirmation} onChange={setConfirmation}/><button disabled={busy}>Create PIN</button>{message&&<p role="alert">{message}</p>}</form></section></main>;
}
export async function pinUnlock(phoneNumber:string,pin:string){if(!validPin(pin))throw new Error("Enter your 5-digit PIN.");return api<{accessToken:string;refreshToken:string;user:{role:string}}>("/api/v1/auth/pin/unlock",{method:"POST",body:JSON.stringify({phoneNumber,pin})})}
export function ForgotPin({phoneNumber,onBack}:{phoneNumber:string;onBack:()=>void}){
  const [password,setPassword]=useState(""),[pin,setPin]=useState(""),[confirmation,setConfirmation]=useState(""),[message,setMessage]=useState("");
  async function reset(e:FormEvent){e.preventDefault();if(!validPin(pin)||pin!==confirmation){setMessage("PIN must contain exactly 5 numeric digits and match confirmation.");return}try{await api("/api/v1/auth/pin/reset-with-password",{method:"POST",body:JSON.stringify({phoneNumber,password,newPin:pin,confirmation})});await clearSession();setMessage("PIN reset. Sign in with your new PIN.");setPassword("");setPin("");setConfirmation("")}catch(e){setMessage((e as Error).message)}}
  return <form onSubmit={reset}><h1>Reset 5-digit PIN</h1><p>Confirm your account password, then choose a new PIN.</p><PasswordInput label="Password" autoComplete="current-password" required value={password} onChange={setPassword}/><PinEntry label="New PIN" value={pin} onChange={setPin}/><PinEntry label="Confirm PIN" value={confirmation} onChange={setConfirmation}/><button>Reset PIN</button><button type="button" className="quiet" onClick={onBack}>Back</button>{message&&<p role="alert">{message}</p>}</form>
}
