import {
  Alert,
  Anchor,
  Avatar,
  Badge,
  Breadcrumbs,
  Button,
  Group,
  Paper,
  Skeleton,
  Stack,
  Text,
  ThemeIcon,
  Title,
  VisuallyHidden,
} from '@mantine/core';
import { IconAlertOctagon, IconCheck, IconCircleCheck, IconEye, type IconProps } from '@tabler/icons-react';
import type { UseQueryResult } from '@tanstack/react-query';
import type { ComponentType, ReactNode } from 'react';
import { Link } from 'react-router';
import { texto, textos } from '../textos';

type Icono = ComponentType<IconProps>;

export interface Miga {
  etiqueta: string;
  ruta?: string;
}

/** Encabezado de página: migas de pan, un h1 por página, una descripción opcional y acciones a la derecha. */
export function Encabezado({
  titulo,
  descripcion,
  migas,
  children,
}: {
  titulo: string;
  descripcion?: ReactNode;
  migas?: Miga[];
  children?: ReactNode;
}) {
  return (
    <Stack gap={4} mb="lg">
      {migas && (
        <nav aria-label="Ruta de navegación">
          <Breadcrumbs separatorMargin={6}>
            {migas.map((m) =>
              m.ruta ? (
                <Anchor key={m.etiqueta} component={Link} to={m.ruta} size="sm">
                  {m.etiqueta}
                </Anchor>
              ) : (
                <Text key={m.etiqueta} size="sm" c="dimmed" aria-current="page">
                  {m.etiqueta}
                </Text>
              ),
            )}
          </Breadcrumbs>
        </nav>
      )}
      <Group justify="space-between" align="flex-end" wrap="wrap" gap="sm">
        <div>
          <Title order={1} size="h2">
            {titulo}
          </Title>
          {descripcion && (
            <Text c="dimmed" size="sm" mt={2}>
              {descripcion}
            </Text>
          )}
        </div>
        {children && <Group gap="xs">{children}</Group>}
      </Group>
    </Stack>
  );
}

/** Esqueleto de carga genérico: bloques con la forma aproximada del contenido. */
export function Esqueleto({ filas = 3, alto = 72 }: { filas?: number; alto?: number }) {
  return (
    <Stack gap="sm">
      {Array.from({ length: filas }, (_, i) => (
        <Skeleton key={i} height={alto} radius="md" />
      ))}
    </Stack>
  );
}

/** Pinta la consulta: esqueleto mientras carga, error con reintento, o el contenido. */
export function Consulta<T>({
  consulta,
  children,
  esqueleto,
}: {
  consulta: UseQueryResult<T>;
  children: (datos: T) => ReactNode;
  esqueleto?: ReactNode;
}) {
  if (consulta.isPending)
    return (
      <div role="status">
        <VisuallyHidden>{textos.app.cargando}</VisuallyHidden>
        <div aria-hidden>{esqueleto ?? <Esqueleto />}</div>
      </div>
    );
  if (consulta.isError)
    return (
      <Alert color="red" title="No se pudo cargar" role="alert" icon={<IconAlertOctagon aria-hidden />}>
        <Group justify="space-between">
          <Text size="sm">{consulta.error.message}</Text>
          <Button size="xs" variant="light" onClick={() => void consulta.refetch()}>
            {textos.app.reintentar}
          </Button>
        </Group>
      </Alert>
    );
  return <>{children(consulta.data)}</>;
}

/** Sección gris niebla (sin borde ni sombra), con título, icono y acciones opcionales: la unidad visual de la página. */
export function Tarjeta({
  titulo,
  icono: Icono,
  acciones,
  children,
}: {
  titulo: string;
  icono?: Icono;
  acciones?: ReactNode;
  children: ReactNode;
}) {
  return (
    <Paper p={24} h="100%" component="section" aria-label={titulo} className="superficie">
      <Group justify="space-between" mb="md" wrap="nowrap" gap="xs">
        <Group gap="xs" wrap="nowrap">
          {Icono && (
            <ThemeIcon variant="filled" size="md" radius="md">
              <Icono size={16} aria-hidden />
            </ThemeIcon>
          )}
          <Title order={2} size="h5" fw={500}>
            {titulo}
          </Title>
        </Group>
        {acciones}
      </Group>
      {children}
    </Paper>
  );
}

