import { ActionIcon, Alert, Badge, Button, Drawer, Group, Menu, Modal, Pagination, Stack, Text } from '@mantine/core';
import { IconDots } from '@tabler/icons-react';
import { useQuery, type UseMutationResult } from '@tanstack/react-query';
import type { FormEvent, ReactNode } from 'react';
import { api, datos } from '../api/cliente';
import { diasHasta, fechaCorta } from '../comun/fechas';
import { texto, textos } from '../textos';

// Piezas compartidas de las pantallas de administración (11b): lista con filtros y, a la derecha, un panel lateral
// para crear o editar sin salir de la lista (decisión del usuario, 2026-10-21).

export const TamanoPagina = 25;

export type FiltroActivo = 'activos' | 'inactivos' | 'todos';

/** Días antes del vencimiento en que un documento ya se marca como "vence pronto". */
export const DiasAvisoVencimiento = 30;

/** Los conductores activos, para elegir uno (son pocos por operador: una sola página basta). */
export function useConductoresActivos() {
  return useQuery({
    queryKey: ['conductores', 'activos'],
    queryFn: () => datos(api.GET('/api/conductores', { params: { query: { Estado: 'Activo', TamanoPagina: 200 } } })),
    select: (p) => p.elementos,
  });
}

/** Estado del conductor con texto (OPLOG-ACC-02): solo los activos reciben asignaciones. */
export function InsigniaConductor({ estado }: { estado: string }) {
  const color = estado === 'Activo' ? 'teal' : estado === 'Suspendido' ? 'orange' : 'gray';
  return (
    <Badge color={color} variant={estado === 'Inactivo' ? 'outline' : 'light'}>
      {texto(textos.estadoConductor, estado)}
    </Badge>
  );
}

/** Fecha de vencimiento con su estado en texto: vencido, vence pronto o vigente (OPLOG-ACC-02: no solo color). */
export function InsigniaVencimiento({ fecha }: { fecha: string | null | undefined }) {
  if (!fecha) return <>—</>;
  const dias = diasHasta(fecha);
  if (dias < 0)
    return (
      <Badge color="red" variant="light">
        Vencido · {fechaCorta(fecha)}
      </Badge>
    );
  if (dias <= DiasAvisoVencimiento)
    return (
      <Badge color="orange" variant="light">
        Vence pronto · {fechaCorta(fecha)}
      </Badge>
    );
  return <Text size="sm">{fechaCorta(fecha)}</Text>;
}

/** Panel lateral con un formulario: título, error de la API arriba y los botones abajo. */
export function PanelLateral({
  abierto,
  alCerrar,
  titulo,
  descripcion,
  mutacion,
  alEnviar,
  textoEnviar = 'Guardar',
  tamano = 'md',
  children,
}: {
  abierto: boolean;
  alCerrar: () => void;
  titulo: string;
  descripcion?: ReactNode;
  mutacion: Pick<UseMutationResult<unknown, Error, never>, 'isPending' | 'isError' | 'error'>;
  alEnviar: () => void;
  textoEnviar?: string;
  tamano?: 'md' | 'lg' | 'xl';
  children: ReactNode;
}) {
  return (
    <Drawer opened={abierto} onClose={alCerrar} title={titulo} position="right" size={tamano} padding="lg" closeButtonProps={{ 'aria-label': 'Cerrar' }}>
      <form
        onSubmit={(evento: FormEvent) => {
          evento.preventDefault();
          alEnviar();
        }}
      >
        <Stack gap="md">
          {descripcion && (
            <Text size="sm" c="var(--fr-texto-suave)">
              {descripcion}
            </Text>
          )}
          {mutacion.isError && (
            <Alert color="red" role="alert">
              {mutacion.error?.message}
            </Alert>
          )}
          {children}
          <Group justify="flex-end" mt="sm">
            <Button variant="default" onClick={alCerrar}>
              Cancelar
            </Button>
            <Button type="submit" loading={mutacion.isPending}>
              {textoEnviar}
            </Button>
          </Group>
        </Stack>
      </form>
    </Drawer>
  );
}

/** Confirmación de una acción que cambia el acceso o la disponibilidad (activar, desactivar, cambiar rol). */
export function Confirmacion({
  abierta,
  alCerrar,
  titulo,
  mensaje,
  textoConfirmar,
  peligrosa = false,
  mutacion,
  alConfirmar,
  children,
}: {
  abierta: boolean;
  alCerrar: () => void;
  titulo: string;
  mensaje: ReactNode;
  textoConfirmar: string;
  peligrosa?: boolean;
  mutacion: Pick<UseMutationResult<unknown, Error, never>, 'isPending' | 'isError' | 'error'>;
  alConfirmar: () => void;
  children?: ReactNode;
}) {
  return (
    <Modal opened={abierta} onClose={alCerrar} title={titulo}>
      <Stack>
        <Text size="sm">{mensaje}</Text>
        {children}
        {mutacion.isError && (
          <Alert color="red" role="alert">
            {mutacion.error?.message}
          </Alert>
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={alCerrar}>
            Volver
          </Button>
          <Button color={peligrosa ? 'red' : undefined} loading={mutacion.isPending} onClick={alConfirmar}>
            {textoConfirmar}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}

/** Activo o inactivo con texto, no solo color (OPLOG-ACC-02). */
export function InsigniaActivo({ activo, femenino = false }: { activo: boolean; femenino?: boolean }) {
  return activo ? (
    <Badge color="teal" variant="light">
      {femenino ? 'Activa' : 'Activo'}
    </Badge>
  ) : (
    <Badge color="gray" variant="outline">
      {femenino ? 'Inactiva' : 'Inactivo'}
    </Badge>
  );
}

export interface AccionFila {
  etiqueta: string;
  icono: ReactNode;
  alElegir: () => void;
  peligrosa?: boolean;
  deshabilitada?: boolean;
}

/** Menú "⋯" de una fila; su nombre dice de qué registro son las acciones. */
export function MenuAcciones({ de, acciones }: { de: string; acciones: AccionFila[] }) {
  return (
    <Menu position="bottom-end" withinPortal>
      <Menu.Target>
        <ActionIcon variant="subtle" color="gray" aria-label={`Acciones de ${de}`}>
          <IconDots size={18} aria-hidden />
        </ActionIcon>
      </Menu.Target>
      <Menu.Dropdown>
        {acciones.map((a) => (
          <Menu.Item key={a.etiqueta} leftSection={a.icono} color={a.peligrosa ? 'red' : undefined} disabled={a.deshabilitada} onClick={a.alElegir}>
            {a.etiqueta}
          </Menu.Item>
        ))}
      </Menu.Dropdown>
    </Menu>
  );
}

/** Total y paginación al pie de una lista. */
export function PiePagina({ total, pagina, alCambiar, unidad }: { total: number; pagina: number; alCambiar: (p: number) => void; unidad: string }) {
  return (
    <Group justify="space-between" mt="sm">
      <Text size="sm" c="var(--fr-texto-suave)">
        {total} {unidad}
      </Text>
      <Pagination
        total={Math.max(1, Math.ceil(total / TamanoPagina))}
        value={pagina}
        onChange={alCambiar}
        getControlProps={(control) => ({ 'aria-label': control === 'next' ? 'Página siguiente' : 'Página anterior' })}
      />
    </Group>
  );
}
