import { Alert, Button, Group, Paper, SegmentedControl, Select, SimpleGrid, Stack, Table, Tabs, Text, TextInput } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconFileExport, IconReportAnalytics } from '@tabler/icons-react';
import { keepPreviousData, useMutation, useQuery, type UseQueryResult } from '@tanstack/react-query';
import { type ReactNode, useState } from 'react';
import { PiePagina, TamanoPagina } from '../administracion/comun';
import { api, datos } from '../api/cliente';
import { Consulta, Dato, Encabezado, EstadoVacio, InsigniaSolicitud, InsigniaViaje } from '../comun/Componentes';
import { hoyIso, instante } from '../comun/fechas';
import { texto, textos } from '../textos';

type Tipo = 'Viajes' | 'Solicitudes' | 'Conductores' | 'Empresas';

/** Filtros comunes a las cuatro pestañas; el periodo son días locales completos. */
interface Filtros {
  desde: string;
  hasta: string;
  empresaClienteId: string | null;
  grupo: string | null;
}

const numero = new Intl.NumberFormat('es-CO', { maximumFractionDigits: 1 });
const cifra = (valor: number | null | undefined) => (valor == null ? '—' : numero.format(valor));
const porcentaje = (valor: number | null | undefined) => (valor == null ? '—' : `${numero.format(valor)} %`);

/** `yyyy-MM-dd` de hace `dias` días (zona del navegador). */
function haceDias(dias: number, ahora: Date = new Date()): string {
  return hoyIso(new Date(ahora.getFullYear(), ahora.getMonth(), ahora.getDate() - dias));
}

/**
 * Del día inicial al final, ambos incluidos, en la zona del navegador: la API recibe instantes con "hasta" excluido
 * (medianoche del día siguiente), como el tablero.
 */
export function periodoApi(desde: string, hasta: string): { desde: string; hasta: string } {
  const local = (iso: string, mas = 0) => {
    const [a, m, d] = iso.split('-').map(Number);
    return new Date(a ?? 1970, (m ?? 1) - 1, (d ?? 1) + mas).toISOString();
  };
  return { desde: local(desde), hasta: local(hasta, 1) };
}

/**
 * Reportes del operador (RF-46 a RF-49): viajes, solicitudes, conductores y empresas de un periodo de hasta 93 días, con
 * su resumen, la tabla paginada y el exporte a CSV (enlace temporal al archivo). Las solicitudes personales llegan sin
 * direcciones según el parámetro de visibilidad, igual que en el resto del panel.
 */
export function PaginaReportes() {
  const [desde, setDesde] = useState(haceDias(6));
  const [hasta, setHasta] = useState(hoyIso());
  const [empresaId, setEmpresaId] = useState<string | null>(null);
  const [grupo, setGrupo] = useState<string>('todas');
  const [pestana, setPestana] = useState<Tipo>('Viajes');
  const filtros: Filtros = { desde, hasta, empresaClienteId: empresaId, grupo: grupo === 'todas' ? null : grupo };
  // Cambiar un filtro vuelve a la primera página de cada reporte.
  const clave = JSON.stringify(filtros);

  const empresas = useQuery({
    queryKey: ['empresas', 'todas'],
    queryFn: () => datos(api.GET('/api/empresas-cliente', { params: { query: { TamanoPagina: 200 } } })),
    select: (p) => p.elementos,
  });

  return (
    <>
      <Encabezado
        titulo={textos.menu.reportes}
        descripcion="Lo que pasó en un periodo: viajes, solicitudes, conductores y empresas. Cada reporte se puede exportar a CSV."
        migas={[{ etiqueta: textos.menu.operacion }, { etiqueta: textos.menu.reportes }]}
      >
        <Exportar tipo={pestana} filtros={filtros} />
      </Encabezado>

      <Group mb="md" align="end" wrap="wrap">
        <TextInput label="Desde" type="date" required value={desde} max={hasta} onChange={(e) => setDesde(e.currentTarget.value)} />
        <TextInput label="Hasta" type="date" required value={hasta} min={desde} onChange={(e) => setHasta(e.currentTarget.value)} />
        <Select
          label="Empresa"
          placeholder="Todas"
          data={(empresas.data ?? []).map((e) => ({ value: e.id, label: e.nombre }))}
          value={empresaId}
          onChange={setEmpresaId}
          searchable
          clearable
          w={240}
        />
      </Group>

      <Tabs value={pestana} onChange={(v) => setPestana((v as Tipo) ?? 'Viajes')} keepMounted={false}>
        <Tabs.List mb="md">
          <Tabs.Tab value="Viajes">Viajes</Tabs.Tab>
          <Tabs.Tab value="Solicitudes">Solicitudes</Tabs.Tab>
          <Tabs.Tab value="Conductores">Conductores</Tabs.Tab>
          <Tabs.Tab value="Empresas">Empresas</Tabs.Tab>
        </Tabs.List>
        <Tabs.Panel value="Viajes">
          <ReporteViajes key={clave} filtros={filtros} />
        </Tabs.Panel>
        <Tabs.Panel value="Solicitudes">
          <SegmentedControl
            aria-label="Grupo de solicitudes"
            mb="md"
            value={grupo}
            onChange={setGrupo}
            data={[
              { value: 'todas', label: 'Todas' },
              ...['Atendida', 'Pendiente', 'Cancelada'].map((g) => ({ value: g, label: texto(textos.grupoSolicitud, g) })),
            ]}
          />
          <ReporteSolicitudes key={clave} filtros={filtros} />
        </Tabs.Panel>
        <Tabs.Panel value="Conductores">
          <ReporteConductores key={clave} filtros={filtros} />
        </Tabs.Panel>
        <Tabs.Panel value="Empresas">
          <ReporteEmpresas key={clave} filtros={filtros} />
        </Tabs.Panel>
      </Tabs>
    </>
  );
}

