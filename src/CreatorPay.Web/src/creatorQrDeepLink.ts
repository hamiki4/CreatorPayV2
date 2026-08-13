const pendingKey='weymela_pending_creator_qr'
const publicIdPattern=/^[A-Za-z0-9_-]{3,128}$/
const tokenPattern=/^[A-Za-z0-9_-]{32,128}$/

export function creatorQrPath(pathname:string,search:string){
  const match=pathname.match(/^\/c\/([^/]+)$/)
  if(!match)return null
  let publicQrId:string
  try{publicQrId=decodeURIComponent(match[1])}catch{return null}
  const query=new URLSearchParams(search),token=query.get('t'),version=query.get('v')
  if([...query.keys()].some(key=>key!=='t'&&key!=='v')||query.getAll('t').length!==1||query.getAll('v').length!==1||!publicIdPattern.test(publicQrId)||!token||!tokenPattern.test(token)||version!=='1')return null
  return `/c/${encodeURIComponent(publicQrId)}?t=${encodeURIComponent(token)}&v=1`
}

export function preserveCreatorQrPath(path:string){
  const url=new URL(path,location.origin),safe=url.origin===location.origin?creatorQrPath(url.pathname,url.search):null
  if(safe)sessionStorage.setItem(pendingKey,safe)
  return safe
}

export function takeCreatorQrPath(){
  const stored=sessionStorage.getItem(pendingKey);sessionStorage.removeItem(pendingKey)
  if(!stored)return null
  try{const url=new URL(stored,location.origin);return url.origin===location.origin?creatorQrPath(url.pathname,url.search):null}catch{return null}
}

export function creatorQrPayload(path:string){return new URL(path,location.origin).toString()}
