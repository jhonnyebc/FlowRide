import { MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import axe from 'axe-core';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { vi } from 'vitest';
import { ContextoUsuario, type UsuarioActual } from '../auth/usuario';
import { tema } from '../tema';

export const operador: UsuarioActual = { id: 'u-1', nombre: 'Laura Gómez', correo: 'laura@ops.co', rol: 'OperadorLogistico' };
export const administrador: UsuarioActual = { id: 'u-2', nombre: 'Ana Rojas', correo: 'ana@ops.co', rol: 'Administrador' };

type Manejador = (peticion: Request) => unknown | Response | Promise<unknown | Response>;

/**
 * fetch falso por ruta: la clave es `MÉTODO /ruta` (sin consulta). Devuelve JSON 200 salvo que el manejador entregue un
 * Response. Guarda las peticiones para revisar qué se envió.
 */
export function apiFalsa(rutas: Record<string, Manejador>) {
  const peticiones: Request[] = [];
  const falso = vi.fn(async (entrada: RequestInfo | URL, init?: RequestInit) => {
    const peticion = entrada instanceof Request ? entrada : new Request(String(entrada), init);
    peticiones.push(peticion.clone());
    const url = new URL(peticion.url);
    const clave = `${peticion.method} ${url.pathname}`;
    const manejador = rutas[clave];
    if (!manejador) return new Response(JSON.stringify({ title: `Sin ruta de prueba para ${clave}` }), { status: 404 });
    const resultado = await manejador(peticion);
    return resultado instanceof Response
      ? resultado
      : new Response(JSON.stringify(resultado ?? null), { status: 200, headers: { 'Content-Type': 'application/json' } });
  });
  vi.stubGlobal('fetch', falso);
  return { peticiones, falso };
}

/** Renderiza con los proveedores del panel, un usuario y una ruta inicial. */
export function renderizar(elemento: ReactElement, opciones: { usuario?: UsuarioActual; ruta?: string; patron?: string } = {}) {
  const consultas = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <MantineProvider theme={tema} env="test">
      <Notifications />
      <QueryClientProvider client={consultas}>
        <ContextoUsuario.Provider value={opciones.usuario ?? operador}>
          <MemoryRouter initialEntries={[opciones.ruta ?? '/']}>
            <Routes>
              <Route path={opciones.patron ?? '*'} element={elemento} />
            </Routes>
          </MemoryRouter>
        </ContextoUsuario.Provider>
      </QueryClientProvider>
    </MantineProvider>,
  );
}

/**
 * WCAG 2.1 AA con axe-core sobre lo que se pintó. El contraste de color no se puede medir en jsdom (no hay diseño): se
 * cubre con el tema de Mantine.
 */
export async function sinViolacionesDeAccesibilidad(contenedor: Element = document.body): Promise<void> {
  const resultado = await axe.run(contenedor, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] },
    rules: { 'color-contrast': { enabled: false } },
  });
  const violaciones = resultado.violations.map((v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(' ')).join(', ')})`);
  if (violaciones.length > 0) throw new Error(`Violaciones de accesibilidad:\n${violaciones.join('\n')}`);
}
