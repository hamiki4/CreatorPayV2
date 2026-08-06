const CACHE='creatorpay-shell-v18'
const SHELL=['/','/offline.html','/manifest.webmanifest','/icon.svg']

self.addEventListener('install',event=>{
  event.waitUntil(caches.open(CACHE).then(cache=>cache.addAll(SHELL)).then(()=>self.skipWaiting()))
})

self.addEventListener('activate',event=>{
  event.waitUntil(caches.keys().then(keys=>Promise.all(keys.filter(key=>key!==CACHE).map(key=>caches.delete(key)))).then(()=>self.clients.claim()))
})

self.addEventListener('fetch',event=>{
  const url=new URL(event.request.url)
  if(event.request.method!=='GET'||url.pathname.startsWith('/api/')||event.request.headers.has('Authorization'))return
  event.respondWith(
    fetch(event.request).then(response=>{
      if(response.ok&&url.origin===self.location.origin){
        const copy=response.clone()
        void caches.open(CACHE).then(cache=>cache.put(event.request,copy))
      }
      return response
    }).catch(()=>caches.match(event.request).then(response=>response??(event.request.mode==='navigate'?caches.match('/offline.html'):Response.error())))
  )
})

self.addEventListener('sync',event=>{
  if(event.tag==='creatorpay-offline-sync')event.waitUntil(self.clients.matchAll({type:'window'}).then(clients=>clients.forEach(client=>client.postMessage({type:'SYNC_REQUESTED'}))))
})
