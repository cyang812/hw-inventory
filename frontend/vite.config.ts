import { defineConfig, loadEnv } from 'vite';
import vue from '@vitejs/plugin-vue';

// Vite picks up our Vue 3 SFCs and proxies /api, /mcp, /openapi to the .NET API.
// Default target is the local backend on :5080 (mirrors docker-compose.yml). Override
// with BACKEND_URL in .env.local (gitignored) when developing against a remote host,
// e.g. BACKEND_URL=http://192.168.31.152:1234.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const target = env.BACKEND_URL || 'http://127.0.0.1:5080';
  // eslint-disable-next-line no-console
  console.log(`[vite] proxying /api,/mcp,/openapi,/scalar -> ${target}`);
  return {
    plugins: [vue()],
    server: {
      port: 5173,
      proxy: {
        '/api':     { target, changeOrigin: true },
        '/openapi': { target, changeOrigin: true },
        '/scalar':  { target, changeOrigin: true },
        '/mcp':     { target, changeOrigin: true, ws: true },
      },
    },
    test: {
      environment: 'jsdom',
      globals: true,
    },
  };
});
