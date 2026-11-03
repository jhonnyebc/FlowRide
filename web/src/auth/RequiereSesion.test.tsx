import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiFalsa, renderizar } from '../pruebas/utilidades';
import { RequiereSesion } from './RequiereSesion';
import { useUsuario } from './usuario';

const auth = {
  isLoading: false,
  isAuthenticated: false,
  activeNavigator: undefined as string | undefined,
  error: undefined as Error | undefined,
  user: undefined as { access_token: string; profile: { sub: string } } | undefined,
  signinRedirect: vi.fn(),
  signoutRedirect: vi.fn(),
};
vi.mock('react-oidc-context', () => ({ useAuth: () => auth }));

function Hijo() {
  return <p>Hola, {useUsuario().nombre}</p>;
}

describe('RequiereSesion', () => {
  beforeEach(() => {
    auth.isAuthenticated = false;
    auth.user = undefined;
    auth.error = undefined;
    auth.signinRedirect.mockClear();
  });

  it('sin sesión abre el inicio de sesión de Keycloak y vuelve a la misma página', () => {
    apiFalsa({});
    renderizar(
      <RequiereSesion>
        <Hijo />
      </RequiereSesion>,
    );

    expect(screen.getByText('Abriendo el inicio de sesión…')).toBeInTheDocument();
    expect(auth.signinRedirect).toHaveBeenCalledWith({ state: '/' });
  });

  it('con sesión resuelve el usuario interno y deja ver el panel', async () => {
    auth.isAuthenticated = true;
    auth.user = { access_token: 'token', profile: { sub: 'sub-1' } };
    apiFalsa({ 'GET /api/usuarios/yo': () => ({ id: 'u-1', nombre: 'Ana Rojas', correo: null, rol: 'Administrador' }) });

    renderizar(
      <RequiereSesion>
        <Hijo />
      </RequiereSesion>,
    );

    expect(await screen.findByText('Hola, Ana Rojas')).toBeInTheDocument();
  });

  it('si FlowRide no reconoce al usuario (o está inactivo) no hay panel', async () => {
    auth.isAuthenticated = true;
    auth.user = { access_token: 'token', profile: { sub: 'sub-2' } };
    apiFalsa({ 'GET /api/usuarios/yo': () => new Response(null, { status: 401 }) });

    renderizar(
      <RequiereSesion>
        <Hijo />
      </RequiereSesion>,
    );

    expect(await screen.findByText('Tu usuario no tiene acceso al Panel Web de este operador.')).toBeInTheDocument();
    expect(screen.queryByText(/Hola/)).not.toBeInTheDocument();
  });
});
