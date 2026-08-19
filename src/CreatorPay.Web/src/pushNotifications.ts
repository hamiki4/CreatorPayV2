import { getApps, initializeApp } from 'firebase/app'
import { getMessaging, getToken, onMessage } from 'firebase/messaging'
import { PushNotifications } from '@capacitor/push-notifications'
import { api } from './apiClient'
import { isNativePlatform } from './runtimePlatform'
import { getInstallationId } from './sessionStore'

const config={apiKey:import.meta.env.VITE_FIREBASE_API_KEY,authDomain:import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,projectId:import.meta.env.VITE_FIREBASE_PROJECT_ID,appId:import.meta.env.VITE_FIREBASE_APP_ID,messagingSenderId:import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID}
const vapid=import.meta.env.VITE_FIREBASE_VAPID_PUBLIC_KEY
let registeredToken=''
function configured(){return Boolean(config.apiKey&&config.authDomain&&config.projectId&&config.appId&&config.messagingSenderId&&vapid)}
async function tokenHash(token:string){const bytes=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(token));return [...new Uint8Array(bytes)].map(x=>x.toString(16).padStart(2,'0')).join('')}
async function register(platform:'web'|'android',token:string){await api('/api/v1/push-devices',{method:'POST',body:JSON.stringify({platform,token,installationId:await getInstallationId()})});registeredToken=token}
async function optIn(){const preferences=await api<Array<{notificationType:string,inAppEnabled:boolean,emailEnabled:boolean,smsEnabled:boolean,pushEnabled:boolean,languageCode:string}>>('/api/v1/notification-preferences');await api('/api/v1/notification-preferences',{method:'PUT',body:JSON.stringify(preferences.map(x=>({...x,pushEnabled:true})))})}

// Call only from an authenticated, user-initiated notification preference action.
export async function enablePushNotifications(){
  if(!configured())throw new Error('Push notifications are not configured.')
  if(isNativePlatform()){
    const permission=await PushNotifications.requestPermissions();if(permission.receive!=='granted')return false
    await PushNotifications.removeAllListeners()
    await PushNotifications.addListener('registration',token=>void register('android',token.value).catch(()=>{}))
    await PushNotifications.addListener('registrationError',()=>window.dispatchEvent(new Event('weymela-push-error')))
    await PushNotifications.addListener('pushNotificationReceived',notification=>window.dispatchEvent(new CustomEvent('weymela-push',{detail:notification.data})))
    await PushNotifications.addListener('pushNotificationActionPerformed',action=>{const target=String(action.notification.data?.targetPath??'/');if(target.startsWith('/'))location.assign(target)})
    await PushNotifications.register();await optIn();return true
  }
  if(!('Notification'in window)||!('serviceWorker'in navigator))throw new Error('This browser does not support notifications.')
  if(await Notification.requestPermission()!=='granted')return false
  const sw=await navigator.serviceWorker.ready
  sw.active?.postMessage({type:'FIREBASE_CONFIG',config})
  const app=getApps()[0]??initializeApp(config)
  const value=await getToken(getMessaging(app),{vapidKey:vapid,serviceWorkerRegistration:sw})
  if(!value)throw new Error('A push token could not be created.')
  await register('web',value)
  await optIn()
  onMessage(getMessaging(app),payload=>window.dispatchEvent(new CustomEvent('weymela-push',{detail:payload.data})))
  return true
}
export async function refreshPushRegistration(){if(!configured()||isNativePlatform()||Notification.permission!=='granted')return;try{await enablePushNotifications()}catch{ /* push must never interrupt the application */ }}
export async function revokePushNotifications(){if(!registeredToken)return;try{await api(`/api/v1/push-devices/${await tokenHash(registeredToken)}`,{method:'DELETE'})}finally{registeredToken=''}}
