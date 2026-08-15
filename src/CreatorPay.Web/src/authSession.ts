import {clearSession,getAccessToken} from './sessionStore'

export const workspaceRoutes:Record<string,string>={Customer:'/shopper',Creator:'/creator',MerchantAdmin:'/business',Supervisor:'/supervisor',Cashier:'/cashier',PlatformAdmin:'/admin'}

export function workspaceRoute(role:string){return workspaceRoutes[role]??'/'}
export function isWorkspacePathAllowed(role:string,path:string){const route=workspaceRoute(role);return route!=='/'&&(path===route||path.startsWith(`${route}/`))}
export function clearAuthState(){return clearSession()}

export const INACTIVITY_TIMEOUT_MS=120_000
export function expireAuthenticatedSession(){void clearAuthState();if(location.pathname!=='/')location.assign('/')}
export function handleUnauthorized(status:number){if(status===401&&getAccessToken()){expireAuthenticatedSession();return true}return false}
export function installInactivityLogout(timeoutMs=INACTIVITY_TIMEOUT_MS){let timer=0;const reset=()=>{window.clearTimeout(timer);timer=window.setTimeout(expireAuthenticatedSession,timeoutMs)};const events=['pointerdown','pointermove','touchstart','keydown','scroll','click','popstate'] as const;events.forEach(name=>window.addEventListener(name,reset,{passive:true}));reset();return()=>{window.clearTimeout(timer);events.forEach(name=>window.removeEventListener(name,reset))}}
