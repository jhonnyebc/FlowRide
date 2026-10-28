import { Badge, Button, Group, NumberInput, Paper, Stack, Switch, Table, Text, Title, VisuallyHidden } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconArrowBackUp, IconCirclesRelation, IconPlayerPlay, IconPlus } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Confirmacion, PanelLateral } from '../administracion/comun';
import { api, datos, type Esquemas } from '../api/cliente';
import { esAdministrador, useUsuario } from '../auth/usuario';
import { Consulta, Encabezado, EstadoVacio } from '../comun/Componentes';
import { instante } from '../comun/fechas';
import { texto, textos } from '../textos';

type Configuracion = Esquemas['ConfiguracionMotorRespuesta'];
type Regla = Esquemas['ReglaRespuesta'];

/** Orden de presentación de los tipos de regla: así las aplica el motor (ADR-0009). */
const OrdenTipos = ['Filtering', 'Priority', 'Scoring', 'TieBreak'];
/** Las eliminatorias son obligatorias: ninguna configuración puede deshabilitarlas (la API lo vuelve a validar). */
const esObligatoria = (r: Regla) => r.tipo === 'Filtering';
const ParametroVentanaDias = 'ventanaDias';

const ordenadas = (reglas: Regla[]) =>
  [...reglas].sort((a, b) => OrdenTipos.indexOf(a.tipo) - OrdenTipos.indexOf(b.tipo) || a.orden - b.orden || a.codigo.localeCompare(b.codigo));

function InsigniaConfiguracion({ estado }: { estado: string }) {
  const color = estado === 'Activa' ? 'teal' : estado === 'Borrador' ? 'blue' : 'gray';
  return (
    <Badge color={color} variant={estado === 'Archivada' ? 'outline' : 'light'}>
      {texto(textos.estadoConfiguracion, estado)}
    </Badge>
  );
}

/**
 * Configuración del motor de asignación (RF-21, ADR-0009): versiones con sus reglas. Una versión nueva nace como
 * borrador y se activa aparte; activar una versión anterior es el rollback. Las decisiones del motor guardan la versión
 * con la que se tomaron. Solo el Administrador crea y activa, con inicio de sesión reciente.
 */
