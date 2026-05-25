import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// Tailwind v4 runs as a Vite plugin — no postcss.config.js needed.
// '@' alias maps to src/ so components import as '@/components/Button'.
// Dev proxy forwards /api to the Docker API container — no CORS issues in dev.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5001',
    },
  },
})
