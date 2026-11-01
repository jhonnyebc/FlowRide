import { BarChart, DonutChart } from '@mantine/charts';
import {
  Anchor,
  Badge,
  ColorSwatch,
  Grid,
  Group,
  Paper,
  Progress,
  RingProgress,
  SegmentedControl,
  SimpleGrid,
  Skeleton,
  Stack,
  Text,
  ThemeIcon,
} from '@mantine/core';
import {
  IconAlertTriangle,
  IconBell,
  IconBuildingSkyscraper,
  IconCalendarStats,
  IconCar,
  IconChartBar,
  IconChartDonut,
  IconCircleCheck,
  IconClockCheck,
  IconListDetails,
  IconRoute,
  IconStarFilled,
  IconSteeringWheel,
  IconThumbUp,
  IconUrgent,
  IconX,
  type IconProps,
} from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { type ComponentType, useState } from 'react';
import { Link } from 'react-router';
import { api, datos, type Esquemas } from '../api/cliente';
import { indicadores, monitoreo, vistaDelDia } from '../api/consultas';
import { colorSeveridad, IconoSeveridad } from '../alertas/severidad';
import { claveAlertas } from '../alertas/useAlertas';
import { useUsuario } from '../auth/usuario';
import {
  Consulta,
  coloresSolicitud,
  Encabezado,
  EstadoVacio,
  InsigniaNivel,
  InsigniaSolicitud,
  InsigniaViaje,
  Persona,
  Tarjeta,
} from '../comun/Componentes';
import { hora, hoyIso, instante } from '../comun/fechas';
import { texto, textos } from '../textos';
import { type ClavePeriodo, horaPico, porcentaje, rangoDelPeriodo, saludo, serviciosPorHora } from './calculos';

const periodos: { value: ClavePeriodo; label: string; frase: string }[] = [
  { value: 'hoy', label: 'Hoy', frase: 'hoy' },
  { value: '7d', label: '7 días', frase: 'en los últimos 7 días' },
  { value: '30d', label: '30 días', frase: 'en los últimos 30 días' },
];

const numero = new Intl.NumberFormat('es-CO', { maximumFractionDigits: 1 });
const fechaLarga = new Intl.DateTimeFormat('es-CO', { weekday: 'long', day: 'numeric', month: 'long' });
const formatoPorcentaje = (valor: number | null | undefined) => (valor == null ? '—' : `${numero.format(valor)} %`);

/**
 * Tablero del operador (RF-36, RF-38): los indicadores del periodo, cómo viene el día y lo que pide atención ya. Todo
 * enlaza a su detalle; los gráficos llevan su resumen en texto para el lector de pantalla (OPLOG-ACC-02).
 */
