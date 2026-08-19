import { defineConfig, loadEnv } from 'vite'
import type {Plugin} from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({mode}) => {
  const environment=loadEnv(mode,'.','')
  const mobileApiUrls:Record<string,string>={
    'mobile-pilot':'https://api-pilot.weymela.com',
    'mobile-production':'https://api.weymela.com',
  }
  const apiUrl=environment.VITE_API_URL??mobileApiUrls[mode]??(mode==='development'?'http://localhost:5225':'')
  const health:Plugin={name:'creatorpay-health',configureServer(server){server.middlewares.use('/healthz',(_request,response)=>{response.statusCode=200;response.setHeader('Content-Type','text/plain');response.end('healthy')})}}
  return {plugins:[react(),health],define:{'import.meta.env.VITE_API_URL':JSON.stringify(apiUrl)}}
})
