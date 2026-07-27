import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Listen on all interfaces so phones/laptops on the same WiFi can reach the dev server.
    host: true,

    // Proxy API calls through Vite instead of the browser calling :5000 directly.
    // Two reasons this beats pointing the frontend at the API's LAN address:
    //   1. Same-origin — no CORS involved at all, from any device.
    //   2. The .NET API stays bound to localhost, so it is NOT exposed to the network.
    //      Only this dev server is, and it just relays.
    proxy: {
      '/api': 'http://localhost:5000',
      '/health': 'http://localhost:5000',
    },
  },
})