export function Tablero() {
  const usuario = useUsuario();
  const [periodo, setPeriodo] = useState<ClavePeriodo>('hoy');
  const { desde, hasta } = rangoDelPeriodo(periodo);
  const frase = periodos.find((p) => p.value === periodo)!.frase;

  const resumen = useQuery(indicadores(desde, hasta));
  const vista = useQuery(vistaDelDia(hoyIso()));
  const viajes = useQuery(monitoreo());
  const alertas = useQuery({
    queryKey: [...claveAlertas, 'recientes'],
    queryFn: () => datos(api.GET('/api/alertas', { params: { query: { Pagina: 1, TamanoPagina: 5 } } })),
  });
  const primerNombre = usuario.nombre.split(' ')[0] ?? usuario.nombre;
  const hoyTexto = fechaLarga.format(new Date());

  return (
    <>
      <Encabezado
        titulo={`${saludo()}, ${primerNombre}`}
        descripcion={`${hoyTexto.charAt(0).toUpperCase()}${hoyTexto.slice(1)} · así va la operación ${frase}.`}
      >
        <SegmentedControl
          aria-label="Periodo de los indicadores"
          value={periodo}
          onChange={(v) => setPeriodo(v as ClavePeriodo)}
          data={periodos.map(({ value, label }) => ({ value, label }))}
        />
      </Encabezado>

      <Consulta consulta={resumen} esqueleto={<EsqueletoIndicadores />}>
        {(i) => <Indicadores datos={i} />}
      </Consulta>

      <Grid mt="md" gutter="md">
        <Grid.Col span={{ base: 12, lg: 8 }}>
          <Tarjeta titulo="Servicios de hoy por hora" icono={IconChartBar}>
            <Consulta consulta={vista} esqueleto={<Skeleton h={260} radius="md" />}>
              {(v) => <GraficaPorHora servicios={v.servicios} />}
            </Consulta>
          </Tarjeta>
        </Grid.Col>
        <Grid.Col span={{ base: 12, lg: 4 }}>
          <Tarjeta titulo="Solicitudes por estado" icono={IconChartDonut}>
            <Consulta consulta={resumen} esqueleto={<Skeleton h={260} radius="md" />}>
              {(i) => <GraficaPorEstado porEstado={i.volumen.solicitudesPorEstado} frase={frase} />}
            </Consulta>
          </Tarjeta>
        </Grid.Col>

        <Grid.Col span={{ base: 12, lg: 7 }}>
          <Tarjeta
            titulo="Requieren atención hoy"
            icono={IconUrgent}
            acciones={
              <Anchor component={Link} to="/dia" size="sm">
                Ver el día completo
              </Anchor>
            }
          >
            <Consulta consulta={vista}>{(v) => <PorAtender servicios={v.servicios} />}</Consulta>
          </Tarjeta>
        </Grid.Col>
        <Grid.Col span={{ base: 12, lg: 5 }}>
          <Tarjeta
            titulo="Viajes en curso y próximos"
            icono={IconRoute}
            acciones={
              <Anchor component={Link} to="/viajes" size="sm">
                Ver todos
              </Anchor>
            }
          >
            <Consulta consulta={viajes}>{(lista) => <ViajesActivos viajes={lista} />}</Consulta>
          </Tarjeta>
        </Grid.Col>

        <Grid.Col span={{ base: 12, md: 6, lg: 4 }}>
          <Tarjeta titulo="Conductores con más viajes" icono={IconSteeringWheel}>
            <Consulta consulta={resumen}>{(i) => <Conductores conductores={i.conductores.masViajes} />}</Consulta>
          </Tarjeta>
        </Grid.Col>
        <Grid.Col span={{ base: 12, md: 6, lg: 4 }}>
          <Tarjeta titulo="Empresas cliente" icono={IconBuildingSkyscraper}>
            <Consulta consulta={resumen}>{(i) => <Empresas empresas={i.empresas} />}</Consulta>
          </Tarjeta>
        </Grid.Col>
        <Grid.Col span={{ base: 12, lg: 4 }}>
          <Tarjeta
            titulo="Últimas alertas"
            icono={IconBell}
            acciones={
              <Anchor component={Link} to="/alertas" size="sm">
                Ver todas
              </Anchor>
            }
          >
            <Consulta consulta={alertas}>{(p) => <UltimasAlertas alertas={p.elementos} />}</Consulta>
          </Tarjeta>
        </Grid.Col>
      </Grid>
    </>
  );
}

// ---------- Indicadores ----------

function Indicadores({ datos: i }: { datos: Esquemas['IndicadoresLectura'] }) {
  const { volumen: v, cumplimiento: c, conductores: k } = i;
  const base = v.solicitudes - v.canceladas;
  return (
    <Stack gap="md">
      <SimpleGrid cols={{ base: 1, xs: 2, lg: 4 }} spacing="md">
        <Indicador etiqueta="Solicitudes" valor={numero.format(v.solicitudes)} icono={IconListDetails} color="primario">
          {v.urgentes} urgentes · {v.pendientes} pendientes
        </Indicador>
        <Indicador etiqueta="Atendidas" valor={formatoPorcentaje(porcentaje(v.atendidas, base))} anillo={porcentaje(v.atendidas, base)} icono={IconCircleCheck}>
          {v.atendidas} de {base} (sin contar canceladas)
        </Indicador>
        <Indicador etiqueta="Puntualidad" valor={formatoPorcentaje(c.porcentajePuntualidad)} anillo={c.porcentajePuntualidad} icono={IconClockCheck}>
          {c.puntuales} de {c.viajesIniciados} viajes · tolerancia {c.toleranciaMinutos} min
        </Indicador>
        <Indicador
          etiqueta="Aceptación de conductores"
          valor={formatoPorcentaje(k.porcentajeAceptacion)}
          anillo={k.porcentajeAceptacion}
          icono={IconThumbUp}
        >
          {k.asignaciones} asignaciones · {formatoPorcentaje(k.porcentajeVencidas)} sin respuesta a tiempo
        </Indicador>
      </SimpleGrid>
      <SimpleGrid cols={{ base: 2, md: 4 }} spacing="md">
        <Mini etiqueta="Viajes realizados" valor={numero.format(v.viajesRealizados)} icono={IconCar} />
        <Mini etiqueta="Viajes cancelados" valor={numero.format(v.viajesCancelados)} icono={IconX} />
        <Mini etiqueta="Incidentes" valor={numero.format(c.incidentes)} icono={IconAlertTriangle} />
        <Mini
          etiqueta="Calificación promedio"
          valor={k.calificacionPromedio == null ? '—' : `${numero.format(k.calificacionPromedio)} / 5`}
          icono={IconStarFilled}
        />
      </SimpleGrid>
    </Stack>
  );
}

