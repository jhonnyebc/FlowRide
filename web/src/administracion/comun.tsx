import { ActionIcon, Alert, Badge, Button, Drawer, Group, Menu, Modal, Pagination, Stack, Text } from '@mantine/core';
import { IconDots } from '@tabler/icons-react';
import type { UseMutationResult } from '@tanstack/react-query';
import type { FormEvent, ReactNode } from 'react';

// Piezas compartidas de las pantallas de administración (11b): lista con filtros y, a la derecha, un panel lateral
// para crear o editar sin salir de la lista (decisión del usuario, 2026-10-21).

export const TamanoPagina = 25;

/** Panel lateral con un formulario: título, error de la API arriba y los botones abajo. */
export function PanelLateral({
  abierto,
  alCerrar,
  titulo,
  descripcion,
  mutacion,
  alEnviar,
  textoEnviar = 'Guardar',
  children,
}: {
  abierto: boolean;
  alCerrar: () => void;
  titulo: string;
  descripcion?: ReactNode;
  mutacion: Pick<UseMutationResult<unknown, Error, never>, 'isPending' | 'isError' | 'error'>;
  alEnviar: () => void;
  textoEnviar?: string;
  children: ReactNode;
}) {
  return (
    <Drawer opened={abierto} onClose={alCerrar} title={titulo} position="right" size="md" padding="lg">
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
