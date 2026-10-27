import { Badge, Button, Checkbox, Group, Select, SimpleGrid, Table, Text, TextInput } from '@mantine/core';
import { useDebouncedValue, useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconCalendarRepeat, IconPlus, IconSearch } from '@tabler/icons-react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos, type Esquemas } from '../api/cliente';
import { Consulta, Encabezado, EstadoVacio, Recorrido } from '../comun/Componentes';
import { fechaCorta, hoyIso } from '../comun/fechas';
import { textos } from '../textos';
import { PanelLateral, PiePagina, TamanoPagina } from './comun';

const DiasSemana = ['lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado', 'domingo'] as const;

/** `06:00:00` → `06:00` (la API guarda horas sin fecha ni desfase, en la zona del itinerario). */
const horaCorta = (hora: string) => hora.slice(0, 5);
/** `06:00` del campo de hora → `06:00:00`, el formato de hora de la API. */
const horaApi = (hora: string) => (hora.length === 5 ? `${hora}:00` : hora);

/** Las empresas cliente, para filtrar y elegir (son pocas: una sola página basta). */
function useEmpresas() {
  return useQuery({
    queryKey: ['empresas', 'todas'],
    queryFn: () => datos(api.GET('/api/empresas-cliente', { params: { query: { TamanoPagina: 200 } } })),
    select: (p) => p.elementos,
  });
}

/**
 * Itinerarios (RF-08): los servicios programados que contrata una empresa cliente (por ejemplo, el traslado de una
 * tripulación), uno por fecha. Se registran desde el panel, no por WhatsApp (recordatorio del 2026-10-18). El
 * Administrador y el Operador logístico los registran y consultan.
 */