function colorDePorcentaje(valor: number | null | undefined): string {
  if (valor == null) return 'gray';
  return valor >= 90 ? 'teal' : valor >= 70 ? 'yellow' : 'red';
}

function Indicador({
  etiqueta,
  valor,
  icono: Icono,
  color,
  anillo,
  children,
}: {
  etiqueta: string;
  valor: string;
  icono: ComponentType<IconProps>;
  color?: string;
  anillo?: number | null;
  children: React.ReactNode;
}) {
  const tono = color ?? colorDePorcentaje(anillo);
  return (
    <Paper p={24} className="superficie">
      <Group justify="space-between" wrap="nowrap" align="flex-start">
        <div>
          <Group gap={6} mb={4} wrap="nowrap">
            <ThemeIcon size="sm" variant="transparent" radius="sm">
              <Icono size={14} aria-hidden />
            </ThemeIcon>
            <Text size="sm" c="var(--fr-texto-suave)">
              {etiqueta}
            </Text>
          </Group>
          <Text fz={36} fw={400} lh={1.22} className="cifra">
            {valor}
          </Text>
        </div>
        {anillo !== undefined && (
          <RingProgress
            aria-hidden
            size={64}
            thickness={7}
            roundCaps
            sections={[{ value: anillo ?? 0, color: tono }]}
            rootColor="var(--mantine-color-default-border)"
          />
        )}
      </Group>
      <Text size="xs" c="dimmed" mt={6}>
        {children}
      </Text>
    </Paper>
  );
}

function Mini({ etiqueta, valor, icono: Icono }: { etiqueta: string; valor: string; icono: ComponentType<IconProps> }) {
  return (
    <Paper px="md" py="sm" className="superficie">
      <Group gap="sm" wrap="nowrap">
        <ThemeIcon variant="filled" size="lg" radius="md">
          <Icono size={18} aria-hidden />
        </ThemeIcon>
        <div>
          <Text fw={500} size="lg" lh={1.1} className="cifra">
            {valor}
          </Text>
          <Text size="xs" c="dimmed">
            {etiqueta}
          </Text>
        </div>
      </Group>
    </Paper>
  );
}

function EsqueletoIndicadores() {
  return (
    <Stack gap="md">
      <SimpleGrid cols={{ base: 1, xs: 2, lg: 4 }}>
        {[1, 2, 3, 4].map((n) => (
          <Skeleton key={n} h={118} radius="md" />
        ))}
      </SimpleGrid>
      <SimpleGrid cols={{ base: 2, md: 4 }}>
        {[1, 2, 3, 4].map((n) => (
          <Skeleton key={n} h={62} radius="md" />
        ))}
      </SimpleGrid>
    </Stack>
  );
}

// ---------- Gráficas ----------

const seriesNivel = [
  { name: 'Critico', label: textos.nivel.Critico!, color: 'red.6' },
  { name: 'Atencion', label: textos.nivel.Atencion!, color: 'yellow.5' },
  { name: 'Normal', label: textos.nivel.Normal!, color: 'teal.6' },
];

function GraficaPorHora({ servicios }: { servicios: Esquemas['ServicioVistaLectura'][] }) {
  const filas = serviciosPorHora(servicios);
  if (filas.length === 0)
    return <EstadoVacio icono={IconCalendarStats} titulo="Sin servicios hoy" descripcion="Cuando haya servicios programados verás aquí cómo se reparten en el día." />;
  const resumen = `${servicios.length} servicios hoy entre las ${filas[0]!.hora} y las ${filas.at(-1)!.hora}; la hora con más servicios es las ${horaPico(filas)!.hora}.`;
  return (
    <div role="img" aria-label={resumen}>
      <BarChart
        h={260}
        data={filas}
        dataKey="hora"
        type="stacked"
        series={seriesNivel}
        withLegend
        legendProps={{ verticalAlign: 'bottom', height: 36 }}
        tickLine="none"
        gridAxis="y"
        yAxisProps={{ allowDecimals: false, width: 28 }}
        barProps={{ radius: 3 }}
      />
    </div>
  );
}