export function PaginaMotor() {
  const usuario = useUsuario();
  const puedeEditar = esAdministrador(usuario);
  const cliente = useQueryClient();
  const [nueva, setNueva] = useState<Configuracion | null>(null);
  const [porActivar, setPorActivar] = useState<Configuracion | null>(null);
  const consulta = useQuery({ queryKey: ['motor'], queryFn: () => datos(api.GET('/api/motor/configuraciones')) });

  const activar = useMutation({
    mutationFn: (c: Configuracion) => datos(api.POST('/api/motor/configuraciones/{version}/activar', { params: { path: { version: c.version } } })),
    onSuccess: (c) => {
      notifications.show({ color: 'green', message: `El motor usa ahora la versión ${c.version}.` });
      setPorActivar(null);
      void cliente.invalidateQueries({ queryKey: ['motor'] });
    },
  });

  const base = consulta.data?.find((v) => v.estado === 'Activa') ?? consulta.data?.[0];

  return (
    <>
      <Encabezado
        titulo={textos.menu.motor}
        descripcion="Las reglas con que el motor elige conductor y vehículo para cada solicitud."
        migas={[{ etiqueta: textos.menu.configuracion }, { etiqueta: textos.menu.motor }]}
      >
        {puedeEditar && base && (
          <Button leftSection={<IconPlus size={16} aria-hidden />} onClick={() => setNueva(base)}>
            Nueva versión
          </Button>
        )}
      </Encabezado>
      <Consulta consulta={consulta}>
      {(versiones) => {
        const activa = versiones.find((v) => v.estado === 'Activa');
        return (
          <>
            {activa ? (
              <Paper withBorder p="md" radius="md" mb="lg">
                <Group justify="space-between" mb="sm">
                  <Title order={2} size="h4">
                    Versión activa: {activa.version}
                  </Title>
                  <Text size="sm" c="var(--fr-texto-suave)">
                    Activada {instante(activa.activadaEn)}
                  </Text>
                </Group>
                <TablaReglas reglas={activa.reglas} />
              </Paper>
            ) : (
              <EstadoVacio icono={IconCirclesRelation} titulo="Sin versión activa" descripcion="El motor no asigna hasta que haya una versión activa." />
            )}

            <Title order={2} size="h4" mb="xs">
              Historial de versiones
            </Title>
            <Table.ScrollContainer minWidth={600}>
              <Table>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Versión</Table.Th>
                    <Table.Th>Estado</Table.Th>
                    <Table.Th>Creada</Table.Th>
                    <Table.Th>Activada</Table.Th>
                    {puedeEditar && (
                      <Table.Th>
                        <VisuallyHidden>Acciones</VisuallyHidden>
                      </Table.Th>
                    )}
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {versiones.map((v) => (
                    <Table.Tr key={v.id}>
                      <Table.Td>v{v.version}</Table.Td>
                      <Table.Td>
                        <InsigniaConfiguracion estado={v.estado} />
                      </Table.Td>
                      <Table.Td>{instante(v.creadaEn)}</Table.Td>
                      <Table.Td>{instante(v.activadaEn)}</Table.Td>
                      {puedeEditar && (
                        <Table.Td>
                          {v.estado !== 'Activa' && (
                            <Button
                              size="xs"
                              variant="default"
                              leftSection={
                                activa && v.version < activa.version ? <IconArrowBackUp size={14} aria-hidden /> : <IconPlayerPlay size={14} aria-hidden />
                              }
                              onClick={() => {
                                activar.reset();
                                setPorActivar(v);
                              }}
                              aria-label={`Activar la versión ${v.version}`}
                            >
                              {activa && v.version < activa.version ? 'Volver a esta' : 'Activar'}
                            </Button>
                          )}
                        </Table.Td>
                      )}
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>

            {porActivar && (
              <Confirmacion
                abierta
                alCerrar={() => setPorActivar(null)}
                titulo={`Activar la versión ${porActivar.version}`}
                mensaje={
                  activa
                    ? `La versión ${activa.version} queda archivada y las asignaciones siguientes usan la ${porActivar.version}. Las decisiones ya tomadas conservan la versión con que se tomaron.`
                    : `Las asignaciones siguientes usan la versión ${porActivar.version}.`
                }
                textoConfirmar="Activar"
                mutacion={activar}
                alConfirmar={() => activar.mutate(porActivar)}
              >
                <TablaReglas reglas={porActivar.reglas} />
              </Confirmacion>
            )}
          </>
        );
      }}
      </Consulta>
      {nueva && <NuevaVersion key={nueva.id} base={nueva} alCerrar={() => setNueva(null)} />}
    </>
  );
}

function TablaReglas({ reglas }: { reglas: Regla[] }) {
  return (
    <Table.ScrollContainer minWidth={560}>
      <Table>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Regla</Table.Th>
            <Table.Th>Tipo</Table.Th>
            <Table.Th>Habilitada</Table.Th>
            <Table.Th>Orden</Table.Th>
            <Table.Th>Peso</Table.Th>
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {ordenadas(reglas).map((r) => (
            <Table.Tr key={r.codigo}>
              <Table.Td>
                <Text size="sm">{texto(textos.regla, r.codigo)}</Text>
                {r.parametros[ParametroVentanaDias] && (
                  <Text size="xs" c="var(--fr-texto-suave)">
                    Cuenta las asignaciones de los últimos {r.parametros[ParametroVentanaDias]} días
                  </Text>
                )}
              </Table.Td>
              <Table.Td>{texto(textos.tipoRegla, r.tipo)}</Table.Td>
              <Table.Td>{r.habilitada ? 'Sí' : 'No'}</Table.Td>
              <Table.Td>{r.orden}</Table.Td>
              <Table.Td>{r.peso}</Table.Td>
            </Table.Tr>
          ))}
        </Table.Tbody>
      </Table>
    </Table.ScrollContainer>
  );
}

/** Crea un borrador a partir de la versión base; la API valida pesos, órdenes, obligatorias y dependencias. */
function NuevaVersion({ base, alCerrar }: { base: Configuracion; alCerrar: () => void }) {
  const cliente = useQueryClient();
  const [reglas, setReglas] = useState<Regla[]>(ordenadas(base.reglas));
  const cambiar = (codigo: string, cambio: Partial<Regla>) => setReglas((rs) => rs.map((r) => (r.codigo === codigo ? { ...r, ...cambio } : r)));

  const crear = useMutation({
    mutationFn: () =>
      datos(
        api.POST('/api/motor/configuraciones', {
          body: { reglas: reglas.map((r) => ({ codigo: r.codigo, habilitada: r.habilitada, orden: r.orden, peso: r.peso, parametros: r.parametros })) },
        }),
      ),
    onSuccess: (c) => {
      notifications.show({ color: 'green', message: `Se creó la versión ${c.version} como borrador. Actívala para que el motor la use.` });
      void cliente.invalidateQueries({ queryKey: ['motor'] });
      alCerrar();
    },
  });

  return (
    <PanelLateral
      abierto
      alCerrar={alCerrar}
      titulo="Nueva versión del motor"
      descripcion={`Parte de la versión ${base.version}. Queda como borrador hasta que la actives. El peso va de 0 a 1 y dos reglas del mismo tipo no pueden tener el mismo orden.`}
      mutacion={crear}
      alEnviar={() => crear.mutate()}
      textoEnviar="Crear borrador"
      tamano="lg"
    >
      {reglas.map((r) => {
        const nombre = texto(textos.regla, r.codigo);
        return (
          <Paper key={r.codigo} withBorder p="sm" radius="md">
            <Stack gap="xs">
              <Group justify="space-between" wrap="nowrap">
                <div>
                  <Text size="sm" fw={600}>
                    {nombre}
                  </Text>
                  <Text size="xs" c="var(--fr-texto-suave)">
                    {texto(textos.tipoRegla, r.tipo)}
                    {esObligatoria(r) ? ' · obligatoria' : ''}
                  </Text>
                </div>
                <Switch
                  label="Habilitada"
                  checked={r.habilitada}
                  disabled={esObligatoria(r)}
                  onChange={(e) => cambiar(r.codigo, { habilitada: e.currentTarget.checked })}
                  aria-label={`${nombre}: habilitada`}
                />
              </Group>
              <Group grow>
                <NumberInput
                  label="Orden"
                  aria-label={`${nombre}: orden`}
                  min={1}
                  allowDecimal={false}
                  value={r.orden}
                  onChange={(v) => cambiar(r.codigo, { orden: Number(v) })}
                />
                <NumberInput
                  label="Peso"
                  aria-label={`${nombre}: peso`}
                  min={0}
                  max={1}
                  step={0.1}
                  decimalScale={2}
                  value={r.peso}
                  onChange={(v) => cambiar(r.codigo, { peso: Number(v) })}
                />
                {r.parametros[ParametroVentanaDias] !== undefined && (
                  <NumberInput
                    label="Ventana (días)"
                    aria-label={`${nombre}: ventana en días`}
                    min={1}
                    allowDecimal={false}
                    value={Number(r.parametros[ParametroVentanaDias])}
                    onChange={(v) => cambiar(r.codigo, { parametros: { ...r.parametros, [ParametroVentanaDias]: String(v) } })}
                  />
                )}
              </Group>
            </Stack>
          </Paper>
        );
      })}
    </PanelLateral>
  );
}
