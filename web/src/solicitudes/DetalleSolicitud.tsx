import {
  Accordion,
  Alert,
  Badge,
  Button,
  Divider,
  Grid,
  Group,
  Modal,
  Paper,
  SimpleGrid,
  Stack,
  Table,
  Tabs,
  Text,
  Textarea,
  ThemeIcon,
  Timeline,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import {
  IconAlertTriangle,
  IconArrowBackUp,
  IconBolt,
  IconCheck,
  IconCpu,
  IconFileDescription,
  IconHistory,
  IconListCheck,
  IconMapPin,
  IconRoute,
  IconStar,
  IconSteeringWheel,
  IconUser,
  IconUserX,
  IconX,
  type IconProps,
} from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { type ComponentType, type FormEvent, useState } from 'react';
import { Link, useParams } from 'react-router';
import { api, datos, type Esquemas } from '../api/cliente';
import { esOperador, useUsuario } from '../auth/usuario';
import {
  Consulta,
  Dato,
  Encabezado,
  EstadoVacio,
  InsigniaAsignacion,
  InsigniaSolicitud,
  LineaDeVida,
  Persona,
  Placa,
  Recorrido,
  Tarjeta,
} from '../comun/Componentes';
import { horaLocal, instante } from '../comun/fechas';
import { texto, textos } from '../textos';

const sinCerrar = new Set(['Pendiente', 'Asignada', 'SinCandidatos']);
const etapas = ['Registrada', 'Asignada', 'En curso', 'Completada'];
const etapaDe: Record<string, number> = { Pendiente: 1, SinCandidatos: 1, Asignada: 2, EnCurso: 3, Completada: 4 };

type Accion = 'cancelar' | 'devolver' | 'aceptar' | 'rechazar';

const accionTextos: Record<Accion, { titulo: string; exito: string; conMotivo: boolean; aviso?: string }> = {
  cancelar: { titulo: 'Cancelar la solicitud', exito: 'Solicitud cancelada.', conMotivo: true },
  devolver: { titulo: 'Devolver a la cola', exito: 'La solicitud volvió a la cola.', conMotivo: true },
  aceptar: {
    titulo: 'Aceptar en nombre del conductor',
    exito: 'Asignación aceptada: se avisará al pasajero por WhatsApp.',
    conMotivo: false,
    aviso: 'Registra que el conductor aceptó el servicio (por ejemplo, porque lo confirmó por teléfono). Se crea el viaje y el pasajero recibe por WhatsApp el nombre del conductor y la placa.',
  },
  rechazar: {
    titulo: 'Rechazar en nombre del conductor',
    exito: 'Asignación rechazada: el motor buscará otro conductor.',
    conMotivo: true,
    aviso: 'La asignación se cierra y la solicitud vuelve a buscar conductor.',
  },
};

/**
 * Una solicitud de punta a punta (RF-30, RF-40): ficha, conductor asignado, decisiones del motor y trazabilidad, con las
 * acciones que permiten su estado y tu rol. Aceptar o rechazar en nombre del conductor es del Operador logístico.
 */
export function DetalleSolicitud() {
  const { id = '' } = useParams();
  const usuario = useUsuario();
  const cliente = useQueryClient();
  const [motivo, setMotivo] = useState('');
  const [accion, setAccion] = useState<Accion | null>(null);
  const [modal, { open, close }] = useDisclosure();

  const detalle = useQuery({ queryKey: ['solicitud', id], queryFn: () => datos(api.GET('/api/solicitudes/{id}', { params: { path: { id } } })) });
  const traza = useQuery({
    queryKey: ['solicitud', id, 'trazabilidad'],
    queryFn: () => datos(api.GET('/api/solicitudes/{id}/trazabilidad', { params: { path: { id } } })),
  });
  const asignaciones = useQuery({
    queryKey: ['solicitud', id, 'asignaciones'],
    queryFn: () => datos(api.GET('/api/asignaciones', { params: { query: { SolicitudId: id, TamanoPagina: 50 } } })),
  });
  const vigente = asignacionVigente(asignaciones.data?.elementos ?? []);
  const refrescar = () => {
    void cliente.invalidateQueries({ queryKey: ['solicitud', id] });
    void cliente.invalidateQueries({ queryKey: ['vista-del-dia'] });
  };

  const asignar = useMutation({
    mutationFn: () => datos(api.POST('/api/solicitudes/{id}/asignar', { params: { path: { id } } })),
    onSuccess: (r) => {
      notifications.show({ color: r.asignacionId ? 'green' : 'orange', title: 'Motor de asignación', message: r.motivo });
      refrescar();
    },
    onError: (e) => notifications.show({ color: 'red', message: e.message }),
  });
  const ejecutar = useMutation({
    mutationFn: async (cual: Accion) => {
      const asignacion = vigente?.id ?? '';
      switch (cual) {
        case 'cancelar':
          return datos(api.POST('/api/solicitudes/{id}/cancelar', { params: { path: { id } }, body: { motivo } }));
        case 'devolver':
          return datos(api.POST('/api/solicitudes/{id}/devolver-a-cola', { params: { path: { id } }, body: { motivo } }));
        case 'aceptar':
          return datos(api.POST('/api/asignaciones/{id}/aceptar', { params: { path: { id: asignacion } } }));
        case 'rechazar':
          return datos(api.POST('/api/asignaciones/{id}/rechazar', { params: { path: { id: asignacion } }, body: { motivo } }));
      }
    },
    onSuccess: (_, cual) => {
      notifications.show({ color: 'green', message: accionTextos[cual].exito });
      close();
      setMotivo('');
      refrescar();
    },
  });

  const pedir = (cual: Accion) => {
    setAccion(cual);
    ejecutar.reset();
    open();
  };
  const operador = esOperador(usuario);

  return (
    <Consulta consulta={detalle}>
      {({ solicitud: s, historial }) => (
        <>
          <Encabezado
            titulo={`Solicitud de ${s.pasajeroNombre}`}
            descripcion={`Servicio del ${horaLocal(s.horaLocalServicio, s.zonaHoraria)}`}
            migas={[{ etiqueta: textos.menu.solicitudes, ruta: '/solicitudes' }, { etiqueta: s.pasajeroNombre }]}
          >
            {s.estado === 'Pendiente' && (
              <Button leftSection={<IconBolt size={16} aria-hidden />} loading={asignar.isPending} onClick={() => asignar.mutate()}>
                Asignar ya
              </Button>
            )}
            {s.estado === 'Asignada' && operador && (
              <Button variant="default" leftSection={<IconArrowBackUp size={16} aria-hidden />} onClick={() => pedir('devolver')}>
                Devolver a la cola
              </Button>
            )}
            {sinCerrar.has(s.estado) && (
              <Button color="red" variant="light" leftSection={<IconX size={16} aria-hidden />} onClick={() => pedir('cancelar')}>
                Cancelar
              </Button>
            )}
          </Encabezado>

          <Paper p={24} className="superficie" mb="md">
            <Stack gap="md">
              <Group justify="space-between" wrap="wrap" gap="sm">
                <Persona
                  nombre={s.pasajeroNombre}
                  tamano="lg"
                  detalle={s.empresaClienteNombre ?? (s.esPersonal ? 'Servicio personal' : 'Sin empresa')}
                />
                <Group gap={6}>
                  <InsigniaSolicitud estado={s.estado} />
                  {s.urgente && (
                    <Badge color="red" leftSection={<IconAlertTriangle size={12} aria-hidden />}>
                      Urgente
                    </Badge>
                  )}
                  {s.esPersonal && <Badge variant="outline">Personal</Badge>}
                  <Badge variant="default">{texto(textos.canal, s.canal)}</Badge>
                </Group>
              </Group>
              {s.estado === 'Cancelada' ? (
                <Alert color="gray" icon={<IconX aria-hidden />} title="Solicitud cancelada">
                  {historial.at(-1)?.motivo ?? 'Sin motivo registrado.'}
                </Alert>
              ) : (
                <LineaDeVida etapas={etapas} actual={etapaDe[s.estado] ?? 0} etiqueta="Avance de la solicitud" />
              )}
              {s.estado === 'SinCandidatos' && (
                <Alert color="red" icon={<IconUserX aria-hidden />} title="Sin conductor disponible">
                  El motor no encontró un conductor elegible. Revisa las decisiones del motor abajo para ver por qué se descartó a cada uno.
                </Alert>
              )}
            </Stack>
          </Paper>

          <Grid gutter="md">
            <Grid.Col span={{ base: 12, md: 7, lg: 8 }}>
              <Stack gap="md">
                <Tarjeta titulo="Servicio" icono={IconMapPin}>
                  <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="lg">
                    <Recorrido origen={s.direccionOrigen} destino={s.direccionDestino} />
                    <SimpleGrid cols={2} spacing="sm">
                      <Dato etiqueta="Hora local">{horaLocal(s.horaLocalServicio, null)}</Dato>
                      <Dato etiqueta="Zona horaria">{s.zonaHoraria}</Dato>
                      <Dato etiqueta="Duración">{s.duracionMinutos} min</Dato>
                      <Dato etiqueta="Pasajeros">{s.cantidadPasajeros}</Dato>
                      <Dato etiqueta="Registrada">{instante(s.registradaEn)}</Dato>
                      <Dato etiqueta="Itinerario">{s.itinerarioId ? 'Recurrente' : 'Único'}</Dato>
                    </SimpleGrid>
                  </SimpleGrid>
                  {s.detalleRestringido && (
                    <Text size="sm" c="dimmed" mt="sm">
                      Es una solicitud personal: sus direcciones no son visibles para tu rol.
                    </Text>
                  )}
                </Tarjeta>

                <Paper p={24} className="superficie">
                  <Tabs defaultValue="trazabilidad" keepMounted={false}>
                    <Tabs.List mb="md">
                      <Tabs.Tab value="trazabilidad" leftSection={<IconRoute size={16} aria-hidden />}>
                        Trazabilidad
                      </Tabs.Tab>
                      <Tabs.Tab value="motor" leftSection={<IconCpu size={16} aria-hidden />}>
                        Decisiones del motor
                      </Tabs.Tab>
                      <Tabs.Tab value="historial" leftSection={<IconHistory size={16} aria-hidden />}>
                        Estados ({historial.length})
                      </Tabs.Tab>
                    </Tabs.List>
                    <Tabs.Panel value="trazabilidad">
                      <Consulta consulta={traza}>{(t) => <Trazabilidad eventos={t.eventos} />}</Consulta>
                    </Tabs.Panel>
                    <Tabs.Panel value="motor">
                      <Decisiones solicitudId={id} />
                    </Tabs.Panel>
                    <Tabs.Panel value="historial">
                      <Historial historial={historial} />
                    </Tabs.Panel>
                  </Tabs>
                </Paper>
              </Stack>
            </Grid.Col>

            <Grid.Col span={{ base: 12, md: 5, lg: 4 }}>
              <Tarjeta titulo="Conductor asignado" icono={IconSteeringWheel}>
                <Consulta consulta={asignaciones}>
                  {(pagina) =>
                    vigente ? (
                      <Asignacion
                        asignacion={vigente}
                        anteriores={pagina.elementos.length - 1}
                        solicitud={s}
                        puedeResponder={operador && vigente.estado === 'Asignada'}
                        onResponder={pedir}
                      />
                    ) : (
                      <EstadoVacio
                        icono={IconUser}
                        titulo="Aún sin conductor"
                        descripcion={
                          s.estado === 'Pendiente'
                            ? 'Usa «Asignar ya» para que el motor busque un conductor ahora.'
                            : 'Esta solicitud no tiene una asignación vigente.'
                        }
                      />
                    )
                  }
                </Consulta>
              </Tarjeta>
            </Grid.Col>
          </Grid>

          <Modal opened={modal} onClose={close} title={accion ? accionTextos[accion].titulo : ''}>
            <form
              onSubmit={(e: FormEvent) => {
                e.preventDefault();
                if (accion) ejecutar.mutate(accion);
              }}
            >
              <Stack>
                {accion && accionTextos[accion].aviso && (
                  <Text size="sm" c="dimmed">
                    {accionTextos[accion].aviso}
                  </Text>
                )}
                {ejecutar.isError && (
                  <Alert color="red" role="alert">
                    {ejecutar.error.message}
                  </Alert>
                )}
                {accion && accionTextos[accion].conMotivo && (
                  <Textarea label="Motivo" required minLength={3} value={motivo} onChange={(e) => setMotivo(e.currentTarget.value)} autosize minRows={2} />
                )}
                <Group justify="flex-end">
                  <Button variant="default" onClick={close}>
                    Volver
                  </Button>
                  <Button type="submit" color={accion === 'cancelar' || accion === 'rechazar' ? 'red' : undefined} loading={ejecutar.isPending}>
                    Confirmar
                  </Button>
                </Group>
              </Stack>
            </form>
          </Modal>
        </>
      )}
    </Consulta>
  );
}

/** La asignación que manda ahora: la más reciente que no fue reemplazada. */
function asignacionVigente(asignaciones: Esquemas['AsignacionLectura'][]): Esquemas['AsignacionLectura'] | undefined {
  return [...asignaciones].sort((a, b) => b.asignadaEn.localeCompare(a.asignadaEn)).find((a) => a.estado !== 'Reemplazada');
}

const conViaje = new Set(['Aceptada', 'EnCurso', 'Finalizada']);

function Asignacion({
  asignacion: a,
  anteriores,
  solicitud,
  puedeResponder,
  onResponder,
}: {
  asignacion: Esquemas['AsignacionLectura'];
  anteriores: number;
  solicitud: Esquemas['SolicitudRespuesta'];
  puedeResponder: boolean;
  onResponder: (accion: Accion) => void;
}) {
  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="nowrap" align="flex-start">
        <Persona nombre={a.conductorNombre} detalle={texto(textos.origenAsignacion, a.origen)} />
        <InsigniaAsignacion estado={a.estado} />
      </Group>
      <Group gap="xs">
        <Placa placa={a.placa} />
      </Group>
      <SimpleGrid cols={2} spacing="sm">
        <Dato etiqueta="Asignada">{instante(a.asignadaEn)}</Dato>
        <Dato etiqueta="Ventana">
          {instante(a.inicio)}
          <Text size="xs" c="dimmed">
            hasta {instante(a.fin)}
          </Text>
        </Dato>
      </SimpleGrid>
      {a.motivoCierre && (
        <Text size="sm" c="dimmed">
          Motivo de cierre: {a.motivoCierre}
        </Text>
      )}
      {a.estado === 'Asignada' && (
        <Alert color="yellow" variant="light" icon={<IconHistory aria-hidden />}>
          Esperando que el conductor acepte por WhatsApp. El pasajero recibe la confirmación cuando el conductor acepta.
        </Alert>
      )}
      {puedeResponder && (
        <>
          <Divider label="Responder por el conductor" labelPosition="center" />
          <Group grow>
            <Button color="teal" leftSection={<IconCheck size={16} aria-hidden />} onClick={() => onResponder('aceptar')}>
              Aceptar
            </Button>
            <Button color="red" variant="light" leftSection={<IconX size={16} aria-hidden />} onClick={() => onResponder('rechazar')}>
              Rechazar
            </Button>
          </Group>
        </>
      )}
      {conViaje.has(a.estado) && <EnlaceViaje asignacionId={a.id} solicitud={solicitud} />}
      {anteriores > 0 && (
        <Text size="xs" c="dimmed">
          Hubo {anteriores} asignación(es) anterior(es) para esta solicitud.
        </Text>
      )}
    </Stack>
  );
}

