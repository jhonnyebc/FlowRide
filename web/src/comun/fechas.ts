// Fechas (requisito del profesor, 2026-10-20): un instante (algo que ya pasó o su UTC derivado) se muestra en la zona de
// quien mira; un evento futuro se muestra en su hora local con el nombre de su zona, tal como se acordó.

const formatoInstante = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' });
const formatoHora = new Intl.DateTimeFormat('es-CO', { hour: '2-digit', minute: '2-digit' });

/** Un instante UTC en la zona horaria del navegador. */
export function instante(valor: string | null | undefined): string {
  return valor ? formatoInstante.format(new Date(valor)) : '—';
}

/** Solo la hora de un instante, en la zona del navegador. */
export function hora(valor: string | null | undefined): string {
  return valor ? formatoHora.format(new Date(valor)) : '—';
}

/**
 * La hora local de un evento futuro (`2026-10-22T06:00:00`) con su zona, sin convertirla: es la hora acordada en ese
 * lugar. Ejemplo: `22 oct 2026, 6:00 a. m. (America/Bogota)`.
 */
export function horaLocal(local: string | null | undefined, zona: string | null | undefined): string {
  if (!local) return '—';
  const [fecha = '', tiempo = '00:00'] = local.split('T');
  const [anio, mes, dia] = fecha.split('-').map(Number);
  const [h, m] = tiempo.split(':').map(Number);
  // Se arma como UTC y se formatea en UTC para mostrar exactamente los números locales, sin conversión.
  const texto = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(
    new Date(Date.UTC(anio ?? 1970, (mes ?? 1) - 1, dia ?? 1, h ?? 0, m ?? 0)),
  );
  return zona ? `${texto} (${zona})` : texto;
}

/** `yyyy-MM-dd` de hoy en la zona del navegador (para el filtro de la vista del día). */
export function hoyIso(ahora: Date = new Date()): string {
  const local = new Date(ahora.getTime() - ahora.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}