/** Consulta de un reporte con su página; la clave incluye los filtros para no mezclar periodos. */
function useReporte<T>(tipo: Tipo, filtros: Filtros, pagina: number, pedir: (query: Record<string, unknown>) => Promise<T>): UseQueryResult<T> {
  const periodo = periodoApi(filtros.desde, filtros.hasta);
  return useQuery({
    queryKey: ['reportes', tipo, filtros, pagina],
    queryFn: () =>
      pedir({
        Desde: periodo.desde,
        Hasta: periodo.hasta,
        EmpresaClienteId: filtros.empresaClienteId ?? undefined,
        Grupo: tipo === 'Solicitudes' ? (filtros.grupo ?? undefined) : undefined,
        Pagina: pagina,
        TamanoPagina,
      }),
    enabled: Boolean(filtros.desde && filtros.hasta),
    placeholderData: keepPreviousData,
  });
}

function Resumen({ children }: { children: ReactNode }) {
  return (
    <Paper withBorder radius="md" p="md" mb="md">
      <SimpleGrid cols={{ base: 2, sm: 3, md: 6 }} spacing="md">
        {children}
      </SimpleGrid>
    </Paper>
  );
}

function ReporteViajes({ filtros }: { filtros: Filtros }) {
  const [pagina, setPagina] = useState(1);
  const consulta = useReporte('Viajes', filtros, pagina, (query) => datos(api.GET('/api/reportes/viajes', { params: { query } })));
  return (
    <Consulta consulta={consulta}>
      {(r) => (
        <>
          {r.resumen && (
            <Resumen>
              <Dato etiqueta="Viajes">{cifra(r.resumen.viajes)}</Dato>
              <Dato etiqueta="Puntuales">{porcentaje(r.resumen.porcentajePuntualidad)}</Dato>
              <Dato etiqueta="Retraso promedio">{r.resumen.retrasoPromedioMinutos == null ? '—' : `${cifra(r.resumen.retrasoPromedioMinutos)} min`}</Dato>
              <Dato etiqueta="Incidentes">{cifra(r.resumen.incidentes)}</Dato>
              <Dato etiqueta="Calificados">{cifra(r.resumen.calificados)}</Dato>
              <Dato etiqueta="Calificación promedio">{cifra(r.resumen.calificacionPromedio)}</Dato>
            </Resumen>
          )}
          {r.filas.elementos.length === 0 ? (
            <SinFilas />
          ) : (
            <>
              <Table.ScrollContainer minWidth={860}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Inicio programado</Table.Th>
                      <Table.Th>Pasajero</Table.Th>
                      <Table.Th>Empresa</Table.Th>
                      <Table.Th>Conductor</Table.Th>
                      <Table.Th>Estado</Table.Th>
                      <Table.Th>Puntualidad</Table.Th>
                      <Table.Th>Calificación</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {r.filas.elementos.map(({ viaje: v, puntual, minutosRetraso }) => (
                      <Table.Tr key={v.id}>
                        <Table.Td>{instante(v.inicioProgramado)}</Table.Td>
                        <Table.Td>{v.pasajeroNombre}</Table.Td>
                        <Table.Td>{v.empresaClienteNombre ?? '—'}</Table.Td>
                        <Table.Td>
                          {v.conductorNombre} · {v.placa}
                        </Table.Td>
                        <Table.Td>
                          <InsigniaViaje estado={v.estado} />
                        </Table.Td>
                        <Table.Td>{v.fechaHoraInicio ? (puntual ? 'Puntual' : `${minutosRetraso} min tarde`) : '—'}</Table.Td>
                        <Table.Td>{cifra(v.puntaje)}</Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
              <PiePagina total={r.filas.total} pagina={pagina} alCambiar={setPagina} unidad="viaje(s)" />
            </>
          )}
        </>
      )}
    </Consulta>
  );
}

function ReporteSolicitudes({ filtros }: { filtros: Filtros }) {
  const [pagina, setPagina] = useState(1);
  const consulta = useReporte('Solicitudes', filtros, pagina, (query) => datos(api.GET('/api/reportes/solicitudes', { params: { query } })));
  return (
    <Consulta consulta={consulta}>
      {(r) => (
        <>
          {r.resumen && (
            <Resumen>
              <Dato etiqueta="Solicitudes">{cifra(r.resumen.total)}</Dato>
              <Dato etiqueta="Atendidas">{cifra(r.resumen.atendidas)}</Dato>
              <Dato etiqueta="Pendientes">{cifra(r.resumen.pendientes)}</Dato>
              <Dato etiqueta="Canceladas">{cifra(r.resumen.canceladas)}</Dato>
              <Dato etiqueta="Atención">{porcentaje(r.resumen.porcentajeAtencion)}</Dato>
            </Resumen>
          )}
          {r.filas.elementos.length === 0 ? (
            <SinFilas />
          ) : (
            <>
              <Table.ScrollContainer minWidth={860}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Servicio</Table.Th>
                      <Table.Th>Pasajero</Table.Th>
                      <Table.Th>Empresa</Table.Th>
                      <Table.Th>Conductor</Table.Th>
                      <Table.Th>Estado</Table.Th>
                      <Table.Th>Grupo</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {r.filas.elementos.map(({ solicitud: s, grupo }) => (
                      <Table.Tr key={s.id}>
                        <Table.Td>
                          {instante(s.fechaHoraServicio)}
                          {s.urgente && (
                            <Text size="xs" c="red" fw={600}>
                              Urgente
                            </Text>
                          )}
                        </Table.Td>
                        <Table.Td>{s.pasajeroNombre}</Table.Td>
                        <Table.Td>{s.empresaClienteNombre ?? '—'}</Table.Td>
                        <Table.Td>{s.conductorNombre ? `${s.conductorNombre} · ${s.placa ?? ''}` : '—'}</Table.Td>
                        <Table.Td>
                          <InsigniaSolicitud estado={s.estado} />
                        </Table.Td>
                        <Table.Td>{texto(textos.grupoSolicitud, grupo)}</Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
              <PiePagina total={r.filas.total} pagina={pagina} alCambiar={setPagina} unidad="solicitud(es)" />
            </>
          )}
        </>
      )}
    </Consulta>
  );
}

function ReporteConductores({ filtros }: { filtros: Filtros }) {
  const [pagina, setPagina] = useState(1);
  const consulta = useReporte('Conductores', filtros, pagina, (query) => datos(api.GET('/api/reportes/conductores', { params: { query } })));
  return (
    <Consulta consulta={consulta}>
      {(r) => (
        <>
          {r.resumen && (
            <Resumen>
              <Dato etiqueta="Conductores">{cifra(r.resumen.conductores)}</Dato>
              <Dato etiqueta="Asignaciones">{cifra(r.resumen.asignaciones)}</Dato>
              <Dato etiqueta="Aceptadas">{cifra(r.resumen.aceptadas)}</Dato>
              <Dato etiqueta="Rechazadas">{cifra(r.resumen.rechazadas)}</Dato>
              <Dato etiqueta="Vencidas sin respuesta">{cifra(r.resumen.vencidas)}</Dato>
              <Dato etiqueta="Viajes realizados">{cifra(r.resumen.viajesRealizados)}</Dato>
            </Resumen>
          )}
          {r.filas.elementos.length === 0 ? (
            <SinFilas />
          ) : (
            <>
              <Table.ScrollContainer minWidth={900}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Conductor</Table.Th>
                      <Table.Th>Asignaciones</Table.Th>
                      <Table.Th>Aceptación</Table.Th>
                      <Table.Th>Rechazadas</Table.Th>
                      <Table.Th>Vencidas</Table.Th>
                      <Table.Th>Viajes</Table.Th>
                      <Table.Th>Puntualidad</Table.Th>
                      <Table.Th>Incidentes</Table.Th>
                      <Table.Th>Calificación</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {r.filas.elementos.map((c) => (
                      <Table.Tr key={c.conductorId}>
                        <Table.Td>
                          <Text size="sm">{c.nombre}</Text>
                          <Text size="xs" c="var(--fr-texto-suave)">
                            {c.placas || '—'}
                          </Text>
                        </Table.Td>
                        <Table.Td>{c.asignaciones}</Table.Td>
                        <Table.Td>{porcentaje(c.porcentajeAceptacion)}</Table.Td>
                        <Table.Td>{c.rechazadas}</Table.Td>
                        <Table.Td>{c.vencidas}</Table.Td>
                        <Table.Td>{c.viajesRealizados}</Table.Td>
                        <Table.Td>{porcentaje(c.porcentajePuntualidad)}</Table.Td>
                        <Table.Td>{c.incidentes}</Table.Td>
                        <Table.Td>{cifra(c.calificacionPromedio)}</Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
              <PiePagina total={r.filas.total} pagina={pagina} alCambiar={setPagina} unidad="conductor(es)" />
            </>
          )}
        </>
      )}
    </Consulta>
  );
}

function ReporteEmpresas({ filtros }: { filtros: Filtros }) {
  const [pagina, setPagina] = useState(1);
  const consulta = useReporte('Empresas', filtros, pagina, (query) => datos(api.GET('/api/reportes/empresas', { params: { query } })));
  return (
    <Consulta consulta={consulta}>
      {(r) => (
        <>
          {r.resumen && (
            <Resumen>
              <Dato etiqueta="Empresas">{cifra(r.resumen.empresas)}</Dato>
              <Dato etiqueta="Solicitudes">{cifra(r.resumen.solicitudes)}</Dato>
              <Dato etiqueta="Viajes realizados">{cifra(r.resumen.viajesRealizados)}</Dato>
            </Resumen>
          )}
          {r.filas.elementos.length === 0 ? (
            <SinFilas />
          ) : (
            <>
              <Table.ScrollContainer minWidth={900}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Empresa</Table.Th>
                      <Table.Th>Solicitudes</Table.Th>
                      <Table.Th>Atendidas</Table.Th>
                      <Table.Th>Pendientes</Table.Th>
                      <Table.Th>Canceladas</Table.Th>
                      <Table.Th>Urgentes</Table.Th>
                      <Table.Th>Pasajeros</Table.Th>
                      <Table.Th>Viajes</Table.Th>
                      <Table.Th>Calificación</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {r.filas.elementos.map((e) => (
                      <Table.Tr key={e.empresaClienteId ?? 'personales'}>
                        <Table.Td>{e.nombre}</Table.Td>
                        <Table.Td>{e.solicitudes}</Table.Td>
                        <Table.Td>{e.atendidas}</Table.Td>
                        <Table.Td>{e.pendientes}</Table.Td>
                        <Table.Td>{e.canceladas}</Table.Td>
                        <Table.Td>{e.urgentes}</Table.Td>
                        <Table.Td>{e.pasajeros}</Table.Td>
                        <Table.Td>{e.viajesRealizados}</Table.Td>
                        <Table.Td>{cifra(e.calificacionPromedio)}</Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
              <PiePagina total={r.filas.total} pagina={pagina} alCambiar={setPagina} unidad="empresa(s)" />
            </>
          )}
        </>
      )}
    </Consulta>
  );
}

function SinFilas() {
  return <EstadoVacio icono={IconReportAnalytics} titulo="Sin datos" descripcion="No hay registros en ese periodo con esos filtros." />;
}

/** Exporta el reporte de la pestaña abierta con los mismos filtros; el CSV se descarga desde un enlace temporal. */
function Exportar({ tipo, filtros }: { tipo: Tipo; filtros: Filtros }) {
  const exportar = useMutation({
    mutationFn: () => {
      const periodo = periodoApi(filtros.desde, filtros.hasta);
      return datos(
        api.POST('/api/reportes/{tipo}/exportar', {
          params: { path: { tipo } },
          body: {
            desde: periodo.desde,
            hasta: periodo.hasta,
            empresaClienteId: filtros.empresaClienteId,
            grupo: tipo === 'Solicitudes' ? filtros.grupo : null,
          },
        }),
      );
    },
    onSuccess: (e) => {
      notifications.show({ color: 'green', message: `Reporte exportado: ${e.filas} fila(s) en ${e.nombreArchivo}.` });
      window.open(e.url, '_blank', 'noopener,noreferrer');
    },
  });

  return (
    <Stack gap={4} align="end">
      <Button leftSection={<IconFileExport size={16} aria-hidden />} loading={exportar.isPending} onClick={() => exportar.mutate()}>
        Exportar {tipo.toLowerCase()} a CSV
      </Button>
      {exportar.isError && (
        <Alert color="red" role="alert" p="xs">
          {exportar.error.message}
        </Alert>
      )}
    </Stack>
  );
}
