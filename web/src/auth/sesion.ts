import { InMemoryWebStorage, WebStorageStateStore, type User } from 'oidc-client-ts';
import type { AuthProviderProps } from 'react-oidc-context';
import { autoridad, configuracion } from '../configuracion';

/**
 * OIDC Authorization Code + PKCE contra el realm del operador (ADR-0003, ADR-0019). Los tokens quedan solo en memoria:
 * al recargar la página se renuevan con la sesión de Keycloak, sin volver a pedir la contraseña.
 */
export function ajustesOidc(origen: string = window.location.origin): AuthProviderProps {
  return {
    authority: autoridad(),
    client_id: configuracion.clientePanel,
    redirect_uri: `${origen}/`,
    post_logout_redirect_uri: `${origen}/`,
    response_type: 'code',
    scope: 'openid',
    userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
    automaticSilentRenew: true,
    onSigninCallback: (usuario: User | void) => {
      const destino = typeof usuario?.state === 'string' ? usuario.state : '/';
      window.history.replaceState({}, document.title, destino);
    },
  };
}
