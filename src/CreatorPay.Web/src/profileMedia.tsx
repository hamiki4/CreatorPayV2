import {useEffect,useState} from 'react'

export const creatorPhotoUrl=(publicCreatorId?:string,version?:string)=>publicCreatorId?`/api/v1/discovery/creators/${encodeURIComponent(publicCreatorId)}/photo${version?`?v=${encodeURIComponent(version)}`:''}`:undefined

export const initials=(value?:string)=>{
  const tokens=(value??'').trim().split(/\s+/).filter(Boolean)
  if(tokens.length===0)return '•'
  return tokens.slice(0,2).map(x=>x[0]!.toUpperCase()).join('')
}

export function ProfileAvatar({name,photoUrl,className=''}:{name?:string;photoUrl?:string;className?:string}){
  const[failed,setFailed]=useState(false)
  useEffect(()=>{setFailed(false)},[photoUrl])
  const label=initials(name)
  if(photoUrl&&!failed)return <span className={`avatar creator-avatar ${className}`.trim()}><img src={photoUrl} alt="" onError={()=>setFailed(true)} /></span>
  return <span className={`avatar creator-avatar ${className}`.trim()} aria-hidden="true">{label}</span>
}
