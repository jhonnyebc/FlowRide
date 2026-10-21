import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { servicioVista, viaje } from '../pruebas/datos';
import { apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { Marco } from './Marco';

const cerrarSesion = vi.fn();
vi.mock('react-oidc-context', () => ({ useAuth: () => ({ signoutRedirect: cerrarSesion }) }));

describe('Marco del panel', () => {
  beforeEach(() =>
    apiFalsa({
      'GET /api/reportes/vista-centralizada': () => ({
        desde: '',
        hasta: '',
        criticos: 1,
        atencion: 1,
        normales: 1,
        servicios: [
          { servicio: servicioVista('s-1', 'SinCandidatos'), nivel: 'Critico', motivo: null },
          { servicio: servicioVista('s-2', 'Pendiente'), nivel: 'Atencion', motivo: null },
          { servicio: servicioVista('s-3', 'Asignada'), nivel: 'Normal', motivo: null },
        ],
      }),
      'GET /api/viajes/monitoreo': () => [{ viaje: viaje('v-1', 'EnCurso'), retrasado: false, minutosRetraso: 0 }],
    }),
  );

  it('tiene enlace para saltar al contenido, menú navegable con lo pendiente de cada sección y la página actual marcada', async () => {
    renderizar(<Marco />, { ruta: '/solicitudes' });

    expect(screen.getByRole('link', { name: 'Saltar al contenido' })).toHaveAttribute('href', '#contenido');
    expect(screen.getByRole('link', { name: 'Solicitudes' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Vista del día' })).not.toHaveAttribute('aria-current');
    expect(screen.getByRole('link', { name: 'Alertas: 0 sin leer' })).toBeInTheDocument();
    expect(await screen.findByText('2')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Solicitudes' })).toHaveAccessibleDescription('2 por atender hoy');
    expect(screen.getByRole('link', { name: 'Vista del día' })).toHaveAccessibleDescription('1 críticos hoy');
    expect(screen.getByRole('link', { name: 'Viajes en curso' })).toHaveAccessibleDescription('1 en curso');
    await sinViolacionesDeAccesibilidad();
  });

  it('cambia el tema y cierra sesión desde el menú del usuario', async () => {
    const usuario = userEvent.setup();
    renderizar(<Marco />);

    await usuario.click(screen.getByRole('button', { name: 'Usar tema oscuro' }));
    expect(screen.getByRole('button', { name: 'Usar tema claro' })).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: /Laura Gómez, Operador logístico/ }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Cerrar sesión' }));
    expect(cerrarSesion).toHaveBeenCalledOnce();
  });
});
