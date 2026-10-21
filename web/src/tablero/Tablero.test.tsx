import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { alerta, indicadores, pagina, servicioVista, viaje } from '../pruebas/datos';
import { apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { Tablero } from './Tablero';

const rutas = {
  'GET /api/reportes/indicadores': () => indicadores(),
  'GET /api/reportes/vista-centralizada': () => ({
    desde: '',
    hasta: '',
    criticos: 1,
    atencion: 0,
    normales: 1,
    servicios: [
      { servicio: servicioVista('s-1', 'SinCandidatos'), nivel: 'Critico', motivo: 'Sin conductor disponible.' },
      { servicio: servicioVista('s-2', 'Asignada'), nivel: 'Normal', motivo: null },
    ],
  }),
  'GET /api/viajes/monitoreo': () => [{ viaje: viaje('v-1', 'EnCurso'), retrasado: true, minutosRetraso: 7 }],
  'GET /api/alertas': () => pagina([alerta('al-1', 'Critica')]),
};

describe('Tablero (RF-36, RF-38)', () => {
  it('muestra los indicadores, lo que requiere atención, los viajes activos y las alertas, todo enlazado', async () => {
    apiFalsa(rutas);

    renderizar(<Tablero />);

    expect(await screen.findByText('Sin conductor disponible.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(/, Laura$/);
    expect(await screen.findByText('81,8 %')).toBeInTheDocument(); // 9 atendidas de 11 (sin canceladas)
    expect(screen.getByText('80 %')).toBeInTheDocument();
    expect(screen.getByText('4,6 / 5')).toBeInTheDocument();

    const atencion = screen.getByRole('region', { name: 'Requieren atención hoy' });
    expect(within(atencion).getByRole('link', { name: 'Pasajero s-1' })).toHaveAttribute('href', '/solicitudes/s-1');
    expect(within(atencion).queryByText('Pasajero s-2')).not.toBeInTheDocument();

    const viajes = screen.getByRole('region', { name: 'Viajes en curso y próximos' });
    expect(within(viajes).getByText('7 min tarde')).toBeInTheDocument();
    expect(screen.getByText('Alerta al-1')).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Solicitudes por estado hoy' })).toHaveTextContent('Completada9');
    await sinViolacionesDeAccesibilidad();
  });

  it('cambiar el periodo pide los indicadores de los últimos 7 días', async () => {
    const { peticiones } = apiFalsa(rutas);
    const usuario = userEvent.setup();
    renderizar(<Tablero />);
    await screen.findByText('81,8 %');

    await usuario.click(screen.getByText('7 días'));

    await screen.findByText(/así va la operación en los últimos 7 días/);
    const pedidas = peticiones.filter((p) => p.url.includes('/indicadores')).map((p) => new URL(p.url).searchParams);
    const ultima = pedidas.at(-1)!;
    const dias = (Date.parse(ultima.get('Hasta')!) - Date.parse(ultima.get('Desde')!)) / 86_400_000;
    expect(Math.round(dias)).toBe(7);
  });

  it('un día tranquilo lo dice en vez de mostrar listas vacías', async () => {
    apiFalsa({
      ...rutas,
      'GET /api/reportes/vista-centralizada': () => ({ desde: '', hasta: '', criticos: 0, atencion: 0, normales: 0, servicios: [] }),
      'GET /api/viajes/monitoreo': () => [],
      'GET /api/alertas': () => pagina([]),
    });

    renderizar(<Tablero />);

    expect(await screen.findByText('Todo en orden')).toBeInTheDocument();
    expect(screen.getByText('Sin servicios hoy')).toBeInTheDocument();
    expect(screen.getByText('Sin viajes activos')).toBeInTheDocument();
    expect(screen.getByText('Sin alertas')).toBeInTheDocument();
  });
});
