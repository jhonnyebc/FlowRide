import { createContext, useContext } from 'react';
import type { Esquemas } from '../api/cliente';

export type UsuarioActual = Esquemas['UsuarioAutenticadoRespuesta'];

export const ContextoUsuario = createContext<UsuarioActual | null>(null);

/** El usuario interno de FlowRide detrás del token (su rol lo decide FlowRide, no Keycloak). */
export function useUsuario(): UsuarioActual {
  const usuario = useContext(ContextoUsuario);
  if (!usuario) throw new Error('useUsuario se usa dentro de RequiereSesion.');
  return usuario;
}

export const esOperador = (usuario: UsuarioActual) => usuario.rol === 'OperadorLogistico';
export const esAdministrador = (usuario: UsuarioActual) => usuario.rol === 'Administrador';
