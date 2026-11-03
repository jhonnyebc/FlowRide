// Configuración pública del panel. Nada de esto es secreto: el panel es un cliente público de OIDC con PKCE (ADR-0003).

/**
 * Operador al que pertenece este panel: en producción, la primera etiqueta del subdominio (`tenant-a.flowride.co`,
 * ADR-0012); en local, `VITE_OPERADOR`.
 */
export function operadorActual(host: string = window.location.hostname): string {
  const desdeEntorno = import.meta.env.VITE_OPERADOR as string | undefined;
  if (desdeEntorno) return desdeEntorno;
  const [primera] = host.split('.');
  return primera ?? '';
}

export const configuracion = {
  urlKeycloak: (import.meta.env.VITE_KEYCLOAK_URL as string | undefined) ?? 'http://localhost:8080',
  clientePanel: 'flowride-panel',
  /** Ventana de reautenticación que pide la API para las acciones sensibles (RFC 9470), en segundos. */
  maxAgeAccionesSensibles: 300,
  /** La vista del día se actualiza sola cada 30 s (estado operacional: sin caché, ADR-0006). */
  refrescoVistaMs: 30_000,
};

/** Realm de Keycloak del operador: uno por operador (ADR-0003). */
export function autoridad(operador: string = operadorActual()): string {
  return `${configuracion.urlKeycloak.replace(/\/$/, '')}/realms/flowride-${operador}`;
}
