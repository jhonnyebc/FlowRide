import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { asignacion, pagina, solicitud } from '../pruebas/datos';
import { administrador, apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { DetalleSolicitud } from './DetalleSolicitud';

const rutas = (estado: string, asignaciones: ReturnType<typeof asignacion>[] = []) => ({
  'GET /api/asignaciones': () => pagina(asignaciones),
  'GET /api/solicitudes/s-1': () => ({ solicitud: solicitud('s-1', estado), historial: [] }),
  'GET /api/solicitudes/s-1/trazabilidad': () => ({
    solicitudId: 's-1',
    estado,
    eventos: [{ fechaHora: '2026-10-20T12:00:00Z', tipo: 'solicitud', descripcion: 'Solicitud registrada (Pendiente).', actorId: null, motivo: null, referenciaId: null }],
  }),
});

const abrir = (usuario = undefined as typeof administrador | undefined) =>
  renderizar(<DetalleSolicitud />, { ruta: '/solicitudes/s-1', patron: '/solicitudes/:id', usuario });

describe('Detalle de la solicitud', () => {
  it('pendiente: muestra la trazabilidad y "asignar ya" pide al motor', async () => {
    const { peticiones } = apiFalsa({
      ...rutas('Pendiente'),
      'POST /api/solicitudes/s-1/asignar': () => ({ solicitudId: 's-1', resultado: 'Asignada', asignacionId: 'a-1', conductorId: 'c', vehiculoId: 'v', decisionId: 'd', motivo: 'Asignada a Carlos.' }),
    });
    const usuario = userEvent.setup();
    abrir();

    expect(await screen.findByText('Solicitud registrada (Pendiente).')).toBeInTheDocument();
    expect(screen.getByText(/6:00.*America\/Bogota/)).toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();
    await usuario.click(screen.getByRole('button', { name: 'Asignar ya' }));

    expect(await screen.findByText('Asignada a Carlos.')).toBeInTheDocument();
    expect(peticiones.some((p) => p.method === 'POST' && p.url.endsWith('/asignar'))).toBe(true);
  });

  it('asignada: el Operador logístico puede devolverla a la cola con un motivo; el Administrador no ve esa acción', async () => {
    const { peticiones } = apiFalsa({ ...rutas('Asignada'), 'POST /api/solicitudes/s-1/devolver-a-cola': () => new Response(null, { status: 204 }) });
    const usuario = userEvent.setup();
    abrir();

    await usuario.click(await screen.findByRole('button', { name: 'Devolver a la cola' }));
    await usuario.type(await screen.findByLabelText(/Motivo/), 'El conductor reportó una falla');
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(await screen.findByText('La solicitud volvió a la cola.')).toBeInTheDocument();
    const envio = peticiones.find((p) => p.url.endsWith('/devolver-a-cola'))!;
    expect(await envio.json()).toEqual({ motivo: 'El conductor reportó una falla' });
  });

  it('el Administrador no ve "devolver a la cola" y una completada no se puede cancelar', async () => {
    apiFalsa(rutas('Asignada'));
    abrir(administrador);
    expect(await screen.findByRole('button', { name: 'Cancelar' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Devolver a la cola' })).not.toBeInTheDocument();
  });

  it('cancelar muestra el error de la regla de negocio', async () => {
    apiFalsa({
      ...rutas('Pendiente'),
      'POST /api/solicitudes/s-1/cancelar': () => new Response(JSON.stringify({ detail: 'Ya pasó la ventana de cancelación.' }), { status: 400 }),
    });
    const usuario = userEvent.setup();
    abrir();

    await usuario.click(await screen.findByRole('button', { name: 'Cancelar' }));
    await usuario.type(await screen.findByLabelText(/Motivo/), 'Cambio de planes');
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent('Ya pasó la ventana de cancelación.');
  });

  it('el Operador logístico acepta en nombre del conductor: se crea el viaje y se avisa al pasajero', async () => {
    const { peticiones } = apiFalsa({
      ...rutas('Asignada', [asignacion('a-1', 'Asignada')]),
      'POST /api/asignaciones/a-1/aceptar': () =>
        new Response(JSON.stringify({ id: 'v-1', asignacionId: 'a-1', estado: 'Programado', fechaHoraInicio: null, fechaHoraFin: null }), {
          status: 201,
        }),
    });
    const usuario = userEvent.setup();
    abrir();

    const tarjeta = await screen.findByRole('region', { name: 'Conductor asignado' });
    expect(await within(tarjeta).findByText('Carlos Pérez')).toBeInTheDocument();
    expect(within(tarjeta).getByText('Esperando al conductor')).toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();

    await usuario.click(within(tarjeta).getByRole('button', { name: 'Aceptar' }));
    const dialogo = await screen.findByRole('dialog');
    expect(within(dialogo).getByText(/el pasajero recibe por WhatsApp/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Confirmar' }));

    expect(await screen.findByText('Asignación aceptada: se avisará al pasajero por WhatsApp.')).toBeInTheDocument();
    expect(peticiones.some((p) => p.method === 'POST' && p.url.endsWith('/api/asignaciones/a-1/aceptar'))).toBe(true);
  });

  it('rechazar en nombre del conductor pide el motivo', async () => {
    const { peticiones } = apiFalsa({
      ...rutas('Asignada', [asignacion('a-1', 'Asignada')]),
      'POST /api/asignaciones/a-1/rechazar': () => new Response(null, { status: 204 }),
    });
    const usuario = userEvent.setup();
    abrir();

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar' }));
    await usuario.type(await screen.findByLabelText(/Motivo/), 'No contesta el teléfono');
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(await screen.findByText('Asignación rechazada: el motor buscará otro conductor.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.url.endsWith('/rechazar'))!.json()).toEqual({ motivo: 'No contesta el teléfono' });
  });

  it('el Administrador ve al conductor asignado pero no responde por él', async () => {
    apiFalsa(rutas('Asignada', [asignacion('a-0', 'Reemplazada', { conductorNombre: 'Otro' }), asignacion('a-1', 'Asignada')]));
    abrir(administrador);

    const tarjeta = await screen.findByRole('region', { name: 'Conductor asignado' });
    expect(await within(tarjeta).findByText('Carlos Pérez')).toBeInTheDocument();
    expect(within(tarjeta).getByText(/1 asignación\(es\) anterior/)).toBeInTheDocument();
    expect(within(tarjeta).queryByRole('button', { name: 'Aceptar' })).not.toBeInTheDocument();
  });

  it('las decisiones del motor muestran a cada candidato con su nombre y por qué se descartó', async () => {
    apiFalsa({
      ...rutas('SinCandidatos'),
      'GET /api/solicitudes/s-1/decisiones': () => [
        {
          id: 'd-1',
          solicitudId: 's-1',
          versionConfiguracion: 2,
          origen: 'Automatica',
          evaluadaEn: '2026-10-21T16:15:00Z',
          resultado: 'SinCandidatos',
          conductorSeleccionadoId: null,
          vehiculoSeleccionadoId: null,
          motivo: 'Ningún conductor cumple las reglas.',
          desempate: null,
          evaluaciones: [
            {
              conductorId: 'c-9',
              vehiculoId: 'v-9',
              turnoId: 't-9',
              elegible: false,
              motivoDescarte: 'La capacidad del vehículo no alcanza.',
              prioridad: 0,
              puntajeTotal: 0,
              posicionRanking: null,
              resultados: [],
            },
          ],
        },
      ],
      'GET /api/conductores': () => pagina([{ id: 'c-9', nombre: 'Diana Ruiz' }]),
    });
    const usuario = userEvent.setup();
    abrir();

    expect(await screen.findByText('Sin conductor disponible')).toBeInTheDocument();
    await usuario.click(screen.getByRole('tab', { name: 'Decisiones del motor' }));

    expect(await screen.findByText('La capacidad del vehículo no alcanza.')).toBeInTheDocument();
    expect(await screen.findByText('Diana Ruiz')).toBeInTheDocument();
    expect(screen.getByText('Configuración v2')).toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();
  });
});
