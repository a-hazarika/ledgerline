import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// VITE_API_TARGET lets the container point at the api service instead of localhost.
const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:8080'

export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
      '/branding': { target: apiTarget, changeOrigin: true },
    },
  },
  preview: {
    host: true,
    port: 5173,
  },
})
