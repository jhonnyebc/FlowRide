import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { pagina, solicitud } from '../pruebas/datos';
import { apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { PaginaSolicitudes } from './PaginaSolicitudes';

describe('Solicitudes', () => {
  it('lista con la hora local de su zona, oculta las direcciones restringidas y filtra por estado', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/solicitudes': () =>
        pagina([solicitud('s-1', 'Pendiente', { urgente: true }), solicitud('s-2', 'Asignada', { detalleRestringido: true, direccionOrigen: null })]),
    });
    const usuario = userEvent.setup();

    renderizar(<PaginaSolicitudes />);

    expect(await screen.findAllByText(/\(America\/Bogota\)/)).toHaveLength(2);
    expect(screen.getByText('Urgente')).toBeInTheDocument();
    expect(screen.getByText('Personal (restringida)')).toBeInTheDocument();
    expect(screen.getByText('Hotel Central → Aeropuerto')).toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();

    await usuario.click(screen.getByRole('textbox', { name: 'Estado' }));
    await usuario.click(await screen.findByRole('option', { name: 'Sin candidatos' }));

    await screen.findAllByText(/America\/Bogota/);
    expect(new URL(peticiones.at(-1)!.url).searchParams.get('Estado')).toBe('SinCandidatos');
  });

  it('una solicitud nueva envía la hora local sin desfase', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/solicitudes': () => pagina([]),
      'GET /api/pasajeros': () =>
        pagina([{ id: 'p-9', nombre: 'Luis Mora', telefonoWhatsApp: '+573001112233', correo: null, empresaClienteId: 'e-1', codigoInterno: 'E-9', activo: true }]),
      'POST /api/solicitudes': () => new Response(JSON.stringify({ solicitud: solicitud('s-9', 'Pendiente'), historial: [] }), { status: 201 }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaSolicitudes />);

    await usuario.click(await screen.findByRole('button', { name: 'Nueva solicitud' }));
    await usuario.click(await screen.findByRole('textbox', { name: 'Pasajero' }));
    await usuario.click(await screen.findByRole('option', { name: 'Luis Mora · E-9' }));
    await usuario.type(screen.getByLabelText(/Dirección de recogida/), 'Hotel Central');
    await usuario.type(screen.getByLabelText(/Dirección de destino/), 'Aeropuerto');
    await usuario.type(screen.getByLabelText(/Fecha y hora/), '2026-10-22T06:00');
    await sinViolacionesDeAccesibilidad();
    await usuario.click(screen.getByRole('button', { name: 'Registrar' }));

    await screen.findByText(/Solicitud registrada/);
    const envio = peticiones.find((p) => p.method === 'POST')!;
    const cuerpo = (await envio.json()) as Record<string, unknown>;
    expect(cuerpo).toMatchObject({ pasajeroId: 'p-9', fechaHoraServicio: '2026-10-22T06:00', duracionMinutos: 60, cantidadPasajeros: 1 });
    expect(cuerpo).not.toHaveProperty('zonaHoraria');
  });
});
