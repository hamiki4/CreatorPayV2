import { FormEvent, ReactNode, useEffect, useState } from "react";
import { api, ApiError } from "./apiClient";
import { clearSession } from "./sessionStore";

type Status={isEligible:boolean;hasBirthDate:boolean;isPinEnrolled:boolean;isLocked:boolean;failedAttemptCount:number};
const validPin=(value:string)=>/^\d{5}$/.test(value);
const dateValue=(day:string,month:string,year:string)=>{
  const d=Number(day),m=Number(month),y=Number(year),value=new Date(Date.UTC(y,m-1,d));
  return y>=1900&&y<=new Date().getUTCFullYear()&&value.getUTCFullYear()===y&&value.getUTCMonth()===m-1&&value.getUTCDate()===d?`${String(y).padStart(4,"0")}-${String(m).padStart(2,"0")}-${String(d).padStart(2,"0")}`:null;
};
function BirthDate({day,month,year,setDay,setMonth,setYear}:{day:string;month:string;year:string;setDay:(v:string)=>void;setMonth:(v:string)=>void;setYear:(v:string)=>void}){return <fieldset className="birth-date"><legend>Birth Date</legend><label>Day<input inputMode="numeric" type="number" min="1" max="31" required value={day} onChange={e=>setDay(e.target.value)}/></label><label>Month<input inputMode="numeric" type="number" min="1" max="12" required value={month} onChange={e=>setMonth(e.target.value)}/></label><label>Year<input inputMode="numeric" type="number" min="1900" max={new Date().getUTCFullYear()} required value={year} onChange={e=>setYear(e.target.value)}/></label></fieldset>}
export function PinEntry({value,onChange,label}:{value:string;onChange:(v:string)=>void;label:string}){return <label className="pin-entry"><span>{label}</span><input inputMode="numeric" autoComplete="new-password" pattern="[0-9]{5}" maxLength={5} required value={value} onChange={e=>onChange(e.target.value.replace(/\D/g,"").slice(0,5))}/><span className="pin-cells" aria-hidden="true">{Array.from({length:5},(_,i)=><i key={i}>{value[i]?"•":""}</i>)}</span></label>}

export function PinEnrollmentGate({children}:{children:ReactNode}){
  const [status,setStatus]=useState<Status|null>(null),[pin,setPin]=useState(""),[confirmation,setConfirmation]=useState(""),[day,setDay]=useState(""),[month,setMonth]=useState(""),[year,setYear]=useState(""),[message,setMessage]=useState(""),[busy,setBusy]=useState(false);
  const load=()=>api<Status>("/api/v1/auth/pin/status").then(setStatus).catch(e=>setMessage((e as Error).message));
  useEffect(()=>{void load()},[]);
  if(!status)return <main className="auth"><section className="panel"><p>{message||"Preparing secure sign in…"}</p></section></main>;
  if(!status.isEligible||status.isPinEnrolled)return <>{children}</>;
  async function enroll(e:FormEvent){e.preventDefault();if(!validPin(pin)||pin!==confirmation){setMessage("PIN must contain exactly 5 numeric digits and match confirmation.");return}const birthDate=status!.hasBirthDate?undefined:dateValue(day,month,year);if(!status!.hasBirthDate&&!birthDate){setMessage("Enter a valid birth date.");return}setBusy(true);setMessage("");try{await api("/api/v1/auth/pin/enroll",{method:"POST",body:JSON.stringify({pin,confirmation,birthDate})});await load()}catch(e){setMessage((e as ApiError).message)}finally{setBusy(false)}}
  return <main className="auth pin-auth"><section className="panel pin-panel"><p className="pin-brand">WEYMELA</p><h1>{status.hasBirthDate?"Create 5-digit PIN":"Complete your secure setup"}</h1><form onSubmit={enroll}>{!status.hasBirthDate&&<BirthDate day={day} month={month} year={year} setDay={setDay} setMonth={setMonth} setYear={setYear}/>}<PinEntry label="Create 5-digit PIN" value={pin} onChange={setPin}/><PinEntry label="Confirm PIN" value={confirmation} onChange={setConfirmation}/><button disabled={busy}>Create PIN</button>{message&&<p role="alert">{message}</p>}</form></section></main>;
}
export async function pinUnlock(phoneNumber:string,pin:string){if(!validPin(pin))throw new Error("Enter your 5-digit PIN.");return api<{accessToken:string;refreshToken:string;user:{role:string}}>("/api/v1/auth/pin/unlock",{method:"POST",body:JSON.stringify({phoneNumber,pin})})}
export function ForgotPin({phoneNumber,onBack}:{phoneNumber:string;onBack:()=>void}){
  const [day,setDay]=useState(""),[month,setMonth]=useState(""),[year,setYear]=useState(""),[authorization,setAuthorization]=useState(""),[pin,setPin]=useState(""),[confirmation,setConfirmation]=useState(""),[message,setMessage]=useState("");
  async function verify(e:FormEvent){e.preventDefault();const birthDate=dateValue(day,month,year);if(!birthDate){setMessage("Enter a valid birth date.");return}try{const x=await api<{resetAuthorization:string}>("/api/v1/auth/pin/recovery-proof",{method:"POST",body:JSON.stringify({phoneNumber,birthDate})});setAuthorization(x.resetAuthorization);setMessage("")}catch(e){setMessage((e as Error).message)}}
  async function reset(e:FormEvent){e.preventDefault();if(!validPin(pin)||pin!==confirmation){setMessage("PIN must contain exactly 5 numeric digits and match confirmation.");return}try{await api("/api/v1/auth/pin/reset",{method:"POST",body:JSON.stringify({resetAuthorization:authorization,newPin:pin,confirmation})});await clearSession();setMessage("PIN reset. Sign in with your new PIN.");setAuthorization("");setPin("");setConfirmation("")}catch(e){setMessage((e as Error).message)}}
  return <form onSubmit={authorization?reset:verify}><h1>{authorization?"Create New 5-digit PIN":"Forgot PIN"}</h1>{authorization?<><PinEntry label="New PIN" value={pin} onChange={setPin}/><PinEntry label="Confirm PIN" value={confirmation} onChange={setConfirmation}/></>:<BirthDate day={day} month={month} year={year} setDay={setDay} setMonth={setMonth} setYear={setYear}/>}<button>{authorization?"Reset PIN":"Continue"}</button><button type="button" className="quiet" onClick={onBack}>Back</button>{message&&<p role="alert">{message}</p>}</form>
}
