import { Badge, Button, Group, NumberInput, SegmentedControl, Select, Table, Text, TextInput, VisuallyHidden } from '@mantine/core';
import { useDebouncedValue, useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconCar, IconCircleCheck, IconCircleOff, IconEdit, IconPlus, IconSearch, IconSteeringWheel } from '@tabler/icons-react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos, type Esquemas } from '../api/cliente';
import { esAdministrador, useUsuario } from '../auth/usuario';
import { Consulta, Encabezado, EstadoVacio } from '../comun/Componentes';
import { textos } from '../textos';
import { Confirmacion, InsigniaActivo, InsigniaVencimiento, MenuAcciones, PanelLateral, PiePagina, TamanoPagina, type FiltroActivo, useConductoresActivos } from './comun';

type Vehiculo = Esquemas['VehiculoRespuesta'];

/**
 * Vehículos del operador (RF-07): capacidad y vencimientos de RTM y SOAT, que el motor revisa antes de asignar
 * (ADR-0009), y el conductor que lo maneja habitualmente. El Administrador los registra y edita; el Operador logístico
 * los consulta.
 */
export function PaginaVehiculos() {
  const usuario = useUsuario();
  const puedeEditar = esAdministrador(usuario);
  const cliente = useQueryClient();
  const [busqueda, setBusqueda] = useState('');
  const [texto] = useDebouncedValue(busqueda, 300);
  const [filtro, setFiltro] = useState<FiltroActivo>('activos');
  const [pagina, setPagina] = useState(1);
  const [editando, setEditando] = useState<Vehiculo | null>(null);
  const [version, setVersion] = useState(0);
  const [panel, { open: abrirPanel, close: cerrarPanel }] = useDisclosure();
  const [porCambiar, setPorCambiar] = useState<Vehiculo | null>(null);
  const [porAsignar, setPorAsignar] = useState<Vehiculo | null>(null);

  const conductores = useConductoresActivos();
  const nombreConductor = new Map((conductores.data ?? []).map((c) => [c.id, c.nombre]));
  const consulta = useQuery({
    queryKey: ['vehiculos', 'lista', texto, filtro, pagina],
    queryFn: () =>
      datos(
        api.GET('/api/vehiculos', {
          params: { query: { Texto: texto || undefined, Activo: filtro === 'todos' ? undefined : filtro === 'activos', Pagina: pagina, TamanoPagina } },
        }),
      ),
    placeholderData: keepPreviousData,
  });
  const refrescar = () => void cliente.invalidateQueries({ queryKey: ['vehiculos'] });

  const cambiarEstado = useMutation({
    mutationFn: (v: Vehiculo) =>
      v.activo
        ? datos(api.POST('/api/vehiculos/{id}/desactivar', { params: { path: { id: v.id } } }))
        : datos(api.POST('/api/vehiculos/{id}/activar', { params: { path: { id: v.id } } })),
    onSuccess: (v) => {
      notifications.show({ color: 'green', message: v.activo ? `El vehículo ${v.placa} está activo.` : `El vehículo ${v.placa} quedó inactivo.` });
      setPorCambiar(null);
      refrescar();
    },
  });

  const abrir = (vehiculo: Vehiculo | null) => {
    setEditando(vehiculo);
    setVersion((v) => v + 1);
    abrirPanel();
  };

  return (
    <>
      <Encabezado
        titulo={textos.menu.vehiculos}
        descripcion="La flota del operador: el motor solo asigna vehículos activos con RTM y SOAT vigentes."
        migas={[{ etiqueta: textos.menu.administracion }, { etiqueta: textos.menu.vehiculos }]}
      >
        {puedeEditar && (
          <Button leftSection={<IconPlus size={16} aria-hidden />} onClick={() => abrir(null)}>
            Nuevo vehículo
          </Button>
        )}
      </Encabezado>

      <Group mb="md" align="end" wrap="wrap">
        <TextInput
          label="Buscar"
          placeholder="Placa, marca o modelo"
          leftSection={<IconSearch size={16} aria-hidden />}
          value={busqueda}
          onChange={(e) => {
            setBusqueda(e.currentTarget.value);
            setPagina(1);
          }}
          w={280}
        />
        <SegmentedControl
          aria-label="Estado"
          value={filtro}
          onChange={(v) => {
            setFiltro(v as FiltroActivo);
            setPagina(1);
          }}
          data={[
            { value: 'activos', label: 'Activos' },
            { value: 'inactivos', label: 'Inactivos' },
            { value: 'todos', label: 'Todos' },
          ]}
        />
      </Group>

      <Consulta consulta={consulta}>
        {(resultado) =>
          resultado.elementos.length === 0 ? (
            <EstadoVacio icono={IconCar} titulo="Sin vehículos" descripcion="No hay vehículos con esos filtros." />
          ) : (
            <>
              <Table.ScrollContainer minWidth={760}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Placa</Table.Th>
                      <Table.Th>Vehículo</Table.Th>
                      <Table.Th>Capacidad</Table.Th>
                      <Table.Th>RTM</Table.Th>
                      <Table.Th>SOAT</Table.Th>
                      <Table.Th>Conductor habitual</Table.Th>
                      <Table.Th>Estado</Table.Th>
                      {puedeEditar && (
                        <Table.Th>
                          <VisuallyHidden>Acciones</VisuallyHidden>
                        </Table.Th>
                      )}
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {resultado.elementos.map((v) => (
                      <Table.Tr key={v.id}>
                        <Table.Td>
                          <Badge variant="default" radius="sm" size="lg">
                            {v.placa}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          {v.marca} {v.modelo}
                        </Table.Td>
                        <Table.Td>{v.capacidad} puestos</Table.Td>
                        <Table.Td>
                          <InsigniaVencimiento fecha={v.vencimientoRtm} />
                        </Table.Td>
                        <Table.Td>
                          <InsigniaVencimiento fecha={v.vencimientoSoat} />
                        </Table.Td>
                        <Table.Td>{v.conductorHabitualId ? (nombreConductor.get(v.conductorHabitualId) ?? 'Conductor no activo') : '—'}</Table.Td>
                        <Table.Td>
                          <InsigniaActivo activo={v.activo} />
                        </Table.Td>
                        {puedeEditar && (
                          <Table.Td>
                            <MenuAcciones
                              de={`el vehículo ${v.placa}`}
                              acciones={[
                                { etiqueta: 'Editar', icono: <IconEdit size={16} aria-hidden />, alElegir: () => abrir(v) },
                                {
                                  etiqueta: 'Conductor habitual',
                                  icono: <IconSteeringWheel size={16} aria-hidden />,
                                  deshabilitada: !v.activo,
                                  alElegir: () => setPorAsignar(v),
                                },
                                {
                                  etiqueta: v.activo ? 'Desactivar' : 'Activar',
                                  icono: v.activo ? <IconCircleOff size={16} aria-hidden /> : <IconCircleCheck size={16} aria-hidden />,
                                  peligrosa: v.activo,
                                  alElegir: () => {
                                    cambiarEstado.reset();
                                    setPorCambiar(v);
                                  },
                                },
                              ]}
                            />
                          </Table.Td>
                        )}
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
              <PiePagina total={resultado.total} pagina={pagina} alCambiar={setPagina} unidad="vehículo(s)" />
            </>
          )
        }
      </Consulta>

      {puedeEditar && (
        <FormularioVehiculo key={`${editando?.id ?? 'nuevo'}-${version}`} abierto={panel} alCerrar={cerrarPanel} vehiculo={editando} alGuardar={refrescar} />
      )}
      {porAsignar && (
        <ConductorHabitual
          vehiculo={porAsignar}
          conductores={conductores.data ?? []}
          alCerrar={() => setPorAsignar(null)}
          alGuardar={refrescar}
        />
      )}
      {porCambiar && (
        <Confirmacion
          abierta
          alCerrar={() => setPorCambiar(null)}
          titulo={porCambiar.activo ? 'Desactivar vehículo' : 'Activar vehículo'}
          mensaje={
            porCambiar.activo
              ? `El motor dejará de asignar el vehículo ${porCambiar.placa}. Los viajes que ya tiene asignados no cambian.`
              : `El motor podrá volver a asignar el vehículo ${porCambiar.placa}.`
          }
          textoConfirmar={porCambiar.activo ? 'Desactivar' : 'Activar'}
          peligrosa={porCambiar.activo}
          mutacion={cambiarEstado}
          alConfirmar={() => cambiarEstado.mutate(porCambiar)}
        />
      )}
    </>
  );
}

function FormularioVehiculo({
  abierto,
  alCerrar,
  vehiculo,
  alGuardar,
}: {
  abierto: boolean;
  alCerrar: () => void;
  vehiculo: Vehiculo | null;
  alGuardar: () => void;
}) {
  const [placa, setPlaca] = useState(vehiculo?.placa ?? '');
  const [marca, setMarca] = useState(vehiculo?.marca ?? '');
  const [modelo, setModelo] = useState(vehiculo?.modelo ?? '');
  const [capacidad, setCapacidad] = useState<number | string>(vehiculo?.capacidad ?? 4);
  const [rtm, setRtm] = useState(vehiculo?.vencimientoRtm ?? '');
  const [soat, setSoat] = useState(vehiculo?.vencimientoSoat ?? '');

  const guardar = useMutation({
    mutationFn: () => {
      const body = { placa, marca, modelo, capacidad: Number(capacidad), vencimientoRtm: rtm || null, vencimientoSoat: soat || null };
      return vehiculo
        ? datos(api.PUT('/api/vehiculos/{id}', { params: { path: { id: vehiculo.id } }, body }))
        : datos(api.POST('/api/vehiculos', { body }));
    },
    onSuccess: (v) => {
      notifications.show({ color: 'green', message: vehiculo ? 'Vehículo actualizado.' : `El vehículo ${v.placa} quedó registrado.` });
      alGuardar();
      alCerrar();
    },
  });

  return (
    <PanelLateral
      abierto={abierto}
      alCerrar={alCerrar}
      titulo={vehiculo ? `Editar el vehículo ${vehiculo.placa}` : 'Nuevo vehículo'}
      descripcion="El motor no asigna un vehículo con la RTM o el SOAT vencidos."
      mutacion={guardar}
      alEnviar={() => guardar.mutate()}
      textoEnviar={vehiculo ? 'Guardar cambios' : 'Registrar vehículo'}
    >
      <TextInput label="Placa" description="Por ejemplo ABC123" required value={placa} onChange={(e) => setPlaca(e.currentTarget.value.toUpperCase())} />
      <Group grow>
        <TextInput label="Marca" required value={marca} onChange={(e) => setMarca(e.currentTarget.value)} />
        <TextInput label="Modelo" required value={modelo} onChange={(e) => setModelo(e.currentTarget.value)} />
      </Group>
      <NumberInput label="Capacidad (pasajeros)" min={1} max={60} required value={capacidad} onChange={setCapacidad} />
      <Group grow>
        <TextInput label="Vence la RTM" type="date" required value={rtm} onChange={(e) => setRtm(e.currentTarget.value)} />
        <TextInput label="Vence el SOAT" type="date" required value={soat} onChange={(e) => setSoat(e.currentTarget.value)} />
      </Group>
    </PanelLateral>
  );
}

/** Elige el conductor que maneja habitualmente el vehículo (o ninguno). */
function ConductorHabitual({
  vehiculo,
  conductores,
  alCerrar,
  alGuardar,
}: {
  vehiculo: Vehiculo;
  conductores: Esquemas['ConductorRespuesta'][];
  alCerrar: () => void;
  alGuardar: () => void;
}) {
  const [conductorId, setConductorId] = useState<string | null>(vehiculo.conductorHabitualId);
  const asignar = useMutation({
    mutationFn: () =>
      datos(api.PUT('/api/vehiculos/{id}/conductor-habitual', { params: { path: { id: vehiculo.id } }, body: { conductorId } })),
    onSuccess: () => {
      notifications.show({ color: 'green', message: conductorId ? 'Conductor habitual asignado.' : 'El vehículo quedó sin conductor habitual.' });
      alGuardar();
      alCerrar();
    },
  });

  return (
    <PanelLateral
      abierto
      alCerrar={alCerrar}
      titulo={`Conductor habitual de ${vehiculo.placa}`}
      descripcion="El motor asigna a ese conductor siempre este vehículo y no se lo asigna a nadie más. Un conductor tiene a lo sumo un vehículo habitual."
      mutacion={asignar}
      alEnviar={() => asignar.mutate()}
      textoEnviar="Guardar"
    >
      <Select
        label="Conductor"
        placeholder="Sin conductor habitual"
        data={conductores.map((c) => ({ value: c.id, label: c.nombre }))}
        value={conductorId}
        onChange={setConductorId}
        searchable
        clearable
        nothingFoundMessage="No hay conductores activos"
      />
      {conductores.length === 0 && (
        <Text size="sm" c="var(--fr-texto-suave)">
          Primero registra conductores en la sección Conductores.
        </Text>
      )}
    </PanelLateral>
  );
}
