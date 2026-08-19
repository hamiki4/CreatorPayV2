import { KeychainAccess, SecureStorage } from '@aparajita/capacitor-secure-storage'
import { isNativePlatform } from './runtimePlatform'

const keys = {
  accessToken: 'creatorpay_access_token',
  refreshToken: 'creatorpay_refresh_token',
  trustedPhone: 'weymela_trusted_phone',
  installationId: 'weymela_push_installation_id',
} as const

type SessionState = { accessToken: string; refreshToken: string; trustedPhone: string; installationId: string }
let state: SessionState = { accessToken: '', refreshToken: '', trustedPhone: '', installationId: '' }
let hydrated = false
let nativeStorageReady: Promise<void> | undefined

function prepareNativeStorage() {
  if (!isNativePlatform()) throw new Error('Native secure storage is unavailable.')
  nativeStorageReady ??= Promise.all([
    SecureStorage.setKeyPrefix('weymela_'),
    SecureStorage.setSynchronize(false),
    SecureStorage.setDefaultKeychainAccess(KeychainAccess.whenUnlockedThisDeviceOnly),
  ]).then(() => undefined)
  return nativeStorageReady
}

async function read(key: string) {
  if (!isNativePlatform()) return localStorage.getItem(key) ?? ''
  await prepareNativeStorage()
  return (await SecureStorage.getItem(key)) ?? ''
}

async function write(key: string, value: string) {
  if (!isNativePlatform()) {
    if (value) localStorage.setItem(key, value)
    else localStorage.removeItem(key)
    return
  }
  await prepareNativeStorage()
  if (value) await SecureStorage.setItem(key, value)
  else await SecureStorage.removeItem(key)
}

export async function hydrateSession() {
  const [accessToken, refreshToken, trustedPhone, installationId] = await Promise.all([
    read(keys.accessToken), read(keys.refreshToken), read(keys.trustedPhone), read(keys.installationId),
  ])
  state = { accessToken, refreshToken, trustedPhone, installationId }
  hydrated = true
}

function assertHydrated() {
  if (!hydrated) throw new Error('Authentication storage has not been hydrated.')
}

export function isSessionHydrated() { return hydrated }
export function getAccessToken() { assertHydrated(); return state.accessToken }
export function getRefreshToken() { assertHydrated(); return state.refreshToken }
export function getTrustedPhone() { assertHydrated(); return state.trustedPhone }
export async function getInstallationId() { assertHydrated(); if (state.installationId) return state.installationId; const value = crypto.randomUUID(); await write(keys.installationId, value); state = { ...state, installationId: value }; return value }

export async function setSessionTokens(accessToken: string, refreshToken: string) {
  assertHydrated()
  await Promise.all([write(keys.accessToken, accessToken), write(keys.refreshToken, refreshToken)])
  state = { ...state, accessToken, refreshToken }
}

export async function setTrustedPhone(trustedPhone: string) {
  assertHydrated()
  await write(keys.trustedPhone, trustedPhone)
  state = { ...state, trustedPhone }
}

export async function clearSession() {
  assertHydrated()
  state = { ...state, accessToken: '', refreshToken: '' }
  await Promise.all([write(keys.accessToken, ''), write(keys.refreshToken, '')])
}
