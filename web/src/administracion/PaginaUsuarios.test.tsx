import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { usuarioPanel } from '../pruebas/datos';
import { administrador, apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { PaginaUsuarios } from './PaginaUsuarios';

const lista = () => [
  usuarioPanel('u-2', 'Administrador', { nombre: 'Ana Rojas', correo: 'ana@ops.co' }),
  usuarioPanel('u-3', 'OperadorLogistico', { nombre: 'Laura Gómez', correo: 'laura@ops.co' }),
  usuarioPanel('u-4', 'OperadorLogistico', { nombre: 'Beto Ruiz', activo: false }),
  // Pasajeros y conductores también son usuarios, pero no del panel: no se listan aquí.
  { ...usuarioPanel('u-9', 'OperadorLogistico', { nombre: 'Carlos Conductor' }), rol: 'Conductor' },
];

const abrir = () => renderizar(<PaginaUsuarios />, { usuario: administrador });

describe('Usuarios del panel (RF-02, RF-24, RF-25)', () => {
  it('lista solo los del panel, con su rol y estado, y cuenta por rol', async () => {
    apiFalsa({ 'GET /api/usuarios': lista });

    abrir();

    expect(await screen.findByText('Laura Gómez')).toBeInTheDocument();
    expect(screen.queryByText('Carlos Conductor')).not.toBeInTheDocument();
    expect(screen.queryByText('Beto Ruiz')).not.toBeInTheDocument(); // por defecto, solo activos
    expect(screen.getByText('Operadores logísticos').previousSibling).toHaveTextContent('1');
    expect(screen.getByText('Desactivados').previousSibling).toHaveTextContent('1');
    expect(screen.getByText(/ana@ops\.co · tú/)).toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();
  });

  it('crea un usuario desde el panel lateral con el rol elegido', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/usuarios': lista,
      'POST /api/usuarios': () =>
        new Response(JSON.stringify(usuarioPanel('u-5', 'OperadorLogistico', { correo: 'nuevo@ops.co' })), { status: 201 }),
    });
    const usuario = userEvent.setup();
    abrir();

    await usuario.click(await screen.findByRole('button', { name: 'Nuevo usuario' }));
    const panel = await screen.findByRole('dialog', { name: 'Nuevo usuario' });
    await usuario.type(within(panel).getByLabelText(/Nombre completo/), 'Pedro Pérez');
    await usuario.type(within(panel).getByLabelText(/Correo/), 'nuevo@ops.co');
    await usuario.type(within(panel).getByLabelText(/WhatsApp/), '+573009998877');
    await usuario.click(within(panel).getByRole('button', { name: 'Crear usuario' }));

    expect(await screen.findByText(/a nuevo@ops\.co le llegó el correo para definir su contraseña/)).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'POST')!.json()).toEqual({
      nombre: 'Pedro Pérez',
      correo: 'nuevo@ops.co',
      telefonoWhatsApp: '+573009998877',
      rol: 'OperadorLogistico',
    });
  });

  it('editar el correo avisa que el usuario debe confirmarlo', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/usuarios': lista,
      'PUT /api/usuarios/u-3': () => usuarioPanel('u-3', 'OperadorLogistico', { nombre: 'Laura Gómez', correo: 'laura.g@ops.co' }),
    });
    const usuario = userEvent.setup();
    abrir();

    await usuario.click(await screen.findByRole('button', { name: 'Acciones de Laura Gómez' }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Editar datos' }));
    const panel = await screen.findByRole('dialog', { name: 'Editar a Laura Gómez' });
    const correo = within(panel).getByLabelText(/Correo/);
    await usuario.clear(correo);
    await usuario.type(correo, 'laura.g@ops.co');
    await usuario.click(within(panel).getByRole('button', { name: 'Guardar cambios' }));

    expect(await screen.findByText('Datos guardados. Laura Gómez debe confirmar su nuevo correo.')).toBeInTheDocument();
    expect(await peticiones.find((p) => p.method === 'PUT')!.json()).toEqual({
      nombre: 'Laura Gómez',
      correo: 'laura.g@ops.co',
      telefonoWhatsApp: '+573001112233',
    });
  });

  it('desactivar pide confirmación; uno mismo no puede cambiarse el rol ni el estado', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/usuarios': lista,
      'POST /api/usuarios/u-3/desactivar': () => usuarioPanel('u-3', 'OperadorLogistico', { nombre: 'Laura Gómez', activo: false }),
    });
    const usuario = userEvent.setup();
    abrir();

    await usuario.click(await screen.findByRole('button', { name: 'Acciones de Ana Rojas' }));
    expect(await screen.findByRole('menuitem', { name: 'Desactivar' })).toBeDisabled();
    expect(screen.getByRole('menuitem', { name: 'Cambiar rol' })).toBeDisabled();
    await usuario.keyboard('{Escape}');

    await usuario.click(screen.getByRole('button', { name: 'Acciones de Laura Gómez' }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Desactivar' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Desactivar usuario' });
    expect(within(dialogo).getByText(/se cerrarán sus sesiones abiertas/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Desactivar' }));

    expect(await screen.findByText('Laura Gómez ya no puede entrar al panel.')).toBeInTheDocument();
    expect(peticiones.some((p) => p.url.endsWith('/api/usuarios/u-3/desactivar'))).toBe(true);
  });
});