/** El viaje que nació de la asignación: se busca entre los viajes del pasajero alrededor de la hora del servicio. */
function EnlaceViaje({ asignacionId, solicitud: s }: { asignacionId: string; solicitud: Esquemas['SolicitudRespuesta'] }) {
  const dia = 24 * 60 * 60 * 1000;
  const servicio = new Date(s.fechaHoraServicio).getTime();
  const viajes = useQuery({
    queryKey: ['viajes', 'de-pasajero', s.pasajeroId, s.fechaHoraServicio],
    queryFn: () =>
      datos(
        api.GET('/api/viajes', {
          params: {
            query: {
              PasajeroId: s.pasajeroId,
              Desde: new Date(servicio - dia).toISOString(),
              Hasta: new Date(servicio + dia).toISOString(),
              TamanoPagina: 50,
            },
          },
        }),
      ),
  });
  const viaje = viajes.data?.elementos.find((v) => v.asignacionId === asignacionId);
  if (!viaje) return null;
  return (
    <Button component={Link} to={`/viajes/${viaje.id}`} variant="light" leftSection={<IconRoute size={16} aria-hidden />}>
      Ver el viaje
    </Button>
  );
}

// ---------- Trazabilidad ----------

const iconosEvento: Record<string, { icono: ComponentType<IconProps>; color: string }> = {
  solicitud: { icono: IconFileDescription, color: 'blue' },
  decision: { icono: IconCpu, color: 'violet' },
  asignacion: { icono: IconSteeringWheel, color: 'grape' },
  parada: { icono: IconMapPin, color: 'teal' },
  incidente: { icono: IconAlertTriangle, color: 'orange' },
  calificacion: { icono: IconStar, color: 'yellow' },
};

