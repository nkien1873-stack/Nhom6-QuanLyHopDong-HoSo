import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Gọi /api/... được chuyển sang backend ASP.NET Core (khỏi lo CORS, lệch cổng)
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY || 'http://localhost:5215',
        changeOrigin: true
      }
    }
  }
})
