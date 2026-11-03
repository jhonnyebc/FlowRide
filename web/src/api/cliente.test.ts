import { afterEach, describe, expect, it, vi } from 'vitest';
import { apiFalsa } from '../pruebas/utilidades';
import { api, conectarSesion, datos, ErrorApi } from './cliente';

describe('cliente de la API', () => {
  afterEach(() => conectarSesion(() => undefined, () => {}));

  it('manda el token de la sesión en Authorization (nunca en cookies)', async () => {
    const { peticiones } = apiFalsa({ 'GET /api/usuarios/yo': () => ({ id: '1', nombre: 'Ana', correo: null, rol: 'Administrador' }) });
    conectarSesion(() => 'token-123', () => {});

    const usuario = await datos(api.GET('/api/usuarios/yo'));

    expect(usuario.nombre).toBe('Ana');
    expect(peticiones[0]?.headers.get('Authorization')).toBe('Bearer token-123');
    expect(peticiones[0]?.credentials).not.toBe('include');
  });

  it('convierte un ProblemDetails en un mensaje entendible', async () => {
    apiFalsa({
      'POST /api/solicitudes/x/cancelar': () =>
        new Response(JSON.stringify({ title: 'Regla', detail: 'Ya no se puede cancelar.' }), { status: 400 }),
    });

    const error = await datos(api.POST('/api/solicitudes/{id}/cancelar', { params: { path: { id: 'x' } }, body: { motivo: 'm' } })).catch(
      (e: unknown) => e,
    );

    expect(error).toBeInstanceOf(ErrorApi);
    expect((error as ErrorApi).message).toBe('Ya no se puede cancelar.');
    expect((error as ErrorApi).estado).toBe(400);
  });

  it('ante el desafío de RFC 9470 pide reautenticar', async () => {
    const reautenticar = vi.fn();
    conectarSesion(() => 'token', reautenticar);
    apiFalsa({
      'PUT /api/parametros/calificacion.plazo_dias': () =>
        new Response(null, { status: 401, headers: { 'WWW-Authenticate': 'Bearer error="insufficient_user_authentication", max_age="300"' } }),
    });

    const error = (await datos(
      api.PUT('/api/parametros/{clave}', { params: { path: { clave: 'calificacion.plazo_dias' } }, body: { valor: 5 } }),
    ).catch((e: unknown) => e)) as ErrorApi;

    expect(reautenticar).toHaveBeenCalledOnce();
    expect(error.requiereReautenticacion).toBe(true);
  });

  it('un 403 dice que el rol no alcanza', async () => {
    apiFalsa({ 'GET /api/usuarios': () => new Response(null, { status: 403 }) });

    await expect(datos(api.GET('/api/usuarios'))).rejects.toThrow('Tu rol no puede hacer esta acción.');
  });
});
