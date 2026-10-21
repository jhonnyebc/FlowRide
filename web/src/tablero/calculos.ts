import type { Esquemas } from '../api/cliente';

// Cálculos puros del tablero: se prueban sin pintar nada.

export type ClavePeriodo = 'hoy' | '7d' | '30d';

const diasPorPeriodo: Record<ClavePeriodo, number> = { hoy: 1, '7d': 7, '30d': 30 };

/** Rango [desde, hasta) del periodo en días completos de la zona del navegador, como instantes UTC para la API. */
export function rangoDelPeriodo(clave: ClavePeriodo, ahora: Date = new Date()): { desde: string; hasta: string } {
  const manana = new Date(ahora.getFullYear(), ahora.getMonth(), ahora.getDate() + 1);
  const desde = new Date(manana.getFullYear(), manana.getMonth(), manana.getDate() - diasPorPeriodo[clave]);
  return { desde: desde.toISOString(), hasta: manana.toISOString() };
}

/** Porcentaje 0–100 con un decimal, o null si no hay base (igual que la API: nunca un 0 % engañoso). */
export function porcentaje(parte: number, total: number): number | null {
  return total <= 0 ? null : Math.round((1000 * parte) / total) / 10;
}

export interface FilaPorHora {
  hora: string;
  Critico: number;
  Atencion: number;
  Normal: number;
}

/**
 * Servicios del día agrupados por hora de inicio (en la zona del navegador) y por semáforo, de la primera a la última
 * hora con servicios, sin huecos: así una hora tranquila también se ve.
 */
export function serviciosPorHora(servicios: Esquemas['ServicioVistaLectura'][]): FilaPorHora[] {
  if (servicios.length === 0) return [];
  const conteo = new Map<number, FilaPorHora>();
  const horas = servicios.map((s) => new Date(s.servicio.fechaHoraServicio).getHours());
  const primera = Math.min(...horas);
  const ultima = Math.max(...horas);
  for (let h = primera; h <= ultima; h++) conteo.set(h, { hora: `${String(h).padStart(2, '0')}:00`, Critico: 0, Atencion: 0, Normal: 0 });
  servicios.forEach((s, i) => {
    const fila = conteo.get(horas[i]!)!;
    fila[s.nivel] += 1;
  });
  return [...conteo.values()];
}

/** Saludo según la hora local. */
export function saludo(ahora: Date = new Date()): string {
  const h = ahora.getHours();
  return h < 12 ? 'Buenos días' : h < 19 ? 'Buenas tardes' : 'Buenas noches';
}
