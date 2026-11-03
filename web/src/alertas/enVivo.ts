import type { Esquemas } from '../api/cliente';

export type Alerta = Esquemas['AlertaLectura'];

/**
 * Lee las alertas en vivo (Server-Sent Events, ADR-0015). Se usa fetch y no EventSource porque EventSource no puede
 * enviar el token en la cabecera Authorization. Reconecta con espera creciente y retoma desde la última secuencia.
 */
export function escucharAlertas(
  token: () => string | undefined,
  alRecibir: (alerta: Alerta) => void,
  opciones: { fetch?: typeof fetch; esperaInicialMs?: number } = {},
): () => void {
  const controlador = new AbortController();
  const traer = opciones.fetch ?? ((url, init) => globalThis.fetch(url, init));
  let ultima = 0;
  let espera = opciones.esperaInicialMs ?? 1_000;

  const conectar = async (): Promise<void> => {
    while (!controlador.signal.aborted) {
      try {
        const respuesta = await traer(`/api/alertas/en-vivo?desde=${ultima}`, {
          headers: { Accept: 'text/event-stream', Authorization: `Bearer ${token() ?? ''}` },
          signal: controlador.signal,
        });
        if (!respuesta.ok || !respuesta.body) throw new Error(`en-vivo ${respuesta.status}`);
        espera = opciones.esperaInicialMs ?? 1_000;
        await leerEventos(respuesta.body, (alerta) => {
          ultima = Math.max(ultima, alerta.secuencia);
          alRecibir(alerta);
        });
      } catch {
        if (controlador.signal.aborted) return;
      }
      await new Promise((resolver) => setTimeout(resolver, espera));
      espera = Math.min(espera * 2, 30_000);
    }
  };
  void conectar();
  return () => controlador.abort();
}

/** Interpreta el flujo SSE: bloques separados por línea en blanco, con `event:` y `data:`. */
export async function leerEventos(cuerpo: ReadableStream<Uint8Array>, alRecibir: (alerta: Alerta) => void): Promise<void> {
  const lector = cuerpo.getReader();
  const decodificador = new TextDecoder();
  let pendiente = '';
  for (;;) {
    const { value, done } = await lector.read();
    if (done) return;
    pendiente += decodificador.decode(value, { stream: true }).replaceAll('\r\n', '\n');
    let fin = pendiente.indexOf('\n\n');
    while (fin >= 0) {
      const bloque = pendiente.slice(0, fin);
      pendiente = pendiente.slice(fin + 2);
      const datos = bloque
        .split('\n')
        .filter((linea) => linea.startsWith('data:'))
        .map((linea) => linea.slice(5).trimStart())
        .join('\n');
      if (datos) alRecibir(JSON.parse(datos) as Alerta);
      fin = pendiente.indexOf('\n\n');
    }
  }
}
