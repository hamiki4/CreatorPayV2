import { App } from '@capacitor/app'
import { isNativePlatform } from './runtimePlatform'
import { expireAuthenticatedSession, INACTIVITY_TIMEOUT_MS, installInactivityLogout } from './authSession'

export const NATIVE_BACK_EVENT = 'weymela:native-back'

export function installNativeBackNavigation() {
  if (!isNativePlatform()) return undefined
  const listener = App.addListener('backButton', ({ canGoBack }) => {
    const event = new CustomEvent(NATIVE_BACK_EVENT, { cancelable: true })
    if (!dispatchEvent(event)) return
    if (canGoBack) history.back()
    else void App.minimizeApp()
  })
  return () => { void listener.then(handle => handle.remove()) }
}

export function installSessionLifecycle(onResume: () => Promise<boolean>) {
  if (!isNativePlatform()) return installInactivityLogout()
  let lastActivity = Date.now()
  let timer = 0
  let appActive = true
  const scheduleExpiry = () => {
    window.clearTimeout(timer)
    if (!appActive) return
    const remaining = Math.max(0, INACTIVITY_TIMEOUT_MS - (Date.now() - lastActivity))
    timer = window.setTimeout(expireAuthenticatedSession, remaining)
  }
  const activity = () => { lastActivity = Date.now(); scheduleExpiry() }
  const events = ['pointerdown', 'touchstart', 'keydown', 'scroll', 'click'] as const
  events.forEach(name => window.addEventListener(name, activity, { passive: true }))
  const listener = App.addListener('appStateChange', async ({ isActive }) => {
    appActive = isActive
    if (!isActive) { window.clearTimeout(timer); return }
    if (Date.now() - lastActivity >= INACTIVITY_TIMEOUT_MS) {
      expireAuthenticatedSession()
      return
    }
    try {
      if (!(await onResume())) { expireAuthenticatedSession(); return }
    } catch {
      expireAuthenticatedSession()
      return
    }
    dispatchEvent(new CustomEvent('weymela:app-resume'))
    scheduleExpiry()
  })
  scheduleExpiry()
  return () => {
    window.clearTimeout(timer)
    events.forEach(name => window.removeEventListener(name, activity))
    void listener.then(handle => handle.remove())
  }
}
