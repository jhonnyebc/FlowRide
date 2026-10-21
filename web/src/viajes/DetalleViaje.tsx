import { Alert, Badge, Button, Grid, Group, Modal, Paper, Select, SimpleGrid, Stack, Text, Textarea, ThemeIcon, Timeline } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import {
  IconAlertTriangle,
  IconCar,
  IconClock,
  IconFileDescription,
  IconFlag,
  IconMapPin,
  IconMapPins,
  IconPlayerPlay,
  IconStar,
  IconStarFilled,
  IconUser,
} from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { type FormEvent, useState } from 'react';
import { Link, useParams } from 'react-router';
import { api, datos, type Esquemas } from '../api/cliente';
import { esOperador, useUsuario } from '../auth/usuario';
import { Consulta, Dato, Encabezado, EstadoVacio, InsigniaViaje, LineaDeVida, Persona, Placa, Recorrido, Tarjeta } from '../comun/Componentes';
import { instante } from '../comun/fechas';
import { texto, textos } from '../textos';

const tiposIncidente = Object.entries(textos.tipoIncidente).map(([value, label]) => ({ value, label }));
const etapas = ['Programado', 'En curso', 'Finalizado'];
const etapaDe: Record<string, number> = { Programado: 0, EnCurso: 1, Finalizado: 3 };

/** Minutos entre lo programado y lo real (positivo = tarde), o null si aún no pasó. */
function minutosDeDiferencia(programado: string, real: string | null | undefined): number | null {
  return real ? Math.round((new Date(real).getTime() - new Date(programado).getTime()) / 60_000) : null;
}

