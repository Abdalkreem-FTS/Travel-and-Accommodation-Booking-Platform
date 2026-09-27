import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // In development, forward every /api request to the gateway, so the browser
    // only ever talks to localhost:5173 and there is no CORS to deal with.
    proxy: {
      '/api': 'http://localhost:8080',
    },
  },
})
