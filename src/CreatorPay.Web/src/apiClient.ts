import {handleUnauthorized} from './authSession'
const apiBase=(import.meta.env.VITE_API_URL??'').replace(/\/$/,'')
const token=()=>localStorage.getItem('creatorpay_access_token')??''

export class ApiError extends Error{constructor(message:string,readonly technical:string){super(message)}}

export async function api<T>(path:string,init?:RequestInit):Promise<T>{
  try{
    const accessToken=token()
    const response=await fetch(`${apiBase}${path}`,{...init,headers:{...(init?.body instanceof FormData?{}:{'Content-Type':'application/json'}),...(accessToken?{Authorization:`Bearer ${accessToken}`}:{ }),...init?.headers}})
    if(handleUnauthorized(response.status))throw new ApiError('Your session has expired. Please sign in again.',`Authentication required at ${path}.`)
    const contentType=response.headers.get('content-type')?.toLowerCase()??''
    if(response.status===204)return undefined as T
    if(!contentType.includes('json'))throw new ApiError('This section is temporarily unavailable.',`Unexpected ${contentType||'unknown'} response from ${path} (${response.status}).`)
    const body=await response.json().catch(error=>{throw new ApiError('This section is temporarily unavailable.',`Invalid JSON from ${path}: ${String(error)}`)})
    if(!response.ok){
      const detail=body&&typeof body==='object'&&'detail'in body&&typeof body.detail==='string'?body.detail:null
      throw new ApiError(response.status>=500?'This section is temporarily unavailable.':detail??'We could not complete that request.',`${detail??'Request failed'} (${response.status}) at ${path}`)
    }
    return body as T
  }catch(error){
    if(error instanceof ApiError){console.error(error.technical);throw error}
    console.error(`API request failed at ${path}`,error)
    throw new ApiError('This section is temporarily unavailable.',String(error))
  }
}

export async function apiBlob(path:string):Promise<Blob>{
  try{
    const accessToken=token()
    const response=await fetch(`${apiBase}${path}`,{headers:{...(accessToken?{Authorization:`Bearer ${accessToken}`}:{ })}})
    if(handleUnauthorized(response.status))throw new ApiError('Your session has expired. Please sign in again.',`Authentication required at ${path}.`)
    if(!response.ok)throw new Error(`Request failed (${response.status})`)
    return await response.blob()
  }catch(error){console.error(`API download failed at ${path}`,error);throw new ApiError('This section is temporarily unavailable.',String(error))}
}

export const statusLabel=(value:string)=>value.replace(/([a-z])([A-Z])/g,'$1 $2')
