import {CSSProperties,useEffect,useState} from 'react'
import {buildProfilePhotoUrl, normalizeProfilePhotoUrl} from './photoUpload'

export const creatorPhotoUrl=(publicCreatorId?:string,version?:string)=>publicCreatorId?buildProfilePhotoUrl(publicCreatorId,version):undefined

export const initials=(value?:string)=>{
  const tokens=(value??'').trim().split(/\s+/).filter(Boolean)
  if(tokens.length===0)return '•'
  return tokens.slice(0,2).map(x=>x[0]!.toUpperCase()).join('')
}

export function ProfileAvatar({name,photoUrl,className='',style}:{name?:string;photoUrl?:string;className?:string;style?:CSSProperties}){
  const[failed,setFailed]=useState(false)
  useEffect(()=>{setFailed(false)},[photoUrl])
  const label=initials(name)
  const resolvedPhotoUrl=normalizeProfilePhotoUrl(photoUrl)?.trim()
  if(resolvedPhotoUrl&&!failed)return <span className={`avatar creator-avatar ${className}`.trim()} style={style}><img key={resolvedPhotoUrl} src={resolvedPhotoUrl} alt="" loading="eager" decoding="async" referrerPolicy="no-referrer" onError={()=>setFailed(true)} /></span>
  return <span className={`avatar creator-avatar ${className}`.trim()} style={style} aria-hidden="true">{label}</span>
}