/** Un viaje con su conductor, horario, paradas, incidentes y calificación (RF-34, RF-37). Las acciones son del Operador logístico. */
export function DetalleViaje() {
  const { id = '' } = useParams();
  const usuario = useUsuario();
  const cliente = useQueryClient();
  const [modal, { open, close }] = useDisclosure();
  const [tipo, setTipo] = useState<string | null>('Retraso');
  const [descripcion, setDescripcion] = useState('');

  const detalle = useQuery({ queryKey: ['viaje', id], queryFn: () => datos(api.GET('/api/viajes/{id}', { params: { path: { id } } })) });
  const refrescar = () => {
    void cliente.invalidateQueries({ queryKey: ['viaje', id] });
    void cliente.invalidateQueries({ queryKey: ['viajes'] });
  };
  const accion = useMutation({
    mutationFn: (cual: 'iniciar' | 'finalizar') =>
      cual === 'iniciar'
        ? datos(api.POST('/api/viajes/{id}/iniciar', { params: { path: { id } } }))
        : datos(api.POST('/api/viajes/{id}/finalizar', { params: { path: { id } } })),
    onSuccess: (_, cual) => {
      notifications.show({ color: 'green', message: cual === 'iniciar' ? 'Viaje iniciado.' : 'Viaje finalizado.' });
      refrescar();
    },
    onError: (e) => notifications.show({ color: 'red', message: e.message }),
  });
  const incidente = useMutation({
    mutationFn: () => datos(api.POST('/api/viajes/{id}/incidentes', { params: { path: { id } }, body: { tipo, descripcion } })),
    onSuccess: () => {
      notifications.show({ color: 'green', message: 'Incidente reportado.' });
      close();
      setDescripcion('');
      refrescar();
    },
  });
  const puedeActuar = esOperador(usuario);

  return (
    <Consulta consulta={detalle}>
      {({ viaje: v, paradas, incidentes, calificacion }) => {
        const retrasoInicio = minutosDeDiferencia(v.inicioProgramado, v.fechaHoraInicio);
        return (
          <>
            <Encabezado
              titulo={`Viaje de ${v.pasajeroNombre}`}
              descripcion={`Programado para ${instante(v.inicioProgramado)}`}
              migas={[{ etiqueta: textos.menu.viajesEnCurso, ruta: '/viajes' }, { etiqueta: v.pasajeroNombre }]}
            >
              <Button component={Link} to={`/solicitudes/${v.solicitudId}`} variant="default" leftSection={<IconFileDescription size={16} aria-hidden />}>
                Ver la solicitud
              </Button>
              {puedeActuar && v.estado === 'Programado' && (
                <Button leftSection={<IconPlayerPlay size={16} aria-hidden />} loading={accion.isPending} onClick={() => accion.mutate('iniciar')}>
                  Iniciar
                </Button>
              )}
              {puedeActuar && v.estado === 'EnCurso' && (
                <Button leftSection={<IconFlag size={16} aria-hidden />} loading={accion.isPending} onClick={() => accion.mutate('finalizar')}>
                  Finalizar
                </Button>
              )}
              {puedeActuar && (v.estado === 'Programado' || v.estado === 'EnCurso') && (
                <Button variant="light" color="orange" leftSection={<IconAlertTriangle size={16} aria-hidden />} onClick={open}>
                  Reportar incidente
                </Button>
              )}
            </Encabezado>

            <Paper p={24} className="superficie" mb="md">
              <Stack gap="md">
                <Group justify="space-between" wrap="wrap" gap="sm">
                  <Group gap={6}>
                    <InsigniaViaje estado={v.estado} />
                    {v.urgente && <Badge color="red">Urgente</Badge>}
                    {v.esPersonal && <Badge variant="outline">Personal</Badge>}
                    {v.incidentes > 0 && (
                      <Badge color="orange" variant="light" leftSection={<IconAlertTriangle size={12} aria-hidden />}>
                        {v.incidentes} incidente(s)
                      </Badge>
                    )}
                  </Group>
                  <Puntualidad minutos={retrasoInicio} />
                </Group>
                {v.estado === 'Cancelado' ? (
                  <Alert color="gray" title="Viaje cancelado">
                    Este viaje se canceló antes de terminar.
                  </Alert>
                ) : (
                  <LineaDeVida etapas={etapas} actual={etapaDe[v.estado] ?? 0} etiqueta="Avance del viaje" />
                )}
              </Stack>
            </Paper>

            <Grid gutter="md">
              <Grid.Col span={{ base: 12, md: 6, lg: 4 }}>
                <Tarjeta titulo="Conductor y vehículo" icono={IconCar}>
                  <Stack gap="sm">
                    <Persona nombre={v.conductorNombre} detalle="Conductor" />
                    <Group>
                      <Placa placa={v.placa} />
                    </Group>
                  </Stack>
                </Tarjeta>
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 6, lg: 4 }}>
                <Tarjeta titulo="Pasajero y recorrido" icono={IconUser}>
                  <Stack gap="sm">
                    <Persona nombre={v.pasajeroNombre} detalle={v.empresaClienteNombre ?? 'Sin empresa'} />
                    <Recorrido origen={v.direccionOrigen} destino={v.direccionDestino} />
                  </Stack>
                </Tarjeta>
              </Grid.Col>
              <Grid.Col span={{ base: 12, lg: 4 }}>
                <Tarjeta titulo="Horario" icono={IconClock}>
                  <SimpleGrid cols={2} spacing="sm">
                    <Dato etiqueta="Inicio programado">{instante(v.inicioProgramado)}</Dato>
                    <Dato etiqueta="Inicio real">{instante(v.fechaHoraInicio)}</Dato>
                    <Dato etiqueta="Fin programado">{instante(v.finProgramado)}</Dato>
                    <Dato etiqueta="Fin real">{instante(v.fechaHoraFin)}</Dato>
                  </SimpleGrid>
                </Tarjeta>
              </Grid.Col>

              <Grid.Col span={{ base: 12, md: 6 }}>
                <Tarjeta titulo="Paradas" icono={IconMapPins}>
                  <Paradas paradas={paradas} />
                </Tarjeta>
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <Stack gap="md">
                  <Tarjeta titulo="Incidentes" icono={IconAlertTriangle}>
                    <Incidentes incidentes={incidentes} />
                  </Tarjeta>
                  <Tarjeta titulo="Calificación del pasajero" icono={IconStar}>
                    <Calificacion calificacion={calificacion} finalizado={v.estado === 'Finalizado'} />
                  </Tarjeta>
                </Stack>
              </Grid.Col>
            </Grid>

            <Modal opened={modal} onClose={close} title="Reportar incidente">
              <form
                onSubmit={(e: FormEvent) => {
                  e.preventDefault();
                  incidente.mutate();
                }}
              >
                <Stack>
                  {incidente.isError && (
                    <Alert color="red" role="alert">
                      {incidente.error.message}
                    </Alert>
                  )}
                  <Select label="Tipo" data={tiposIncidente} value={tipo} onChange={setTipo} required allowDeselect={false} />
                  <Textarea label="Descripción" required value={descripcion} onChange={(e) => setDescripcion(e.currentTarget.value)} autosize minRows={2} />
                  <Group justify="flex-end">
                    <Button variant="default" onClick={close}>
                      Volver
                    </Button>
                    <Button type="submit" loading={incidente.isPending}>
                      Reportar
                    </Button>
                  </Group>
                </Stack>
              </form>
            </Modal>
          </>
        );
      }}
    </Consulta>
  );
}