function GraficaPorEstado({ porEstado, frase }: { porEstado: Record<string, number>; frase: string }) {
  const datosGrafica = Object.entries(porEstado)
    .filter(([, n]) => n > 0)
    .map(([estado, n]) => ({ name: texto(textos.estadoSolicitud, estado), value: n, color: `${coloresSolicitud[estado] ?? 'gray'}.6` }));
  const total = datosGrafica.reduce((s, d) => s + d.value, 0);
  if (total === 0) return <EstadoVacio icono={IconChartDonut} titulo="Sin solicitudes" descripcion={`No se registraron solicitudes ${frase}.`} />;
  return (
    <Stack gap="sm" align="center">
      <DonutChart data={datosGrafica} size={170} thickness={26} chartLabel={`${total}`} withTooltip={false} aria-hidden />
      <Stack component="ul" gap={4} w="100%" p={0} m={0} style={{ listStyle: 'none' }} aria-label={`Solicitudes por estado ${frase}`}>
        {datosGrafica.map((d) => (
          <Group key={d.name} component="li" justify="space-between" wrap="nowrap">
            <Group gap={8} wrap="nowrap">
              <ColorSwatch color={`var(--mantine-color-${d.color.replace('.', '-')})`} size={12} aria-hidden />
              <Text size="sm">{d.name}</Text>
            </Group>
            <Text size="sm" fw={600} className="cifra">
              {d.value} <Text span c="dimmed" size="xs">({formatoPorcentaje(porcentaje(d.value, total))})</Text>
            </Text>
          </Group>
        ))}
      </Stack>
    </Stack>
  );
}

// ---------- Listas ----------

const maximoEnLista = 6;

function PorAtender({ servicios }: { servicios: Esquemas['ServicioVistaLectura'][] }) {
  const pendientes = servicios.filter((s) => s.nivel !== 'Normal');
  if (pendientes.length === 0)
    return <EstadoVacio icono={IconCircleCheck} titulo="Todo en orden" descripcion="Ningún servicio de hoy requiere atención." />;
  return (
    <Stack component="ul" gap={0} p={0} m={0} style={{ listStyle: 'none' }}>
      {pendientes.slice(0, maximoEnLista).map(({ servicio: s, nivel, motivo }) => (
        <Group key={s.id} component="li" justify="space-between" wrap="nowrap" py={8} className="fila-lista">
          <Group gap="sm" wrap="nowrap" style={{ minWidth: 0 }}>
            <Text fw={500} size="sm" w={48} className="cifra">
              {hora(s.fechaHoraServicio)}
            </Text>
            <div style={{ minWidth: 0 }}>
              <Anchor component={Link} to={`/solicitudes/${s.id}`} fw={600} size="sm">
                {s.pasajeroNombre}
              </Anchor>
              <Text size="xs" c="dimmed" truncate>
                {motivo ?? s.empresaClienteNombre ?? '—'}
              </Text>
            </div>
          </Group>
          <Group gap={6} wrap="nowrap">
            <InsigniaSolicitud estado={s.estado} />
            <InsigniaNivel nivel={nivel} />
          </Group>
        </Group>
      ))}
      {pendientes.length > maximoEnLista && (
        <Text component="li" size="sm" c="dimmed" pt="xs">
          y {pendientes.length - maximoEnLista} más en la vista del día.
        </Text>
      )}
    </Stack>
  );
}

function ViajesActivos({ viajes }: { viajes: Esquemas['MonitoreoViajeLectura'][] }) {
  if (viajes.length === 0)
    return <EstadoVacio icono={IconRoute} titulo="Sin viajes activos" descripcion="No hay viajes en curso ni programados para las próximas horas." />;
  return (
    <Stack component="ul" gap={0} p={0} m={0} style={{ listStyle: 'none' }}>
      {viajes.slice(0, maximoEnLista).map(({ viaje: v, retrasado, minutosRetraso }) => (
        <Group key={v.id} component="li" justify="space-between" wrap="nowrap" py={8} className="fila-lista">
          <Persona
            nombre={v.conductorNombre}
            tamano="sm"
            detalle={
              <>
                {v.placa} ·{' '}
                <Anchor component={Link} to={`/viajes/${v.id}`} size="xs">
                  {v.pasajeroNombre}
                </Anchor>
              </>
            }
          />
          <Stack gap={2} align="flex-end">
            <InsigniaViaje estado={v.estado} />
            {retrasado ? (
              <Badge color="red" variant="outline" size="xs">
                {minutosRetraso} min tarde
              </Badge>
            ) : (
              <Text size="xs" c="dimmed">
                {hora(v.inicioProgramado)}
              </Text>
            )}
          </Stack>
        </Group>
      ))}
    </Stack>
  );
}

