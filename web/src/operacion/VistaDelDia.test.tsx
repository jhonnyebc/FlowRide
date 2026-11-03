import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { servicioVista } from '../pruebas/datos';
import { apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { VistaDelDia } from './VistaDelDia';

describe('Vista del día (RF-36)', () => {
  it('muestra el semáforo con color, icono y texto, primero lo crítico, y enlaza cada servicio', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/reportes/vista-centralizada': () => ({
        desde: '2026-10-21T05:00:00Z',
        hasta: '2026-10-22T05:00:00Z',
        criticos: 1,
        atencion: 1,
        normales: 0,
        servicios: [
          { servicio: servicioVista('s-1', 'SinCandidatos'), nivel: 'Critico', motivo: 'Sin conductor disponible.' },
          { servicio: servicioVista('s-2', 'Pendiente'), nivel: 'Atencion', motivo: 'Espera asignación.' },
        ],
      }),
    });

    renderizar(<VistaDelDia />);

    expect(await screen.findByText('Sin conductor disponible.')).toBeInTheDocument();
    const filas = screen.getAllByRole('row').slice(1);
    expect(within(filas[0]!).getByText('Crítico')).toBeInTheDocument();
    expect(within(filas[1]!).getByText('Atención')).toBeInTheDocument();
    expect(within(filas[0]!).getByRole('link')).toHaveAttribute('href', '/solicitudes/s-1');
    expect(screen.getByText('Críticos').previousSibling).toHaveTextContent('1');
    expect(new URL(peticiones[0]!.url).searchParams.get('Fecha')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    await sinViolacionesDeAccesibilidad();
  });

  it('sin servicios lo dice', async () => {
    apiFalsa({ 'GET /api/reportes/vista-centralizada': () => ({ desde: '', hasta: '', criticos: 0, atencion: 0, normales: 0, servicios: [] }) });

    renderizar(<VistaDelDia />);

    expect(await screen.findByText('No hay servicios programados para este día.')).toBeInTheDocument();
  });

  it('si la API falla ofrece reintentar', async () => {
    apiFalsa({ 'GET /api/reportes/vista-centralizada': () => new Response(JSON.stringify({ title: 'Caída' }), { status: 500 }) });

    renderizar(<VistaDelDia />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Caída');
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });
});
