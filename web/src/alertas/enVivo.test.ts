import { describe, expect, it, vi } from 'vitest';
import { type Alerta, escucharAlertas, leerEventos } from './enVivo';

const alerta = (secuencia: number): Alerta => ({
  id: `a-${secuencia}`,
  secuencia,
  tipo: 'solicitud.sin_candidatos',
  severidad: 'Critica',
  titulo: 'Sin candidatos',
  detalle: 'La solicitud no tiene conductor.',
  creadaEn: '2026-10-21T12:00:00Z',
  referenciaId: null,
  leidaEn: null,
});

function flujo(...partes: string[]): ReadableStream<Uint8Array> {
  const codificador = new TextEncoder();
  return new ReadableStream({
    start(controlador) {
      for (const parte of partes) controlador.enqueue(codificador.encode(parte));
      controlador.close();
    },
  });
}

describe('alertas en vivo (SSE)', () => {
  it('interpreta eventos aunque lleguen partidos en varios trozos', async () => {
    const recibidas: Alerta[] = [];
    const evento = (a: Alerta) => `event: alerta\r\nid: ${a.secuencia}\r\ndata: ${JSON.stringify(a)}\r\n\r\n`;
    const texto = evento(alerta(1)) + evento(alerta(2));

    await leerEventos(flujo(texto.slice(0, 40), texto.slice(40, 200), texto.slice(200)), (a) => recibidas.push(a));

    expect(recibidas.map((a) => a.secuencia)).toEqual([1, 2]);
  });

  it('manda el token y al reconectar retoma desde la última secuencia', async () => {
    const urls: string[] = [];
    let llamadas = 0;
    const fetchFalso = vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => {
      urls.push(String(url));
      expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer token-1');
      llamadas++;
      return new Response(llamadas === 1 ? flujo(`data: ${JSON.stringify(alerta(7))}\n\n`) : flujo());
    });
    const recibidas: number[] = [];

    const detener = escucharAlertas(() => 'token-1', (a) => recibidas.push(a.secuencia), { fetch: fetchFalso, esperaInicialMs: 1 });
    await vi.waitFor(() => expect(urls.length).toBeGreaterThanOrEqual(2));
    detener();

    expect(recibidas).toEqual([7]);
    expect(urls[0]).toContain('desde=0');
    expect(urls[1]).toContain('desde=7');
  });
});
