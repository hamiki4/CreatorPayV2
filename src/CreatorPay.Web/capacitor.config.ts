import type { CapacitorConfig } from '@capacitor/cli'

const config: CapacitorConfig = {
  appId: 'com.weymela.app',
  appName: 'Weymela',
  webDir: 'dist',
  server: { hostname: 'localhost', androidScheme: 'https' },
}

export default config