function Conductores({ conductores }: { conductores: Esquemas['ConductorReporteLectura'][] }) {
  if (conductores.length === 0)
    return <EstadoVacio icono={IconSteeringWheel} titulo="Sin viajes en el periodo" descripcion="Aún no hay conductores con viajes realizados." />;
  return (
    <Stack gap="sm">
      {conductores.slice(0, 5).map((c) => (
        <div key={c.conductorId}>
          <Group justify="space-between" wrap="nowrap" mb={4}>
            <Persona nombre={c.nombre} tamano="sm" detalle={c.placas || 'Sin vehículo'} />
            <Stack gap={0} align="flex-end">
              <Text size="sm" fw={500} className="cifra">
                {c.viajesRealizados} viajes
              </Text>
              {c.calificacionPromedio != null && (
                <Group gap={2} wrap="nowrap">
                  <IconStarFilled size={12} color="var(--mantine-color-yellow-6)" aria-hidden />
                  <Text size="xs" c="dimmed">
                    {numero.format(c.calificacionPromedio)}
                  </Text>
                </Group>
              )}
            </Stack>
          </Group>
          <Progress
            value={c.porcentajeAceptacion ?? 0}
            color={colorDePorcentaje(c.porcentajeAceptacion)}
            size="sm"
            radius="xl"
            aria-label={`Aceptación de ${c.nombre}: ${formatoPorcentaje(c.porcentajeAceptacion)}`}
          />
        </div>
      ))}
    </Stack>
  );
}

function Empresas({ empresas }: { empresas: Esquemas['EmpresaReporteLectura'][] }) {
  const filas = [...empresas]
    .sort((a, b) => b.solicitudes - a.solicitudes)
    .slice(0, 6)
    .map((e) => ({ empresa: e.nombre, Solicitudes: e.solicitudes, Atendidas: e.atendidas }));
  if (filas.length === 0)
    return <EstadoVacio icono={IconBuildingSkyscraper} titulo="Sin solicitudes de empresas" descripcion="Aún no hay solicitudes en este periodo." />;
  const resumen = filas.map((f) => `${f.empresa}: ${f.Solicitudes} solicitudes, ${f.Atendidas} atendidas`).join('; ');
  return (
    <div role="img" aria-label={resumen}>
      <BarChart
        h={56 + filas.length * 44}
        data={filas}
        dataKey="empresa"
        orientation="vertical"
        series={[
          { name: 'Solicitudes', color: 'gray.4' },
          { name: 'Atendidas', color: 'primario.6' },
        ]}
        withLegend
        legendProps={{ verticalAlign: 'bottom', height: 32 }}
        gridAxis="x"
        tickLine="none"
        yAxisProps={{ width: 110 }}
        xAxisProps={{ allowDecimals: false }}
        barProps={{ radius: 3 }}
      />
    </div>
  );
}

function UltimasAlertas({ alertas }: { alertas: Esquemas['AlertaLectura'][] }) {
  if (alertas.length === 0) return <EstadoVacio icono={IconBell} titulo="Sin alertas" descripcion="Cuando algo necesite tu atención aparecerá aquí." />;
  return (
    <Stack component="ul" gap={0} p={0} m={0} style={{ listStyle: 'none' }}>
      {alertas.map((a) => (
        <Group key={a.id} component="li" gap="sm" wrap="nowrap" align="flex-start" py={8} className="fila-lista">
          <ThemeIcon variant="light" color={colorSeveridad(a.severidad)} radius="xl">
            <IconoSeveridad severidad={a.severidad} tamano={16} />
          </ThemeIcon>
          <div style={{ minWidth: 0 }}>
            <Text size="sm" fw={a.leidaEn ? 500 : 700} lineClamp={1}>
              {a.titulo}
              {!a.leidaEn && <Text span c="red" size="xs"> · sin leer</Text>}
            </Text>
            <Text size="xs" c="dimmed" lineClamp={2}>
              {texto(textos.severidad, a.severidad)} · {instante(a.creadaEn)}
            </Text>
          </div>
        </Group>
      ))}
    </Stack>
  );
}
