import { notifications } from '@mantine/notifications';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { createContext, type ReactNode, useContext, useEffect } from 'react';
import { api, datos, tokenActual } from '../api/cliente';
import { textos } from '../textos';
import { type Alerta, escucharAlertas } from './enVivo';
import { IconoSeveridad, colorSeveridad } from './severidad';

const ContextoNoLeidas = createContext(0);

/** Alertas sin leer (para la campana de la cabecera). */
export function useAlertasNoLeidas(): number {
  return useContext(ContextoNoLeidas);
}

export const claveAlertas = ['alertas'] as const;

/**
 * Conecta las alertas en vivo (SSE) mientras hay sesión: cada alerta nueva se anuncia (color, icono y texto,
 * OPLOG-ACC-02) y refresca las listas. El total sin leer sale de la API.
 */
export function ProveedorAlertas({ children }: { children: ReactNode }) {
  const cliente = useQueryClient();
  const noLeidas = useQuery({
    queryKey: [...claveAlertas, 'no-leidas'],
    queryFn: () => datos(api.GET('/api/alertas', { params: { query: { SoloNoLeidas: true, TamanoPagina: 1 } } })),
    select: (pagina) => pagina.total,
  });

  useEffect(
    () =>
      escucharAlertas(tokenActual, (alerta: Alerta) => {
        notifications.show({
          id: alerta.id,
          title: `${textos.severidad[alerta.severidad] ?? alerta.severidad}: ${alerta.titulo}`,
          message: alerta.detalle,
          color: colorSeveridad(alerta.severidad),
          icon: <IconoSeveridad severidad={alerta.severidad} />,
          autoClose: alerta.severidad === 'Critica' ? false : 8_000,
        });
        void cliente.invalidateQueries({ queryKey: claveAlertas });
      }),
    [cliente],
  );

  return <ContextoNoLeidas.Provider value={noLeidas.data ?? 0}>{children}</ContextoNoLeidas.Provider>;
}
