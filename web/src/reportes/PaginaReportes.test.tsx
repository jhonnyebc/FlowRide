import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { empresa, pagina, servicioVista, viaje } from '../pruebas/datos';
import { apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { PaginaReportes, periodoApi } from './PaginaReportes';

const rutas = {
  'GET /api/empresas-cliente': () => pagina([empresa('e-1', { nombre: 'Minera del Sur' })]),
  'GET /api/reportes/viajes': () => ({
    desde: '2026-10-21T05:00:00Z',
    hasta: '2026-10-28T05:00:00Z',
    resumen: { viajes: 3, puntuales: 2, porcentajePuntualidad: 66.7, retrasoPromedioMinutos: 4, incidentes: 1, calificados: 2, calificacionPromedio: 4.5, toleranciaMinutos: 10 },
    filas: pagina([{ viaje: viaje('v-1', 'Finalizado', { fechaHoraInicio: '2026-10-22T13:12:00Z' }), puntual: false, minutosRetraso: 12 }]),
  }),
  'GET /api/reportes/solicitudes': () => ({
    desde: '2026-10-21T05:00:00Z',
    hasta: '2026-10-28T05:00:00Z',
    resumen: { total: 1, atendidas: 0, pendientes: 1, canceladas: 0, porcentajeAtencion: 0, porEstado: { Pendiente: 1 } },
    filas: pagina([{ solicitud: servicioVista('s-1', 'Pendiente', { urgente: true }), grupo: 'Pendiente' as const }]),
  }),
};

describe('periodoApi', () => {
  it('convierte días locales completos en instantes, con el día final incluido', () => {
    const { desde, hasta } = periodoApi('2026-10-21', '2026-10-27');
    expect(new Date(desde)).toEqual(new Date(2026, 9, 21));
    expect(new Date(hasta)).toEqual(new Date(2026, 9, 28));
  });
});

describe('Reportes (RF-46 a RF-49)', () => {
  it('muestra el reporte de viajes de los últimos 7 días con su resumen y la puntualidad', async () => {
    const { peticiones } = apiFalsa(rutas);
    renderizar(<PaginaReportes />);

    expect(await screen.findByText('Ana Gómez')).toBeInTheDocument();
    expect(screen.getByText('66,7 %')).toBeInTheDocument();
    expect(screen.getByText('12 min tarde')).toBeInTheDocument();
    const consulta = new URL(peticiones.find((p) => p.url.includes('/api/reportes/viajes'))!.url).searchParams;
    const dias = (Date.parse(consulta.get('Hasta')!) - Date.parse(consulta.get('Desde')!)) / 86_400_000;
    expect(Math.round(dias)).toBe(7);
    await sinViolacionesDeAccesibilidad();
  });

  it('filtra las solicitudes pendientes y exporta ese mismo reporte a CSV', async () => {
    const abrirVentana = vi.spyOn(window, 'open').mockReturnValue(null);
    const { peticiones } = apiFalsa({
      ...rutas,
      'POST /api/reportes/Solicitudes/exportar': () => ({
        id: 'x-1',
        tipo: 'Solicitudes',
        nombreArchivo: 'reporte-solicitudes.csv',
        filas: 1,
        url: 'http://objetos.local/reporte.csv?firma=x',
        expiraEn: '2026-10-27T12:15:00Z',
      }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaReportes />);

    await usuario.click(await screen.findByRole('tab', { name: 'Solicitudes' }));
    await usuario.click(within(screen.getByRole('radiogroup', { name: 'Grupo de solicitudes' })).getByLabelText('Pendientes'));
    expect(await screen.findByText('Urgente')).toBeInTheDocument();
    const ultima = peticiones.filter((p) => p.url.includes('/api/reportes/solicitudes')).at(-1)!;
    expect(new URL(ultima.url).searchParams.get('Grupo')).toBe('Pendiente');

    await usuario.click(screen.getByRole('button', { name: 'Exportar solicitudes a CSV' }));
    expect(await screen.findByText('Reporte exportado: 1 fila(s) en reporte-solicitudes.csv.')).toBeInTheDocument();
    expect(abrirVentana).toHaveBeenCalledWith('http://objetos.local/reporte.csv?firma=x', '_blank', 'noopener,noreferrer');
    const cuerpo = (await peticiones.find((p) => p.method === 'POST')!.json()) as Record<string, unknown>;
    expect(cuerpo).toMatchObject({ grupo: 'Pendiente', empresaClienteId: null });
    expect(typeof cuerpo.desde).toBe('string');
  });
});