function Puntualidad({ minutos }: { minutos: number | null }) {
  if (minutos === null) return null;
  if (minutos <= 0)
    return (
      <Badge color="teal" variant="light" size="lg">
        Inició a tiempo
      </Badge>
    );
  return (
    <Badge color={minutos > 15 ? 'red' : 'yellow'} variant="light" size="lg">
      Inició {minutos} min tarde
    </Badge>
  );
}

function Paradas({ paradas }: { paradas: Esquemas['ParadaLectura'][] }) {
  if (paradas.length === 0) return <EstadoVacio icono={IconMapPins} titulo="Sin paradas" descripcion="Este viaje va directo del origen al destino." />;
  const ordenadas = [...paradas].sort((a, b) => a.orden - b.orden);
  const alcanzadas = ordenadas.filter((p) => p.horaReal).length;
  return (
    <Timeline bulletSize={26} lineWidth={2} active={alcanzadas - 1}>
      {ordenadas.map((p) => {
        const diferencia = minutosDeDiferencia(p.horaEstimada, p.horaReal);
        return (
          <Timeline.Item
            key={p.id}
            bullet={
              <ThemeIcon size={26} radius="xl" variant={p.horaReal ? 'filled' : 'light'} color={p.horaReal ? 'teal' : 'gray'}>
                <IconMapPin size={14} aria-hidden />
              </ThemeIcon>
            }
            title={
              <Text size="sm" fw={600}>
                {p.orden}. {p.direccion ?? 'Restringida'}
              </Text>
            }
          >
            <Text size="xs" c="dimmed">
              Estimada: {instante(p.horaEstimada)}
            </Text>
            <Text size="xs">
              {p.horaReal ? `Llegó: ${instante(p.horaReal)}${diferencia && diferencia > 0 ? ` (${diferencia} min tarde)` : ''}` : 'Aún no llega'}
            </Text>
          </Timeline.Item>
        );
      })}
    </Timeline>
  );
}

function Incidentes({ incidentes }: { incidentes: Esquemas['IncidenteLectura'][] }) {
  if (incidentes.length === 0) return <EstadoVacio titulo="Sin incidentes" descripcion="Nada que reportar en este viaje." />;
  return (
    <Stack gap="xs">
      {incidentes.map((i) => (
        <Paper key={i.id} withBorder radius="md" p="sm">
          <Group gap="sm" wrap="nowrap" align="flex-start">
            <ThemeIcon color={i.tipo === 'Accidente' ? 'red' : 'orange'} variant="light" radius="xl">
              <IconAlertTriangle size={16} aria-hidden />
            </ThemeIcon>
            <div>
              <Text size="sm" fw={600}>
                {texto(textos.tipoIncidente, i.tipo)}
              </Text>
              <Text size="sm">{i.descripcion}</Text>
              <Text size="xs" c="dimmed">
                {instante(i.fechaHora)}
              </Text>
            </div>
          </Group>
        </Paper>
      ))}
    </Stack>
  );
}

function Calificacion({ calificacion, finalizado }: { calificacion: Esquemas['CalificacionLectura'] | null; finalizado: boolean }) {
  if (!calificacion)
    return (
      <Text size="sm" c="dimmed">
        {finalizado ? 'El pasajero aún no ha calificado este viaje.' : 'El pasajero podrá calificar cuando el viaje termine.'}
      </Text>
    );
  return (
    <Stack gap={6}>
      <Group gap={4} aria-label={`${calificacion.puntaje} de 5 estrellas`} role="img">
        {[1, 2, 3, 4, 5].map((n) =>
          n <= calificacion.puntaje ? (
            <IconStarFilled key={n} size={22} color="var(--mantine-color-yellow-5)" aria-hidden />
          ) : (
            <IconStar key={n} size={22} color="var(--mantine-color-gray-5)" aria-hidden />
          ),
        )}
        <Text fw={700} ml={6} aria-hidden>
          {calificacion.puntaje}/5
        </Text>
      </Group>
      {calificacion.comentario && <Text size="sm">«{calificacion.comentario}»</Text>}
      <Text size="xs" c="dimmed">
        {instante(calificacion.fechaHora)}
      </Text>
    </Stack>
  );
}
