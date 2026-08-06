export const workspaceRoutes:Record<string,string>={Customer:'/shopper',Creator:'/creator',MerchantAdmin:'/business',Supervisor:'/supervisor',Cashier:'/cashier',PlatformAdmin:'/admin'}

export function workspaceRoute(role:string){return workspaceRoutes[role]??'/'}
export function isWorkspacePathAllowed(role:string,path:string){const route=workspaceRoute(role);return route!=='/'&&(path===route||path.startsWith(`${route}/`))}
export function clearAuthState(){localStorage.removeItem('creatorpay_access_token');localStorage.removeItem('creatorpay_refresh_token')}
