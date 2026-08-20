import {handleUnauthorized} from './authSession'
import {getAccessToken,getRefreshToken,setSessionTokens} from './sessionStore'
const apiBase=(import.meta.env.VITE_API_URL??'').replace(/\/$/,'')
const token=getAccessToken

export class ApiError extends Error{constructor(message:string,readonly technical:string,readonly status:number=0,readonly title?:string){super(message)}}

let refreshInFlight:Promise<boolean>|undefined
async function refreshAccessToken(){
  const refreshToken=getRefreshToken()
  if(!refreshToken)return false
  refreshInFlight??=fetch(`${apiBase}/api/v1/auth/refresh`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({refreshToken})}).then(async response=>{
    if(!response.ok)return false
    const value=await response.json() as {accessToken:string;refreshToken:string}
    await setSessionTokens(value.accessToken,value.refreshToken)
    return true
  }).catch(()=>false).finally(()=>{refreshInFlight=undefined})
  return refreshInFlight
}

export async function api<T>(path:string,init?:RequestInit,retry=true):Promise<T>{
  try{
    const accessToken=token()
    const response=await fetch(`${apiBase}${path}`,{...init,headers:{...(init?.body instanceof FormData?{}:{'Content-Type':'application/json'}),...(accessToken?{Authorization:`Bearer ${accessToken}`}:{ }),...init?.headers}})
    if(response.status===401&&retry&&await refreshAccessToken())return api<T>(path,init,false)
    if(handleUnauthorized(response.status))throw new ApiError('Your session has expired. Please sign in again.',`Authentication required at ${path}.`)
    const contentType=response.headers.get('content-type')?.toLowerCase()??''
    if(response.status===204)return undefined as T
    if(!contentType.includes('json'))throw new ApiError('This section is temporarily unavailable.',`Unexpected ${contentType||'unknown'} response from ${path} (${response.status}).`)
    const body=await response.json().catch(error=>{throw new ApiError('This section is temporarily unavailable.',`Invalid JSON from ${path}: ${String(error)}`)})
    if(!response.ok){
      const detail=body&&typeof body==='object'&&'detail'in body&&typeof body.detail==='string'?body.detail:null
      const title=body&&typeof body==='object'&&'title'in body&&typeof body.title==='string'?body.title:null
      throw new ApiError(response.status>=500?'This section is temporarily unavailable.':detail??'We could not complete that request.',`${detail??'Request failed'} (${response.status}) at ${path}`,response.status,title??undefined)
    }
    return body as T
  }catch(error){
    if(error instanceof ApiError){console.error(error.technical);throw error}
    console.error(`API request failed at ${path}`,error)
    throw new ApiError('This section is temporarily unavailable.',String(error))
  }
}

export async function apiBlob(path:string,retry=true):Promise<Blob>{
  try{
    const accessToken=token()
    const response=await fetch(`${apiBase}${path}`,{headers:{...(accessToken?{Authorization:`Bearer ${accessToken}`}:{ })}})
    if(response.status===401&&retry&&await refreshAccessToken())return apiBlob(path,false)
    if(handleUnauthorized(response.status))throw new ApiError('Your session has expired. Please sign in again.',`Authentication required at ${path}.`)
    if(!response.ok)throw new Error(`Request failed (${response.status})`)
    return await response.blob()
  }catch(error){console.error(`API download failed at ${path}`,error);throw new ApiError('This section is temporarily unavailable.',String(error))}
}

export const statusLabel=(value:string)=>value.replace(/([a-z])([A-Z])/g,'$1 $2')
