import { queryOptions } from '@tanstack/react-query';
import { configuracion } from '../configuracion';
import { api, datos } from './cliente';

// Consultas que comparten varias pantallas (el marco, el tablero y la página propia): la misma clave hace que TanStack
// Query pida una sola vez y todas vean el mismo dato. Es estado operacional: siempre viene de la API (ADR-0006).

/** Servicios de un día con su semáforo (RF-36). */
export const vistaDelDia = (fecha: string) =>
  queryOptions({
    queryKey: ['vista-del-dia', fecha],
    queryFn: () => datos(api.GET('/api/reportes/vista-centralizada', { params: { query: { Fecha: fecha } } })),
    refetchInterval: configuracion.refrescoVistaMs,
  });

/** Viajes en curso y los que empiezan en las próximas horas (RF-37). */
export const monitoreo = () =>
  queryOptions({
    queryKey: ['viajes', 'monitoreo'],
    queryFn: () => datos(api.GET('/api/viajes/monitoreo')),
    refetchInterval: configuracion.refrescoVistaMs,
  });

/** Indicadores de un periodo (RF-38): volumen, cumplimiento, conductores y empresas. */
export const indicadores = (desde: string, hasta: string) =>
  queryOptions({
    queryKey: ['indicadores', desde, hasta],
    queryFn: () => datos(api.GET('/api/reportes/indicadores', { params: { query: { Desde: desde, Hasta: hasta } } })),
    refetchInterval: configuracion.refrescoVistaMs,
  });