/** Cuando no hay nada que mostrar: icono, título y una explicación (nunca una tabla vacía). */
export function EstadoVacio({
  icono: Icono = IconCircleCheck,
  titulo,
  descripcion,
  children,
}: {
  icono?: Icono;
  titulo: string;
  descripcion?: string;
  children?: ReactNode;
}) {
  return (
    <Stack align="flex-start" gap={6} py="md">
      <ThemeIcon variant="default" size={44} radius="md">
        <Icono size={26} aria-hidden />
      </ThemeIcon>
      <Text fw={500}>{titulo}</Text>
      {descripcion && (
        <Text size="sm" c="var(--fr-texto-suave)" maw={420}>
          {descripcion}
        </Text>
      )}
      {children}
    </Stack>
  );
}

/** Un dato con su etiqueta encima (fichas de detalle). */
export function Dato({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <Stack gap={2}>
      <Text size="xs" c="var(--fr-texto-suave)">
        {etiqueta}
      </Text>
      <div>{children}</div>
    </Stack>
  );
}

/** Una persona con su avatar de iniciales (negro en claro, blanco en oscuro). */
export function Persona({ nombre, detalle, tamano = 'md' }: { nombre: string; detalle?: ReactNode; tamano?: 'sm' | 'md' | 'lg' }) {
  return (
    <Group gap="sm" wrap="nowrap">
      <Avatar name={nombre} size={tamano} aria-hidden />
      <div style={{ minWidth: 0 }}>
        <Text fw={500} size={tamano === 'lg' ? 'lg' : 'sm'} truncate>
          {nombre}
        </Text>
        {detalle && (
          <Text size="xs" c="dimmed" component="div">
            {detalle}
          </Text>
        )}
      </div>
    </Group>
  );
}

/** La placa con aspecto de placa colombiana (amarilla con letras negras), igual en ambos temas. */
export function Placa({ placa }: { placa: string }) {
  return (
    <Badge
      variant="filled"
      radius="sm"
      size="lg"
      styles={{
        root: { background: '#f5c400', color: '#1a1a1a', border: '1px solid #1a1a1a', fontFamily: 'ui-monospace, monospace' },
      }}
    >
      <VisuallyHidden>Placa </VisuallyHidden>
      {placa}
    </Badge>
  );
}

/** Origen → destino como la columna de un formulario de viaje: círculo (recogida) y cuadro (destino) unidos por una línea; sin mapa ni GPS (ADR-0005). */
export function Recorrido({ origen, destino }: { origen: string | null | undefined; destino: string | null | undefined }) {
  const marca = { width: 10, height: 10, background: 'var(--mantine-color-text)', flexShrink: 0, marginTop: 5 };
  return (
    <Stack gap={0}>
      <Group gap="sm" wrap="nowrap" align="flex-start">
        <Stack gap={0} align="center" w={10}>
          <div aria-hidden style={{ ...marca, borderRadius: '50%' }} />
          <div aria-hidden style={{ width: 2, height: 30, background: 'var(--mantine-color-text)' }} />
        </Stack>
        <div>
          <Text size="xs" c="var(--fr-texto-suave)">
            Recogida
          </Text>
          <Text size="sm" fw={500}>
            {origen ?? 'Restringida'}
          </Text>
        </div>
      </Group>
      <Group gap="sm" wrap="nowrap" align="flex-start">
        <div aria-hidden style={marca} />
        <div>
          <Text size="xs" c="var(--fr-texto-suave)">
            Destino
          </Text>
          <Text size="sm" fw={500}>
            {destino ?? 'Restringida'}
          </Text>
        </div>
      </Group>
    </Stack>
  );
}

