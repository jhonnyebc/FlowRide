import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { horaLocal } from '../comun/fechas';
import { conductor, documento, empresa, itinerario, pagina, turno, vehiculo } from '../pruebas/datos';
import { administrador, apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { PaginaConductores } from './PaginaConductores';
import { PaginaItinerarios } from './PaginaItinerarios';
import { PaginaVehiculos } from './PaginaVehiculos';

describe('Vehículos (RF-07)', () => {
  const rutas = {
    'GET /api/conductores': () => pagina([conductor('c-1', { nombre: 'Carlos Pérez' })]),
    'GET /api/vehiculos': () =>
      pagina([
        vehiculo('v-1', { placa: 'ABC123', conductorHabitualId: 'c-1' }),
        vehiculo('v-2', { placa: 'XYZ789', vencimientoSoat: '2020-01-31' }),
      ]),
  };

  it('el Operador logístico ve la flota con los vencimientos en texto y sin acciones', async () => {
    apiFalsa(rutas);
    renderizar(<PaginaVehiculos />);

    expect(await screen.findByText('ABC123')).toBeInTheDocument();
    expect(await screen.findByRole('cell', { name: 'Carlos Pérez' })).toBeInTheDocument();
    expect(screen.getByText(/Vencido · 31 de ene de 2020/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Nuevo vehículo' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Acciones de/ })).not.toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();
  });

  it('el Administrador registra un vehículo y le asigna un conductor habitual', async () => {
    const { peticiones } = apiFalsa({
      ...rutas,
      'POST /api/vehiculos': () => new Response(JSON.stringify(vehiculo('v-3', { placa: 'JKL456' })), { status: 201 }),
      'PUT /api/vehiculos/v-2/conductor-habitual': () => vehiculo('v-2', { conductorHabitualId: 'c-1' }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaVehiculos />, { usuario: administrador });

    await usuario.click(await screen.findByRole('button', { name: 'Nuevo vehículo' }));
    const panel = await screen.findByRole('dialog', { name: 'Nuevo vehículo' });
    await usuario.type(within(panel).getByLabelText(/Placa/), 'jkl456');
    await usuario.type(within(panel).getByLabelText(/Marca/), 'Toyota');
    await usuario.type(within(panel).getByLabelText(/Modelo/), 'Hiace');
    await usuario.clear(within(panel).getByLabelText(/Capacidad/));
    await usuario.type(within(panel).getByLabelText(/Capacidad/), '15');
    await usuario.type(within(panel).getByLabelText(/Vence la RTM/), '2027-03-31');
    await usuario.type(within(panel).getByLabelText(/Vence el SOAT/), '2027-01-15');
    await usuario.click(within(panel).getByRole('button', { name: 'Registrar vehículo' }));

    expect(await screen.findByText('El vehículo JKL456 quedó registrado.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'POST')!.json()).toEqual({
      placa: 'JKL456',
      marca: 'Toyota',
      modelo: 'Hiace',
      capacidad: 15,
      vencimientoRtm: '2027-03-31',
      vencimientoSoat: '2027-01-15',
    });

    await usuario.click(screen.getByRole('button', { name: 'Acciones de el vehículo XYZ789' }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Conductor habitual' }));
    const asignar = await screen.findByRole('dialog', { name: 'Conductor habitual de XYZ789' });
    await usuario.click(within(asignar).getByRole('textbox', { name: 'Conductor' }));
    await usuario.click(await screen.findByRole('option', { name: 'Carlos Pérez' }));
    await usuario.click(within(asignar).getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText('Conductor habitual asignado.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'PUT')!.json()).toEqual({ conductorId: 'c-1' });
  });
});

describe('Conductores (RF-07, RF-12, RF-26)', () => {
  const detalle = {
    conductor: conductor('c-1', { nombre: 'Carlos Pérez' }),
    turnos: [turno('t-1', 'Programado'), turno('t-0', 'Finalizado', { inicioLocal: '2026-10-20T06:00:00', finLocal: '2026-10-20T14:00:00' })],
    documentos: [documento('d-1', 'Licencia', '2020-05-01')],
  };
  const rutas = {
    'GET /api/conductores': () => pagina([conductor('c-1', { nombre: 'Carlos Pérez' })]),
    'GET /api/conductores/c-1': () => detalle,
  };

  const abrirDetalle = async (usuario: ReturnType<typeof userEvent.setup>) => {
    await usuario.click(await screen.findByRole('button', { name: 'Acciones de Carlos Pérez' }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Turnos y documentos' }));
    return screen.findByRole('dialog', { name: 'Carlos Pérez' });
  };

  it('el Operador logístico programa un turno en hora local y registra un abandono con motivo', async () => {
    const { peticiones } = apiFalsa({
      ...rutas,
      'POST /api/conductores/c-1/turnos': () => new Response(JSON.stringify(turno('t-2', 'Programado')), { status: 201 }),
      'POST /api/conductores/c-1/turnos/t-1/abandonar': () => ({ solicitudesDevueltas: ['s-1', 's-2'] }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaConductores />);

    expect(screen.queryByRole('button', { name: 'Nuevo conductor' })).not.toBeInTheDocument();
    const panel = await abrirDetalle(usuario);
    expect(await within(panel).findByText(horaLocal('2026-10-27T06:00:00', 'America/Bogota'))).toBeInTheDocument();
    expect(within(panel).getByText('Últimos turnos terminados')).toBeInTheDocument();
    expect(within(panel).queryByRole('form', { name: 'Cargar documento' })).not.toBeInTheDocument();
    expect(within(panel).queryByRole('button', { name: /Eliminar/ })).not.toBeInTheDocument();
    await sinViolacionesDeAccesibilidad(panel);

    const formulario = within(panel).getByRole('form', { name: 'Programar turno' });
    await usuario.type(within(formulario).getByLabelText(/Inicio del turno/), '2026-10-28T06:00');
    await usuario.type(within(formulario).getByLabelText(/Fin del turno/), '2026-10-28T14:00');
    await usuario.click(within(formulario).getByRole('button', { name: 'Programar turno' }));
    expect(await screen.findByText(/Turno programado/)).toBeInTheDocument();
    expect(await peticiones.find((p) => p.url.endsWith('/turnos'))!.json()).toEqual({ inicio: '2026-10-28T06:00', fin: '2026-10-28T14:00' });

    await usuario.click(within(panel).getByRole('button', { name: /Registrar abandono del turno/ }));
    const confirmar = await screen.findByRole('dialog', { name: 'Registrar abandono de turno' });
    await usuario.type(within(confirmar).getByLabelText(/Motivo/), 'Se enfermó');
    await usuario.click(within(confirmar).getByRole('button', { name: 'Registrar abandono' }));
    expect(await screen.findByText('Abandono registrado: 2 solicitud(es) volvieron a la cola de asignación.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.url.endsWith('/abandonar'))!.json()).toEqual({ motivo: 'Se enfermó' });
  });

  it('el Administrador carga un documento, lo descarga con un enlace temporal y no ve el abandono', async () => {
    const abrirVentana = vi.spyOn(window, 'open').mockReturnValue(null);
    const { peticiones } = apiFalsa({
      ...rutas,
      'POST /api/conductores/c-1/documentos': () => new Response(JSON.stringify(documento('d-2', 'Soat', '2027-01-15')), { status: 201 }),
      'GET /api/conductores/c-1/documentos/d-1/descarga': () => ({ url: 'http://objetos.local/d-1?firma=x', expiraEn: '2026-10-24T12:05:00Z' }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaConductores />, { usuario: administrador });

    const panel = await abrirDetalle(usuario);
    expect(await within(panel).findByText(/Vencido · 1 de may de 2020/)).toBeInTheDocument();
    expect(within(panel).queryByRole('button', { name: /Registrar abandono/ })).not.toBeInTheDocument();

    await usuario.click(within(panel).getByRole('button', { name: 'Descargar Licencia de conducción' }));
    await vi.waitFor(() => expect(abrirVentana).toHaveBeenCalledWith('http://objetos.local/d-1?firma=x', '_blank', 'noopener,noreferrer'));

    const formulario = within(panel).getByRole('form', { name: 'Cargar documento' });
    await usuario.click(within(formulario).getByRole('textbox', { name: 'Tipo de documento' }));
    await usuario.click(await screen.findByRole('option', { name: 'SOAT' }));
    await usuario.type(within(formulario).getByLabelText(/Vence/), '2027-01-15');
    // jsdom tiene su propio FormData y el Request de las pruebas es el de Node, así que el cuerpo multipart no se puede
    // leer de vuelta aquí; se verifica lo que el panel pone en el formulario (en el navegador todo es nativo).
    const campos = vi.spyOn(FormData.prototype, 'append');
    const archivo = new File(['%PDF-1.7'], 'soat.pdf', { type: 'application/pdf' });
    // El campo visible es un botón; el archivo entra por el input oculto que Mantine pone detrás.
    await usuario.upload(formulario.querySelector<HTMLInputElement>('input[type="file"]')!, archivo);
    await usuario.click(within(formulario).getByRole('button', { name: 'Cargar documento' }));

    expect(await screen.findByText('Documento cargado: SOAT.')).toBeInTheDocument();
    expect(peticiones.some((p) => p.method === 'POST' && p.url.endsWith('/api/conductores/c-1/documentos'))).toBe(true);
    expect(campos).toHaveBeenCalledWith('archivo', archivo);
    expect(campos).toHaveBeenCalledWith('tipo', 'Soat');
    expect(campos).toHaveBeenCalledWith('fechaVencimiento', '2027-01-15');
  });

  it('el Administrador cambia el estado de un conductor con confirmación', async () => {
    const { peticiones } = apiFalsa({
      ...rutas,
      'PUT /api/conductores/c-1/estado': () => conductor('c-1', { nombre: 'Carlos Pérez', estado: 'Suspendido' }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaConductores />, { usuario: administrador });

    await usuario.click(await screen.findByRole('button', { name: 'Acciones de Carlos Pérez' }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Cambiar estado' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Estado de Carlos Pérez' });
    await usuario.click(within(dialogo).getByRole('textbox', { name: 'Nuevo estado' }));
    await usuario.click(await screen.findByRole('option', { name: 'Suspendido' }));
    await usuario.click(within(dialogo).getByRole('button', { name: 'Cambiar estado' }));

    expect(await screen.findByText('Carlos Pérez quedó suspendido.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'PUT')!.json()).toEqual({ estado: 'Suspendido' });
  });
});

describe('Itinerarios (RF-08)', () => {
  const rutas = {
    'GET /api/empresas-cliente': () => pagina([empresa('e-1', { nombre: 'Minera del Sur' })]),
    'GET /api/itinerarios': () => pagina([itinerario('i-1')]),
  };

  it('lista los itinerarios desde hoy con la empresa y el horario en su zona', async () => {
    const { peticiones } = apiFalsa(rutas);
    renderizar(<PaginaItinerarios />);

    expect(await screen.findByText('AV-123')).toBeInTheDocument();
    expect(await screen.findByRole('cell', { name: 'Minera del Sur' })).toBeInTheDocument();
    expect(screen.getByText('05:30 – 06:15')).toBeInTheDocument();
    expect(new URL(peticiones.find((p) => p.url.includes('/api/itinerarios'))!.url).searchParams.get('Desde')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    await sinViolacionesDeAccesibilidad();
  });

  it('el Operador logístico registra un servicio para un rango de fechas y días', async () => {
    const { peticiones } = apiFalsa({
      ...rutas,
      'POST /api/itinerarios': () => [itinerario('i-2'), itinerario('i-3')],
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaItinerarios />);

    await usuario.click(await screen.findByRole('button', { name: 'Registrar itinerario' }));
    const panel = await screen.findByRole('dialog', { name: 'Registrar itinerario' });
    await usuario.click(within(panel).getByRole('textbox', { name: 'Empresa' }));
    await usuario.click(await screen.findByRole('option', { name: 'Minera del Sur' }));
    await usuario.type(within(panel).getByLabelText(/Código de servicio/), 'AV-123');
    await usuario.type(within(panel).getByLabelText(/^Origen/), 'Hotel Central');
    await usuario.type(within(panel).getByLabelText(/^Destino/), 'Aeropuerto');
    await usuario.type(within(panel).getByLabelText(/Hora de salida/), '05:30');
    await usuario.type(within(panel).getByLabelText(/Hora de llegada/), '06:15');
    await usuario.type(within(panel).getByLabelText(/^Desde/), '2026-10-26');
    await usuario.type(within(panel).getByLabelText(/^Hasta/), '2026-11-01');
    for (const dia of ['Sábado', 'Domingo']) await usuario.click(within(panel).getByRole('checkbox', { name: dia }));
    await usuario.click(within(panel).getByRole('button', { name: 'Registrar' }));

    expect(await screen.findByText('Se registraron 2 itinerarios, uno por fecha.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'POST')!.json()).toEqual({
      empresaClienteId: 'e-1',
      codigoServicio: 'AV-123',
      origen: 'Hotel Central',
      destino: 'Aeropuerto',
      horaSalida: '05:30:00',
      horaLlegada: '06:15:00',
      fechaDesde: '2026-10-26',
      fechaHasta: '2026-11-01',
      dias: ['lunes', 'martes', 'miércoles', 'jueves', 'viernes'],
    });
  });
});
