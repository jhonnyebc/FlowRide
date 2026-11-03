import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { configuracionMotor, parametroEntero, plantilla } from '../pruebas/datos';
import { administrador, apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { PaginaMotor } from './PaginaMotor';
import { PaginaParametros } from './PaginaParametros';
import { PaginaPlantillas } from './PaginaPlantillas';

const parametros = [
  parametroEntero('recordatorio.anticipacion_minutos', 60, { minimo: 5 }),
  parametroEntero('calificacion.plazo_dias', 10, { valorPorDefecto: 7, esPorDefecto: false, modificadoEn: '2026-10-20T15:00:00Z', maximo: 90 }),
  {
    ...parametroEntero('solicitud.visibilidad_personales', 0),
    tipo: 'Opcion',
    valor: 'PasajeroYOperadorLogistico',
    valorPorDefecto: 'PasajeroYOperadorLogistico',
    minimo: null,
    maximo: null,
    opciones: ['PasajeroYOperadorLogistico', 'SoloPasajero'],
  },
  { ...parametroEntero('operacion.franjas_criticas', 0), tipo: 'Franjas', valor: ['03:00-05:00'], valorPorDefecto: [], minimo: null, maximo: null },
];

describe('Parámetros (ADR-0016)', () => {
  it('el Operador logístico los ve en lenguaje claro y sin poder cambiarlos', async () => {
    apiFalsa({ 'GET /api/parametros': () => parametros });
    renderizar(<PaginaParametros />);

    expect(await screen.findByText('Anticipación del recordatorio')).toBeInTheDocument();
    expect(screen.getByText('60 min')).toBeInTheDocument();
    expect(screen.getByText('10 días')).toBeInTheDocument();
    expect(screen.getByText('El pasajero y el Operador logístico')).toBeInTheDocument();
    expect(screen.getByText('03:00-05:00')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Cambiar/ })).not.toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();
  });

  it('el Administrador cambia un número y otro lo devuelve a su valor por defecto', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/parametros': () => parametros,
      'PUT /api/parametros/recordatorio.anticipacion_minutos': () => parametroEntero('recordatorio.anticipacion_minutos', 45, { esPorDefecto: false }),
      'PUT /api/parametros/calificacion.plazo_dias': () => parametroEntero('calificacion.plazo_dias', 7),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaParametros />, { usuario: administrador });

    await usuario.click(await screen.findByRole('button', { name: 'Cambiar Anticipación del recordatorio' }));
    const panel = await screen.findByRole('dialog', { name: 'Cambiar: Anticipación del recordatorio' });
    expect(within(panel).getByText('Entre 5 y 1440')).toBeInTheDocument();
    await usuario.clear(within(panel).getByLabelText(/Valor/));
    await usuario.type(within(panel).getByLabelText(/Valor/), '45');
    await usuario.click(within(panel).getByRole('button', { name: 'Guardar' }));
    expect(await screen.findByText('Anticipación del recordatorio: 45 min.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'PUT')!.json()).toEqual({ valor: 45 });

    await usuario.click(screen.getByRole('button', { name: 'Cambiar Plazo para calificar el viaje' }));
    const otro = await screen.findByRole('dialog', { name: 'Cambiar: Plazo para calificar el viaje' });
    await usuario.click(within(otro).getByRole('button', { name: 'Volver al valor por defecto' }));
    expect(await screen.findByText('Plazo para calificar el viaje: 7 días.')).toBeInTheDocument();
    expect(await peticiones.filter((p) => p.method === 'PUT').at(-1)!.json()).toEqual({ valor: 7 });
  });
});

