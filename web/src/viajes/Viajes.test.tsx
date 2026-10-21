import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { viaje } from '../pruebas/datos';
import { administrador, apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { DetalleViaje } from './DetalleViaje';
import { PaginaViajes } from './PaginaViajes';

describe('Viajes', () => {
  it('el monitoreo marca los retrasados con texto e icono (RF-37)', async () => {
    apiFalsa({
      'GET /api/viajes/monitoreo': () => [
        { viaje: viaje('v-1', 'Programado'), retrasado: true, minutosRetraso: 12 },
        { viaje: viaje('v-2', 'EnCurso', { incidentes: 1 }), retrasado: false, minutosRetraso: 0 },
      ],
    });

    renderizar(<PaginaViajes />);

    expect(await screen.findByText('Retrasado 12 min')).toBeInTheDocument();
    expect(screen.getAllByRole('link')[0]).toHaveAttribute('href', '/viajes/v-1');
    await sinViolacionesDeAccesibilidad();
  });

  it('el Operador logístico inicia un viaje programado y reporta incidentes', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/viajes/v-1': () => ({ viaje: viaje('v-1', 'Programado'), paradas: [], incidentes: [], calificacion: null }),
      'POST /api/viajes/v-1/iniciar': () => ({ id: 'v-1', asignacionId: 'a', estado: 'EnCurso', fechaHoraInicio: null, fechaHoraFin: null }),
      'POST /api/viajes/v-1/incidentes': () => new Response('{}', { status: 201 }),
    });
    const usuario = userEvent.setup();
    renderizar(<DetalleViaje />, { ruta: '/viajes/v-1', patron: '/viajes/:id' });

    await usuario.click(await screen.findByRole('button', { name: 'Iniciar' }));
    expect(await screen.findByText('Viaje iniciado.')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Reportar incidente' }));
    await usuario.type(await screen.findByLabelText(/Descripción/), 'Vía cerrada');
    await sinViolacionesDeAccesibilidad();
    await usuario.click(screen.getByRole('button', { name: 'Reportar' }));
    expect(await screen.findByText('Incidente reportado.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.url.endsWith('/incidentes'))!.json()).toEqual({ tipo: 'Retraso', descripcion: 'Vía cerrada' });
  });

  it('el Administrador consulta pero no actúa sobre el viaje', async () => {
    apiFalsa({ 'GET /api/viajes/v-1': () => ({ viaje: viaje('v-1', 'EnCurso'), paradas: [], incidentes: [], calificacion: null }) });

    renderizar(<DetalleViaje />, { ruta: '/viajes/v-1', patron: '/viajes/:id', usuario: administrador });

    expect(await screen.findByText('Viaje de Ana Gómez')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Finalizar' })).not.toBeInTheDocument();
  });

  it('el detalle dice si inició a tiempo, las paradas alcanzadas y la calificación con texto', async () => {
    apiFalsa({
      'GET /api/viajes/v-1': () => ({
        viaje: viaje('v-1', 'Finalizado', { fechaHoraInicio: '2026-10-21T13:12:00Z', fechaHoraFin: '2026-10-21T14:00:00Z' }),
        paradas: [
          { id: 'p-1', orden: 1, direccion: 'Portería', horaEstimada: '2026-10-21T13:20:00Z', horaReal: '2026-10-21T13:25:00Z' },
          { id: 'p-2', orden: 2, direccion: 'Planta', horaEstimada: '2026-10-21T13:40:00Z', horaReal: null },
        ],
        incidentes: [{ id: 'i-1', tipo: 'FallaMecanica', descripcion: 'Pinchazo', fechaHora: '2026-10-21T13:30:00Z', reportadoPorId: 'u' }],
        calificacion: { id: 'c-1', puntaje: 4, comentario: 'Muy amable', fechaHora: '2026-10-21T15:00:00Z' },
      }),
    });

    renderizar(<DetalleViaje />, { ruta: '/viajes/v-1', patron: '/viajes/:id' });

    expect(await screen.findByText('Inició 12 min tarde')).toBeInTheDocument();
    expect(screen.getByText(/Llegó: .*\(5 min tarde\)/)).toBeInTheDocument();
    expect(screen.getByText('Aún no llega')).toBeInTheDocument();
    expect(screen.getByText('Falla mecánica')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: '4 de 5 estrellas' })).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Avance del viaje' })).toHaveTextContent('Finalizado (completada)');
    await sinViolacionesDeAccesibilidad();
  });
});
