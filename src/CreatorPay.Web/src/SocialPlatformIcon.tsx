import type{SVGProps}from'react'

export type SocialPlatform='TikTok'|'Instagram'|'YouTube'|'Facebook'

export function SocialPlatformIcon({platform,size=22}:{platform:SocialPlatform;size?:number}){
 const common:SVGProps<SVGSVGElement>={viewBox:'0 0 24 24',width:size,height:size,role:'img','aria-label':platform,fill:'none',stroke:'currentColor',strokeWidth:1.9,strokeLinecap:'round',strokeLinejoin:'round'}
 if(platform==='TikTok')return <svg {...common}><path d="M14 4v10.2a4.2 4.2 0 1 1-3.2-4.1"/><path d="M14 4c.8 2.7 2.4 4.1 5 4.2"/></svg>
 if(platform==='Instagram')return <svg {...common}><rect x="3.5" y="3.5" width="17" height="17" rx="5"/><circle cx="12" cy="12" r="4"/><circle cx="17.5" cy="6.6" r=".7" fill="currentColor" stroke="none"/></svg>
 if(platform==='YouTube')return <svg {...common}><path d="M21 12c0 3.7-.5 5.8-1.3 6.5S16.1 19.5 12 19.5s-6.9-.3-7.7-1S3 15.7 3 12s.5-5.8 1.3-6.5 3.6-1 7.7-1 6.9.3 7.7 1S21 8.3 21 12Z"/><path d="m10 9 5 3-5 3Z" fill="currentColor"/></svg>
 return <svg {...common}><path d="M14 21v-8h3l.5-3H14V8.2c0-1 .4-1.7 1.8-1.7H18V3.8c-.7-.1-1.6-.3-2.8-.3-2.8 0-4.7 1.7-4.7 4.8V10H8v3h2.5v8"/></svg>
}
