export type TimedRelationship={status:string;relationshipState?:string;activationRequired?:boolean;expiresAtUtc?:string;endDateUtc?:string;promotionActive?:boolean}
export function relationshipState(item:TimedRelationship,now=Date.now()){
  if(item.relationshipState==='Active'){
    const expires=item.expiresAtUtc??item.endDateUtc,daysLeft=expires?Math.max(0,Math.ceil((new Date(expires).getTime()-now)/86_400_000)):null
    return{label:'Active',daysLeft,tone:daysLeft!==null&&daysLeft<=3?'urgent':daysLeft!==null&&daysLeft<=7?'soon':'active'}
  }
  if(item.relationshipState==='Declined')return{label:'Declined',daysLeft:null,tone:'declined'}
  if(item.relationshipState==='Blocked')return{label:'Blocked',daysLeft:null,tone:'deactivated'}
  if(item.relationshipState==='Suspended'||item.relationshipState==='Revoked')return{label:'Deactivated',daysLeft:null,tone:'deactivated'}
  if(item.relationshipState==='Pending')return{label:'Pending',daysLeft:null,tone:'pending'}
  if(item.relationshipState==='AwaitingVideo')return{label:'Awaiting Video',daysLeft:null,tone:'pending'}
  if(item.relationshipState==='PendingApproval')return{label:'Pending Approval',daysLeft:null,tone:'pending'}
  if(item.relationshipState==='Approved')return{label:'Approved',daysLeft:null,tone:'pending'}
  if(item.relationshipState==='Rejected')return{label:'Video Rejected',daysLeft:null,tone:'declined'}
  if(item.relationshipState==='ActivationRequired'||item.activationRequired===true)return{label:'Activation Required',daysLeft:null,tone:'pending'}
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
