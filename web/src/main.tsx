import '@fontsource-variable/inter';
import '@mantine/core/styles.css';
import '@mantine/notifications/styles.css';
import '@mantine/charts/styles.css';
import './estilos.css';
import { MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AuthProvider } from 'react-oidc-context';
import { BrowserRouter } from 'react-router';
import { App } from './App';
import { ErrorApi } from './api/cliente';
import { ajustesOidc } from './auth/sesion';
import { tema } from './tema';

const consultas = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000,
      // Un 4xx (permiso, validación) no mejora reintentando.
      retry: (intentos, error) => !(error instanceof ErrorApi && error.estado < 500) && intentos < 2,
    },
  },
});

createRoot(document.getElementById('raiz')!).render(
  <StrictMode>
    <MantineProvider theme={tema} defaultColorScheme="auto">
      <Notifications position="top-right" />
      <AuthProvider {...ajustesOidc()}>
        <QueryClientProvider client={consultas}>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </QueryClientProvider>
      </AuthProvider>
    </MantineProvider>
  </StrictMode>,
);
