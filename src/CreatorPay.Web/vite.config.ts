import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({mode}) => {
  const environment=loadEnv(mode,'.','')
  const apiUrl=environment.VITE_API_URL||(mode==='development'?'http://localhost:5225':'')
  return {plugins:[react()],define:{'import.meta.env.VITE_API_URL':JSON.stringify(apiUrl)}}
})
