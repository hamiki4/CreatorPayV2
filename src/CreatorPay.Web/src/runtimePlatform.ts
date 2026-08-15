import { Capacitor } from '@capacitor/core'

export function isNativePlatform() {
  return Capacitor.isNativePlatform()
}

export function nativeWebViewOrigin() {
  return isNativePlatform() ? location.origin : null
}
