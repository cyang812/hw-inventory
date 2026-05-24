import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

// Vite picks up our Vue 3 SFCs and proxies /api, /mcp, /openapi to the .NET API
// at port 5080 so the dev experience is identical to the production reverse-proxy
// setup described in docker-compose.yml.
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api':     { target: 'http://127.0.0.1:5080', changeOrigin: true },
      '/openapi': { target: 'http://127.0.0.1:5080', changeOrigin: true },
      '/scalar':  { target: 'http://127.0.0.1:5080', changeOrigin: true },
      '/mcp':     { target: 'http://127.0.0.1:5080', changeOrigin: true, ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
  },
});