function Trazabilidad({ eventos }: { eventos: Esquemas['EventoTrazabilidad'][] }) {
  if (eventos.length === 0) return <EstadoVacio icono={IconRoute} titulo="Sin eventos" />;
  return (
    <Timeline bulletSize={28} lineWidth={2} active={eventos.length}>
      {eventos.map((e, i) => {
        const { icono: Icono, color } = iconosEvento[e.tipo] ?? { icono: IconHistory, color: 'gray' };
        return (
          <Timeline.Item
            key={`${e.fechaHora}-${i}`}
            bullet={
              <ThemeIcon size={28} radius="xl" color={color} variant="light">
                <Icono size={15} aria-hidden />
              </ThemeIcon>
            }
            title={<Text size="sm" fw={600}>{e.descripcion}</Text>}
          >
            <Text size="xs" c="dimmed">
              {instante(e.fechaHora)}
            </Text>
            {e.motivo && (
              <Text size="sm" mt={4}>
                {e.motivo}
              </Text>
            )}
          </Timeline.Item>
        );
      })}
    </Timeline>
  );
}

function Historial({ historial }: { historial: Esquemas['CambioEstadoRespuesta'][] }) {
  if (historial.length === 0) return <EstadoVacio icono={IconHistory} titulo="Sin cambios de estado" />;
  return (
    <Table.ScrollContainer minWidth={480}>
      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Fecha</Table.Th>
            <Table.Th>De</Table.Th>
            <Table.Th>A</Table.Th>
            <Table.Th>Motivo</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {historial.map((h, i) => (
            <Table.Tr key={`${h.fechaHora}-${i}`}>
              <Table.Td>{instante(h.fechaHora)}</Table.Td>
              <Table.Td>{h.estadoAnterior ? <InsigniaSolicitud estado={h.estadoAnterior} /> : '—'}</Table.Td>
              <Table.Td>
                <InsigniaSolicitud estado={h.estadoNuevo} />
              </Table.Td>
              <Table.Td>{h.motivo ?? '—'}</Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
    </Table.ScrollContainer>
  );
}

// ---------- Decisiones del motor (RF-29) ----------

const decimal = new Intl.NumberFormat('es-CO', { maximumFractionDigits: 2 });

function Decisiones({ solicitudId }: { solicitudId: string }) {
  const decisiones = useQuery({
    queryKey: ['solicitud', solicitudId, 'decisiones'],
    queryFn: () => datos(api.GET('/api/solicitudes/{id}/decisiones', { params: { path: { id: solicitudId } } })),
  });
  // Los nombres de los candidatos: la decisión guarda solo sus identificadores.
  const conductores = useQuery({
    queryKey: ['conductores', 'nombres'],
    queryFn: () => datos(api.GET('/api/conductores', { params: { query: { TamanoPagina: 200 } } })),
    select: (p) => new Map(p.elementos.map((c) => [c.id, c.nombre])),
    enabled: (decisiones.data?.length ?? 0) > 0,
  });
  const nombre = (id: string) => conductores.data?.get(id) ?? 'Conductor';

  return (
    <Consulta consulta={decisiones}>
      {(lista) =>
        lista.length === 0 ? (
          <EstadoVacio icono={IconCpu} titulo="El motor aún no ha evaluado esta solicitud" />
        ) : (
          <Accordion variant="separated" defaultValue={lista.at(-1)!.id}>
            {[...lista].reverse().map((d) => (
              <Accordion.Item key={d.id} value={d.id}>
                <Accordion.Control icon={<IconListCheck size={18} aria-hidden />}>
                  <Group gap="xs" wrap="wrap">
                    <Text size="sm" fw={600}>
                      {d.resultado}
                    </Text>
                    <Badge variant="light" size="sm">
                      {texto(textos.origenAsignacion, d.origen)}
                    </Badge>
                    <Badge variant="default" size="sm">
                      Configuración v{d.versionConfiguracion}
                    </Badge>
                    <Text size="xs" c="dimmed">
                      {instante(d.evaluadaEn)}
                    </Text>
                  </Group>
                </Accordion.Control>
                <Accordion.Panel>
                  <Text size="sm" mb="xs">
                    {d.motivo}
                  </Text>
                  {d.desempate && (
                    <Text size="xs" c="dimmed" mb="xs">
                      Desempate: {d.desempate}
                    </Text>
                  )}
                  {d.evaluaciones.length === 0 ? (
                    <Text size="sm" c="dimmed">
                      No había conductores con turno en esa ventana.
                    </Text>
                  ) : (
                    <Table.ScrollContainer minWidth={520}>
                      <Table>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>#</Table.Th>
                            <Table.Th>Candidato</Table.Th>
                            <Table.Th>Elegible</Table.Th>
                            <Table.Th>Puntaje</Table.Th>
                            <Table.Th>Por qué</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {[...d.evaluaciones]
                            .sort((a, b) => (a.posicionRanking ?? 999) - (b.posicionRanking ?? 999))
                            .map((e) => (
                              <Table.Tr key={`${e.conductorId}-${e.vehiculoId}`} fw={e.conductorId === d.conductorSeleccionadoId ? 700 : undefined}>
                                <Table.Td>{e.posicionRanking ?? '—'}</Table.Td>
                                <Table.Td>
                                  {nombre(e.conductorId)}
                                  {e.conductorId === d.conductorSeleccionadoId && (
                                    <Badge ml={6} size="xs" color="teal">
                                      Elegido
                                    </Badge>
                                  )}
                                </Table.Td>
                                <Table.Td>
                                  {e.elegible ? (
                                    <Badge color="teal" variant="light" leftSection={<IconCheck size={11} aria-hidden />}>
                                      Sí
                                    </Badge>
                                  ) : (
                                    <Badge color="red" variant="light" leftSection={<IconX size={11} aria-hidden />}>
                                      No
                                    </Badge>
                                  )}
                                </Table.Td>
                                <Table.Td>{e.elegible ? decimal.format(e.puntajeTotal) : '—'}</Table.Td>
                                <Table.Td>
                                  <Text size="sm">{e.motivoDescarte ?? 'Cumple todas las reglas'}</Text>
                                </Table.Td>
                              </Table.Tr>
                            ))}
                        </Table.Tbody>
                      </Table>
                    </Table.ScrollContainer>
                  )}
                </Accordion.Panel>
              </Accordion.Item>
            ))}
          </Accordion>
        )
      }
    </Consulta>
  );
}