/**
 * Las etapas de un ciclo de vida (solicitud o viaje) como lista ordenada: hecha, actual y pendiente se distinguen por
 * icono y texto, no solo por color (OPLOG-ACC-02).
 */
export function LineaDeVida({ etapas, actual, etiqueta }: { etapas: string[]; actual: number; etiqueta: string }) {
  return (
    <Group component="ol" aria-label={etiqueta} gap={0} wrap="nowrap" p={0} m={0} style={{ listStyle: 'none', overflowX: 'auto' }}>
      {etapas.map((etapa, i) => {
        const hecha = i < actual;
        const enCurso = i === actual;
        return (
          <Group key={etapa} component="li" gap={6} wrap="nowrap" style={{ flex: i < etapas.length - 1 ? 1 : undefined }}>
            <ThemeIcon size={26} radius="xl" variant={hecha || enCurso ? 'filled' : 'default'}>
              {hecha ? (
                <IconCheck size={14} aria-hidden />
              ) : (
                <Text size="xs" fw={700} aria-hidden>
                  {i + 1}
                </Text>
              )}
            </ThemeIcon>
            <Text size="sm" fw={enCurso ? 600 : 400} c={hecha || enCurso ? undefined : 'var(--fr-texto-suave)'} style={{ whiteSpace: 'nowrap' }}>
              {etapa}
              <VisuallyHidden>{hecha ? ' (completada)' : enCurso ? ' (actual)' : ' (pendiente)'}</VisuallyHidden>
            </Text>
            {i < etapas.length - 1 && (
              <div
                aria-hidden
                style={{
                  flex: 1,
                  minWidth: 16,
                  height: 2,
                  marginInline: 8,
                  background: hecha ? 'var(--mantine-color-text)' : 'var(--fr-hairline)',
                }}
              />
            )}
          </Group>
        );
      })}
    </Group>
  );
}

const niveles: Record<string, { color: string; icono: typeof IconAlertOctagon }> = {
  Critico: { color: 'red', icono: IconAlertOctagon },
  Atencion: { color: 'yellow', icono: IconEye },
  Normal: { color: 'green', icono: IconCircleCheck },
};

/** Semáforo de un servicio (OPLOG-UX-02): color, icono y texto, nunca solo color (OPLOG-ACC-02). */
export function InsigniaNivel({ nivel }: { nivel: string }) {
  const { color, icono: Icono } = niveles[nivel] ?? { color: 'gray', icono: IconEye };
  return (
    <Badge color={color} variant={nivel === 'Critico' ? 'filled' : 'light'} leftSection={<Icono size={12} aria-hidden />}>
      {texto(textos.nivel, nivel)}
    </Badge>
  );
}

export const coloresSolicitud: Record<string, string> = {
  Pendiente: 'yellow',
  Asignada: 'blue',
  SinCandidatos: 'red',
  EnCurso: 'teal',
  Completada: 'green',
  Cancelada: 'gray',
};

export function InsigniaSolicitud({ estado }: { estado: string }) {
  return (
    <Badge color={coloresSolicitud[estado] ?? 'gray'} variant="light">
      {texto(textos.estadoSolicitud, estado)}
    </Badge>
  );
}

const coloresViaje: Record<string, string> = { Programado: 'blue', EnCurso: 'teal', Finalizado: 'green', Cancelado: 'gray' };

export function InsigniaViaje({ estado }: { estado: string }) {
  return (
    <Badge color={coloresViaje[estado] ?? 'gray'} variant="light">
      {texto(textos.estadoViaje, estado)}
    </Badge>
  );
}

const coloresAsignacion: Record<string, string> = {
  Asignada: 'yellow',
  Aceptada: 'teal',
  Rechazada: 'red',
  EnCurso: 'teal',
  Finalizada: 'green',
  Cancelada: 'gray',
  Reemplazada: 'gray',
};

export function InsigniaAsignacion({ estado }: { estado: string }) {
  return (
    <Badge color={coloresAsignacion[estado] ?? 'gray'} variant="light">
      {texto(textos.estadoAsignacion, estado)}
    </Badge>
  );
}
