/// <reference types="vitest/config" />
import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API is same-origin from the browser's point of view: the dev server proxies
// /api to the ASP.NET host. That is deliberate — the session cookie is SameSite=Lax
// and HttpOnly, and a cross-origin client would mean CORS plus credentialed fetches,
// neither of which the auth configuration (WP-0.5) is set up for and neither of which
// this package may change.
//
// Aspire injects the host's address as a service-discovery variable when the client
// runs under `aspire run`; the launchSettings address is the fallback for a bare
// `npm run dev`.
const apiTarget =
  process.env['services__web-host__https__0'] ??
  process.env['services__web-host__http__0'] ??
  'https://localhost:7014'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: Number(process.env['PORT'] ?? 5173),
    strictPort: true,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: false,
        // The ASP.NET development certificate is not fully trusted under WSL
        // (STATUS.md), and this proxy hop never leaves the machine.
        secure: false,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    include: ['src/**/*.test.{ts,tsx}'],
  },
})
