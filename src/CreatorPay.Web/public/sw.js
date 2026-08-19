const CACHE='creatorpay-shell-v19'
const LEGACY_ORIGIN='https://www.weymela.com'
const CANONICAL_ORIGIN='https://weymela.com'
const LEGACY_SELF= self.location.origin===LEGACY_ORIGIN
const SHELL=['/','/offline.html','/manifest.webmanifest','/favicon.ico','/icons/weymela-192x192.png','/icons/weymela-512x512.png']
let firebaseConfigured=false
self.addEventListener('message',event=>{if(event.data?.type==='FIREBASE_CONFIG'&&!firebaseConfigured){const c=event.data.config;if(!c?.apiKey||!c?.projectId||!c?.messagingSenderId)return;importScripts('https://www.gstatic.com/firebasejs/12.16.0/firebase-app-compat.js','https://www.gstatic.com/firebasejs/12.16.0/firebase-messaging-compat.js');firebase.initializeApp(c);firebase.messaging().onBackgroundMessage(payload=>{const data=payload.data||{};self.registration.showNotification(payload.notification?.title||'Weymela',{body:payload.notification?.body||'You have a new notification.',data:{targetPath:data.targetPath||'/'}})});firebaseConfigured=true}})
self.addEventListener('notificationclick',event=>{event.notification.close();event.waitUntil(self.clients.matchAll({type:'window',includeUncontrolled:true}).then(clients=>clients[0]?clients[0].focus().then(()=>clients[0].navigate(event.notification.data.targetPath)):self.clients.openWindow(event.notification.data.targetPath)))})

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
