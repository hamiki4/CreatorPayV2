export type TimedRelationship={status:string;expiresAtUtc?:string;endDateUtc?:string;promotionActive?:boolean}
export function relationshipState(item:TimedRelationship,now=Date.now()){
  if(item.status==='Revoked'||item.status==='Suspended'||item.status==='Blocked')return{label:'Deactivated',daysLeft:null,tone:'deactivated'}
  if(item.status==='Rejected')return{label:'Declined',daysLeft:null,tone:'declined'}
  if(item.status!=='Approved')return{label:'Pending',daysLeft:null,tone:'pending'}
  const expires=item.expiresAtUtc??item.endDateUtc
  if(!expires)return{label:'Expired',daysLeft:0,tone:'expired'}
  const remaining=new Date(expires).getTime()-now
  if(remaining<=0)return{label:'Expired',daysLeft:0,tone:'expired'}
  if(item.promotionActive===false)return{label:'Activation Required',daysLeft:Math.ceil(remaining/86_400_000),tone:'pending'}
  const daysLeft=Math.ceil(remaining/86_400_000)
  return{label:'Active',daysLeft,tone:daysLeft<=3?'urgent':daysLeft<=7?'soon':'active'}
}
export const daysLeftText=(days:number|null,tone:string)=>days===null?'—':tone==='expired'?'Expired':tone==='urgent'?`${days} day${days===1?'':'s'} left · Ending soon`:tone==='soon'?`${days} days left · Expiring soon`:`${days} days left`
