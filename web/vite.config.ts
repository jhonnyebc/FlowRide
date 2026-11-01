/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// En local el panel corre con el servidor de Vite (ADR-0019) y le pasa /api a la API del operador, así que el navegador
// ve un solo origen: sin CORS ni cookies entre sitios. En producción lo sirve el borde común (ADR-0012).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: 'http://localhost:5237', changeOrigin: false },
    },
  },
  build: {
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          react: ['react', 'react-dom', 'react-router'],
          mantine: ['@mantine/core', '@mantine/hooks', '@mantine/notifications'],
          graficas: ['@mantine/charts', 'recharts'],
          sesion: ['oidc-client-ts', 'react-oidc-context', '@tanstack/react-query', 'openapi-fetch'],
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/pruebas/preparacion.ts'],
    css: false,
    restoreMocks: true,
    // Solo con npm run test:cobertura (job de SonarCloud): lcov es el formato que lee SonarCloud.
    coverage: {
      provider: 'v8',
      reporter: ['lcovonly', 'text-summary'],
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/**/*.test.{ts,tsx}', 'src/pruebas/**', 'src/api/esquema.d.ts'],
    },
  },
});
