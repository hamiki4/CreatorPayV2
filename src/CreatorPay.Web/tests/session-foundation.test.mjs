import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import test from 'node:test'

const read=(path)=>readFileSync(new URL(`../${path}`,import.meta.url),'utf8')

test('native authentication storage fails closed into encrypted platform storage',()=>{
  const storage=read('src/sessionStore.ts')
  for(const expected of ['isNativePlatform()','SecureStorage.getItem','SecureStorage.setItem','KeychainAccess.whenUnlockedThisDeviceOnly','SecureStorage.setSynchronize(false)'])
    assert.ok(storage.includes(expected),expected)
  assert.ok(!storage.toLowerCase().includes('pin'))
  assert.ok(!storage.includes('catch'))
})

test('authentication hydrates before rendering and service workers remain Web-only',()=>{
  const main=read('src/main.tsx')
  assert.ok(main.indexOf('hydrateSession()')<main.indexOf('renderApplication();'))
  assert.ok(main.includes('if (isNativePlatform() || !("serviceWorker" in navigator)) return;'))
  assert.ok(main.includes('installSessionLifecycle'))
})

test('native lifecycle enforces inactivity and revalidates on resume',()=>{
  const lifecycle=read('src/mobileLifecycle.ts')
  for(const expected of ["App.addListener('appStateChange'",'INACTIVITY_TIMEOUT_MS','onResume()','expireAuthenticatedSession()'])
    assert.ok(lifecycle.includes(expected),expected)
})

test('Android back navigation is native-aware without duplicating browser history',()=>{
  const lifecycle=read('src/mobileLifecycle.ts')
  const auth=read('src/AuthWorkspace.tsx')
  for(const expected of ["App.addListener('backButton'",'history.back()','App.minimizeApp()','cancelable: true'])
    assert.ok(lifecycle.includes(expected),expected)
  assert.ok(auth.includes('NATIVE_BACK_EVENT'))
  assert.ok(auth.includes('changeMode("login")'))
})