describe('Plantillas de WhatsApp (ADR-0016)', () => {
  it('el Administrador registra que Meta aprobó una plantilla', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/notificaciones/plantillas': () => [plantilla('solicitud.pasajero.conductor_asignado', 'Pendiente')],
      'PUT /api/notificaciones/plantillas/solicitud.pasajero.conductor_asignado': () => plantilla('solicitud.pasajero.conductor_asignado', 'Aprobada'),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaPlantillas />, { usuario: administrador });

    expect(await screen.findByRole('cell', { name: 'Pendiente' })).toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();
    await usuario.click(screen.getByRole('button', { name: 'Editar la plantilla solicitud.pasajero.conductor_asignado' }));
    const panel = await screen.findByRole('dialog', { name: 'Plantilla solicitud.pasajero.conductor_asignado' });
    expect(within(panel).getByText(/2 variable\(s\), en este orden: \{\{1\}\} conductor, \{\{2\}\} placa/)).toBeInTheDocument();
    await usuario.click(within(panel).getByRole('textbox', { name: 'Estado en Meta' }));
    await usuario.click(await screen.findByRole('option', { name: 'Aprobada' }));
    await usuario.click(within(panel).getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText('Plantilla solicitud.pasajero.conductor_asignado: Aprobada.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'PUT')!.json()).toEqual({
      nombre: 'solicitud_pasajero_conductor_asignado',
      idioma: 'es_CO',
      estado: 'Aprobada',
    });
  });
});

describe('Motor de asignación (RF-21, ADR-0009)', () => {
  const versiones = () => [configuracionMotor(2, 'Activa'), configuracionMotor(1, 'Archivada')];

  it('el Operador logístico ve la versión activa con sus reglas y el historial, sin acciones', async () => {
    apiFalsa({ 'GET /api/motor/configuraciones': versiones });
    renderizar(<PaginaMotor />);

    expect(await screen.findByText('Versión activa: 2')).toBeInTheDocument();
    expect(screen.getAllByText('Balance de carga entre conductores').length).toBeGreaterThan(0);
    expect(screen.getByText('Cuenta las asignaciones de los últimos 7 días')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Nueva versión' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Activar la versión/ })).not.toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();
  });

  it('el Administrador crea un borrador a partir de la activa sin poder deshabilitar las eliminatorias', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/motor/configuraciones': versiones,
      'POST /api/motor/configuraciones': () => new Response(JSON.stringify(configuracionMotor(3, 'Borrador')), { status: 201 }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaMotor />, { usuario: administrador });

    await usuario.click(await screen.findByRole('button', { name: 'Nueva versión' }));
    const panel = await screen.findByRole('dialog', { name: 'Nueva versión del motor' });
    expect(within(panel).getByRole('switch', { name: 'Disponibilidad del conductor y el vehículo: habilitada' })).toBeDisabled();
    await usuario.click(within(panel).getByRole('switch', { name: 'Prioridad en servicios críticos: habilitada' }));
    const peso = within(panel).getByRole('textbox', { name: 'Balance de carga entre conductores: peso' });
    await usuario.clear(peso);
    await usuario.type(peso, '0.5');
    const ventana = within(panel).getByRole('textbox', { name: 'Balance de carga entre conductores: ventana en días' });
    await usuario.clear(ventana);
    await usuario.type(ventana, '14');
    await usuario.click(within(panel).getByRole('button', { name: 'Crear borrador' }));

    expect(await screen.findByText('Se creó la versión 3 como borrador. Actívala para que el motor la use.')).toBeInTheDocument();
    const { reglas } = (await peticiones.find((p) => p.method === 'POST')!.json()) as { reglas: { codigo: string; habilitada: boolean; peso: number; parametros: Record<string, string> }[] };
    expect(reglas).toHaveLength(7);
    expect(reglas.find((r) => r.codigo === 'CriticalServicePriorityRule')!.habilitada).toBe(false);
    expect(reglas.find((r) => r.codigo === 'WorkloadBalanceRule')).toMatchObject({ peso: 0.5, parametros: { ventanaDias: '14' } });
    expect(reglas.find((r) => r.codigo === 'AvailabilityRule')!.habilitada).toBe(true);
  });

  it('el Administrador vuelve a una versión anterior con confirmación', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/motor/configuraciones': versiones,
      'POST /api/motor/configuraciones/1/activar': () => configuracionMotor(1, 'Activa'),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaMotor />, { usuario: administrador });

    await usuario.click(await screen.findByRole('button', { name: 'Activar la versión 1' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Activar la versión 1' });
    expect(within(dialogo).getByText(/La versión 2 queda archivada/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Activar' }));

    expect(await screen.findByText('El motor usa ahora la versión 1.')).toBeInTheDocument();
    expect(peticiones.some((p) => p.method === 'POST' && p.url.endsWith('/api/motor/configuraciones/1/activar'))).toBe(true);
  });
});
