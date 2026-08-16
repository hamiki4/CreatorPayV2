const CACHE='creatorpay-shell-v21'
const LEGACY_ORIGIN='https://www.weymela.com'
const CANONICAL_ORIGIN='https://weymela.com'
const LEGACY_SELF=self.location.origin===LEGACY_ORIGIN
const SHELL=['/','/offline.html','/manifest.webmanifest','/favicon.ico','/icons/weymela-192x192.png','/icons/weymela-512x512.png']

self.addEventListener('install',event=>{
  event.waitUntil(caches.open(CACHE).then(cache=>cache.addAll(SHELL)).then(()=>self.skipWaiting()))
})

self.addEventListener('activate',event=>{
  event.waitUntil(caches.keys().then(keys=>Promise.all(keys.filter(key=>key!==CACHE||LEGACY_SELF).map(key=>caches.delete(key)))).then(async()=>{
    if(LEGACY_SELF){await self.registration.unregister();for(const client of await self.clients.matchAll({type:'window'}))await client.navigate(CANONICAL_ORIGIN+new URL(client.url).pathname+new URL(client.url).search+new URL(client.url).hash);return}
    await self.clients.claim()
  }))
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
