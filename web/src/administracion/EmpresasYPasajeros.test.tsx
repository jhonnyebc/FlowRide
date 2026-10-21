import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { empresa, pagina, pasajero } from '../pruebas/datos';
import { administrador, apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { PaginaEmpresas } from './PaginaEmpresas';
import { PaginaPasajeros } from './PaginaPasajeros';

describe('Empresas cliente (RF-04)', () => {
  it('el Operador logístico las consulta sin acciones de edición', async () => {
    const { peticiones } = apiFalsa({ 'GET /api/empresas-cliente': () => pagina([empresa('e-1', { nombre: 'Minera del Sur' })]) });

    renderizar(<PaginaEmpresas />);

    expect(await screen.findByText('Minera del Sur')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Nueva empresa' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Acciones de/ })).not.toBeInTheDocument();
    expect(new URL(peticiones[0]!.url).searchParams.get('Activo')).toBe('true');
    await sinViolacionesDeAccesibilidad();
  });

  it('el Administrador registra una empresa y desactiva otra con confirmación', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/empresas-cliente': () => pagina([empresa('e-1', { nombre: 'Minera del Sur' })]),
      'POST /api/empresas-cliente': () => new Response(JSON.stringify(empresa('e-2', { nombre: 'Agro SAS' })), { status: 201 }),
      'POST /api/empresas-cliente/e-1/desactivar': () => empresa('e-1', { nombre: 'Minera del Sur', activa: false }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaEmpresas />, { usuario: administrador });

    await usuario.click(await screen.findByRole('button', { name: 'Nueva empresa' }));
    const panel = await screen.findByRole('dialog', { name: 'Nueva empresa' });
    await usuario.type(within(panel).getByLabelText(/Razón social/), 'Agro SAS');
    await usuario.type(within(panel).getByLabelText(/NIT/), '900555444-1');
    await usuario.type(within(panel).getByLabelText(/Correo de contacto/), 'ops@agro.co');
    await usuario.click(within(panel).getByRole('button', { name: 'Registrar empresa' }));
    expect(await screen.findByText('Agro SAS quedó registrada.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'POST' && p.url.endsWith('/api/empresas-cliente'))!.json()).toEqual({
      nombre: 'Agro SAS',
      nit: '900555444-1',
      correoContacto: 'ops@agro.co',
    });

    await usuario.click(screen.getByRole('button', { name: 'Acciones de Minera del Sur' }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Desactivar' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Desactivar empresa' });
    expect(within(dialogo).getByText(/no podrán pedir viajes/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Desactivar' }));
    expect(await screen.findByText('Minera del Sur quedó inactiva.')).toBeInTheDocument();
  });
});

describe('Pasajeros (RF-05)', () => {
  const rutas = {
    'GET /api/empresas-cliente': () => pagina([empresa('e-1', { nombre: 'Minera del Sur' }), empresa('e-2', { nombre: 'Agro SAS', activa: false })]),
    'GET /api/pasajeros': () => pagina([pasajero('p-1', { nombre: 'Ana Gómez' })]),
  };

  it('muestra la empresa por su nombre y filtra por empresa', async () => {
    const { peticiones } = apiFalsa(rutas);
    const usuario = userEvent.setup();
    renderizar(<PaginaPasajeros />);

    expect(await screen.findByText('Ana Gómez')).toBeInTheDocument();
    expect(await screen.findByRole('cell', { name: 'Minera del Sur' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Nuevo pasajero' })).not.toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();

    await usuario.click(screen.getByRole('textbox', { name: 'Empresa' }));
    await usuario.click(await screen.findByRole('option', { name: 'Minera del Sur' }));

    await screen.findByText('Ana Gómez');
    const ultima = peticiones.filter((p) => p.url.includes('/api/pasajeros')).at(-1)!;
    expect(new URL(ultima.url).searchParams.get('EmpresaClienteId')).toBe('e-1');
  });

  it('el Administrador registra un pasajero eligiendo solo entre empresas activas', async () => {
    const { peticiones } = apiFalsa({
      ...rutas,
      'POST /api/pasajeros': () => new Response(JSON.stringify(pasajero('p-2', { nombre: 'Luis Díaz' })), { status: 201 }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaPasajeros />, { usuario: administrador });

    await usuario.click(await screen.findByRole('button', { name: 'Nuevo pasajero' }));
    const panel = await screen.findByRole('dialog', { name: 'Nuevo pasajero' });
    await usuario.type(within(panel).getByLabelText(/Nombre completo/), 'Luis Díaz');
    await usuario.type(within(panel).getByLabelText(/WhatsApp/), '+573004443322');
    await usuario.click(within(panel).getByRole('textbox', { name: 'Empresa' }));
    expect(screen.queryByRole('option', { name: 'Agro SAS' })).not.toBeInTheDocument();
    await usuario.click(await screen.findByRole('option', { name: 'Minera del Sur' }));
    await usuario.type(within(panel).getByLabelText(/Código interno/), 'E-200');
    await usuario.click(within(panel).getByRole('button', { name: 'Registrar pasajero' }));

    expect(await screen.findByText('Luis Díaz ya puede pedir viajes por WhatsApp.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'POST')!.json()).toEqual({
      nombre: 'Luis Díaz',
      telefonoWhatsApp: '+573004443322',
      correo: null,
      empresaClienteId: 'e-1',
      codigoInterno: 'E-200',
    });
  });
});
