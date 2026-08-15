import assert from 'node:assert/strict'
import {readFileSync} from 'node:fs'
import test from 'node:test'

const read=(path)=>readFileSync(new URL(`../${path}`,import.meta.url),'utf8')

test('Capacitor configuration uses the approved identity, build output, and Android origin',()=>{
  const config=read('capacitor.config.ts')
  for(const expected of ["appId: 'com.weymela.app'","appName: 'Weymela'","webDir: 'dist'","hostname: 'localhost'","androidScheme: 'https'"])
    assert.ok(config.includes(expected),expected)
})

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

test('Android manifest permits only HTTPS networking and protects session data',()=>{
  const manifest=read('android/app/src/main/AndroidManifest.xml')
  for(const expected of ['android:usesCleartextTraffic="false"','android:allowBackup="false"','android:windowSoftInputMode="adjustResize"','android.permission.INTERNET'])
    assert.ok(manifest.includes(expected),expected)
  assert.ok(!manifest.includes('android.permission.CAMERA'))
})

test('mobile modes select exact non-secret PILOT and Production API URLs',()=>{
  const vite=read('vite.config.ts')
  const pkg=JSON.parse(read('package.json'))
  assert.equal(pkg.scripts['mobile:pilot'],'vite build --mode mobile-pilot')
  assert.equal(pkg.scripts['mobile:production'],'vite build --mode mobile-production')
  assert.ok(vite.includes("'mobile-pilot':'https://api-pilot.weymela.com'"))
  assert.ok(vite.includes("'mobile-production':'https://api.weymela.com'"))
})

test('PILOT alone allows the exact Android WebView origin',()=>{
  const pilot=readFileSync(new URL('../../../docker-compose.pilot.yml',import.meta.url),'utf8')
  assert.ok(pilot.includes('Cors__AllowedOrigins__1: https://localhost'))
  assert.ok(!pilot.includes('Cors__AllowedOrigins__1: *'))
})
