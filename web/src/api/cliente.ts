import createClient, { type Middleware } from 'openapi-fetch';
import type { components, paths } from './esquema';

export type Esquemas = components['schemas'];

/** Error de la API con el mensaje para el usuario (ProblemDetails `detail`, o su `title`). */
export class ErrorApi extends Error {
  constructor(
    message: string,
    readonly estado: number,
    readonly requiereReautenticacion = false,
  ) {
    super(message);
    this.name = 'ErrorApi';
  }
}

// El token vive solo en memoria (nunca en cookies ni en localStorage): lo entrega la sesión OIDC al conectarse.
let obtenerToken: () => string | undefined = () => undefined;
let pedirReautenticacion: () => void = () => {};

export function conectarSesion(token: () => string | undefined, reautenticar: () => void): void {
  obtenerToken = token;
  pedirReautenticacion = reautenticar;
}

export function esDesafioDeReautenticacion(respuesta: Response): boolean {
  return respuesta.status === 401 && (respuesta.headers.get('WWW-Authenticate') ?? '').includes('insufficient_user_authentication');
}

const autenticacion: Middleware = {
  onRequest({ request }) {
    const token = obtenerToken();
    if (token) request.headers.set('Authorization', `Bearer ${token}`);
    return request;
  },
  onResponse({ response }) {
    // Acción sensible sin inicio de sesión reciente (RFC 9470): se vuelve a pedir la contraseña y el TOTP.
    if (esDesafioDeReautenticacion(response)) pedirReautenticacion();
    return response;
  },
};

export const api = createClient<paths>({
  baseUrl: typeof window === 'undefined' ? 'http://localhost' : window.location.origin,
  // Se resuelve en cada llamada (las pruebas reemplazan fetch).
  fetch: (peticion) => globalThis.fetch(peticion),
});
api.use(autenticacion);

interface RespuestaApi<T> {
  data?: T;
  error?: unknown;
  response: Response;
}

/** Devuelve los datos o lanza un ErrorApi con un mensaje entendible. */
export async function datos<T>(llamada: Promise<RespuestaApi<T>>): Promise<T> {
  const { data, error, response } = await llamada;
  if (response.ok) return data as T;
  const problema = (error ?? {}) as { detail?: string; title?: string };
  const mensaje =
    problema.detail ??
    problema.title ??
    (response.status === 403 ? 'Tu rol no puede hacer esta acción.' : `La API respondió ${response.status}.`);
  throw new ErrorApi(mensaje, response.status, esDesafioDeReautenticacion(response));
}

export function tokenActual(): string | undefined {
  return obtenerToken();
}