export function PaginaItinerarios() {
  const cliente = useQueryClient();
  const [empresaId, setEmpresaId] = useState<string | null>(null);
  const [codigo, setCodigo] = useState('');
  const [codigoBuscado] = useDebouncedValue(codigo, 300);
  const [desde, setDesde] = useState(hoyIso());
  const [hasta, setHasta] = useState('');
  const [pagina, setPagina] = useState(1);
  const [version, setVersion] = useState(0);
  const [panel, { open: abrirPanel, close: cerrarPanel }] = useDisclosure();

  const empresas = useEmpresas();
  const nombreEmpresa = new Map((empresas.data ?? []).map((e) => [e.id, e.nombre]));
  const consulta = useQuery({
    queryKey: ['itinerarios', 'lista', empresaId, codigoBuscado, desde, hasta, pagina],
    queryFn: () =>
      datos(
        api.GET('/api/itinerarios', {
          params: {
            query: {
              EmpresaClienteId: empresaId ?? undefined,
              CodigoServicio: codigoBuscado.trim() || undefined,
              Desde: desde || undefined,
              Hasta: hasta || undefined,
              Pagina: pagina,
              TamanoPagina,
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
  const filtro = (aplicar: () => void) => {
    aplicar();
    setPagina(1);
  };

  return (
    <>
      <Encabezado
        titulo={textos.menu.itinerarios}
        descripcion="Los servicios programados que contrata cada empresa, uno por fecha. Las solicitudes de sus pasajeros pueden asociarse a uno."
        migas={[{ etiqueta: textos.menu.administracion }, { etiqueta: textos.menu.itinerarios }]}
      >
        <Button
          leftSection={<IconPlus size={16} aria-hidden />}
          onClick={() => {
            setVersion((v) => v + 1);
            abrirPanel();
          }}
        >
          Registrar itinerario
        </Button>
      </Encabezado>

      <Group mb="md" align="end" wrap="wrap">
        <Select
          label="Empresa"
          placeholder="Todas"
          data={(empresas.data ?? []).map((e) => ({ value: e.id, label: e.nombre }))}
          value={empresaId}
          onChange={(v) => filtro(() => setEmpresaId(v))}
          searchable
          clearable
          w={240}
        />
        <TextInput
          label="Código de servicio"
          placeholder="Por ejemplo AV-123"
          leftSection={<IconSearch size={16} aria-hidden />}
          value={codigo}
          onChange={(e) => {
            const valor = e.currentTarget.value;
            filtro(() => setCodigo(valor));
          }}
          w={200}
        />
        <TextInput label="Desde" type="date" value={desde} onChange={(e) => {
          const valor = e.currentTarget.value;
          filtro(() => setDesde(valor));
        }} />
        <TextInput label="Hasta" type="date" value={hasta} onChange={(e) => {
          const valor = e.currentTarget.value;
          filtro(() => setHasta(valor));
        }} />
      </Group>

      <Consulta consulta={consulta}>
        {(resultado) =>
          resultado.elementos.length === 0 ? (
            <EstadoVacio icono={IconCalendarRepeat} titulo="Sin itinerarios" descripcion="No hay itinerarios con esos filtros." />
          ) : (
            <>
              <Table.ScrollContainer minWidth={760}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Fecha</Table.Th>
                      <Table.Th>Servicio</Table.Th>
                      <Table.Th>Empresa</Table.Th>
                      <Table.Th>Recorrido</Table.Th>
                      <Table.Th>Horario</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {resultado.elementos.map((i) => (
                      <Table.Tr key={i.id}>
                        <Table.Td>{fechaCorta(i.fecha)}</Table.Td>
                        <Table.Td>
                          <Badge variant="default" radius="sm">
                            {i.codigoServicio}
                          </Badge>
                        </Table.Td>
                        <Table.Td>{nombreEmpresa.get(i.empresaClienteId) ?? '—'}</Table.Td>
                        <Table.Td>
                          <Recorrido origen={i.origen} destino={i.destino} />
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm">
                            {horaCorta(i.horaSalida)} – {horaCorta(i.horaLlegada)}
                          </Text>
                          <Text size="xs" c="var(--fr-texto-suave)">
                            {i.zonaHoraria}
                          </Text>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
              <PiePagina total={resultado.total} pagina={pagina} alCambiar={setPagina} unidad="itinerario(s)" />
            </>
          )
        }
      </Consulta>

      <FormularioItinerario
        key={version}
        abierto={panel}
        alCerrar={cerrarPanel}
        empresas={(empresas.data ?? []).filter((e) => e.activa)}
        alGuardar={() => void cliente.invalidateQueries({ queryKey: ['itinerarios'] })}
      />
    </>
  );
}

/** Un servicio para un rango de fechas y días de la semana: la API crea un itinerario por cada fecha que coincide. */
function FormularioItinerario({
  abierto,
  alCerrar,
  empresas,
  alGuardar,
}: {
  abierto: boolean;
  alCerrar: () => void;
  empresas: Esquemas['EmpresaClienteRespuesta'][];
  alGuardar: () => void;
}) {
  const [empresaId, setEmpresaId] = useState<string | null>(null);
  const [codigo, setCodigo] = useState('');
  const [origen, setOrigen] = useState('');
  const [destino, setDestino] = useState('');
  const [salida, setSalida] = useState('');
  const [llegada, setLlegada] = useState('');
  const [desde, setDesde] = useState('');
  const [hasta, setHasta] = useState('');
  const [dias, setDias] = useState<string[]>([...DiasSemana]);

  const registrar = useMutation({
    mutationFn: () =>
      datos(
        api.POST('/api/itinerarios', {
          body: {
            empresaClienteId: empresaId,
            codigoServicio: codigo,
            origen,
            destino,
            horaSalida: salida ? horaApi(salida) : null,
            horaLlegada: llegada ? horaApi(llegada) : null,
            fechaDesde: desde || null,
            fechaHasta: hasta || null,
            dias,
          },
        }),
      ),
    onSuccess: (creados) => {
      notifications.show({
        color: 'green',
        message: creados.length === 1 ? 'Se registró 1 itinerario.' : `Se registraron ${creados.length} itinerarios, uno por fecha.`,
      });
      alGuardar();
      alCerrar();
    },
  });

  return (
    <PanelLateral
      abierto={abierto}
      alCerrar={alCerrar}
      titulo="Registrar itinerario"
      descripcion="Las horas son locales, en la zona del operador. Si alguna de las fechas ya tiene ese código de servicio, no se registra ninguna."
      mutacion={registrar}
      alEnviar={() => registrar.mutate()}
      textoEnviar="Registrar"
    >
      <Select
        label="Empresa"
        data={empresas.map((e) => ({ value: e.id, label: e.nombre }))}
        value={empresaId}
        onChange={setEmpresaId}
        searchable
        required
        nothingFoundMessage="No hay empresas activas"
      />
      <TextInput label="Código de servicio" description="El que usa la empresa (vuelo, ruta, turno…)" required value={codigo} onChange={(e) => setCodigo(e.currentTarget.value)} />
      <TextInput label="Origen" required value={origen} onChange={(e) => setOrigen(e.currentTarget.value)} />
      <TextInput label="Destino" required value={destino} onChange={(e) => setDestino(e.currentTarget.value)} />
      <Group grow>
        <TextInput label="Hora de salida" type="time" required value={salida} onChange={(e) => setSalida(e.currentTarget.value)} />
        <TextInput label="Hora de llegada" type="time" required value={llegada} onChange={(e) => setLlegada(e.currentTarget.value)} />
      </Group>
      <Group grow>
        <TextInput label="Desde" type="date" required value={desde} onChange={(e) => setDesde(e.currentTarget.value)} />
        <TextInput label="Hasta" type="date" description="Vacío: solo la fecha inicial" value={hasta} onChange={(e) => setHasta(e.currentTarget.value)} />
      </Group>
      <Checkbox.Group label="Días de la semana" description="Solo se crean las fechas del rango que caen en estos días." value={dias} onChange={setDias}>
        <SimpleGrid cols={4} spacing="xs" mt="xs">
          {DiasSemana.map((d) => (
            <Checkbox key={d} value={d} label={d.charAt(0).toUpperCase() + d.slice(1)} />
          ))}
        </SimpleGrid>
      </Checkbox.Group>
    </PanelLateral>
  );
}
