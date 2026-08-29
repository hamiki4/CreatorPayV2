import {useEffect,useState} from 'react'
import {api} from './apiClient'
import {AccountStatusBadge} from './AccountChrome'

type FileMetadata={fileName:string;contentType:string;sizeBytes:number}
type DocumentMetadata=FileMetadata&{documentType:string}
type Profile={publicMerchantId:string;legalBusinessName:string;tradingName:string;businessType:string;primaryContactName:string;taxRegistrationNumber?:string;phoneNumber:string;email:string;businessAddress:string;city:string;region:string;country:string;timeZone:string;merchantStatus:string;accountStatus:string;effectiveStatus:string;effectiveStatusReason:string;registrationProgress:number;nextStep:string;isEmailVerified:boolean;isPhoneVerified:boolean;logo?:FileMetadata;documents:DocumentMetadata[]}
export function MerchantWorkspace(){
 const[profile,setProfile]=useState<Profile>(),[error,setError]=useState('')
 useEffect(()=>{void api<Profile>('/api/v1/merchants/me').then(setProfile).catch(e=>{console.error(e);setError('This section is temporarily unavailable.')})},[])
 if(!profile)return error?<section className="creator-section" role="alert"><h2>Profile</h2><p className="friendly-error">{error}</p></section>:<p>Loading profile…</p>
 return <section className="creator-section" aria-labelledby="business-profile"><h2 id="business-profile">Profile</h2>{error&&<p className="friendly-error" role="alert">{error}</p>}<div className="compact-panel profile-details"><strong>{profile.tradingName}</strong><dl><div><dt>Business Type</dt><dd>{profile.businessType}</dd></div><div><dt>Primary Contact</dt><dd>{profile.primaryContactName}</dd></div><div><dt>Phone</dt><dd>{profile.phoneNumber}</dd></div><div><dt>Email</dt><dd>{profile.email||'No email provided'}</dd></div><div><dt>Address</dt><dd>{profile.businessAddress}</dd></div><div><dt>Primary City</dt><dd>{profile.city}</dd></div><div><dt>Business ID</dt><dd>{profile.publicMerchantId}</dd></div><div><dt>Status</dt><dd><AccountStatusBadge status={profile.effectiveStatus} /></dd></div></dl></div></section>
}
