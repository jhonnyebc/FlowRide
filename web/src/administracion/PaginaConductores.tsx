import { Button, Checkbox, Group, Select, Table, TextInput, VisuallyHidden } from '@mantine/core';
import { useDebouncedValue, useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconEdit, IconEye, IconPlus, IconSearch, IconSteeringWheel, IconToggleRight } from '@tabler/icons-react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos, type Esquemas } from '../api/cliente';
import { esAdministrador, useUsuario } from '../auth/usuario';
import { Consulta, Encabezado, EstadoVacio, Persona } from '../comun/Componentes';
import { texto, textos } from '../textos';
import { Confirmacion, InsigniaConductor, InsigniaVencimiento, MenuAcciones, PanelLateral, PiePagina, TamanoPagina } from './comun';
import { DetalleConductor } from './DetalleConductor';

type Conductor = Esquemas['ConductorRespuesta'];

const EstadosConductor = ['Activo', 'Suspendido', 'Inactivo'] as const;

/**
 * Conductores del operador (RF-07, RF-26 a RF-28): licencia, estado, turnos y documentos. El Administrador los registra,
 * edita y cambia de estado; el Operador logístico los consulta y programa sus turnos (decisión del usuario, 2026-10-21).
 */
export function PaginaConductores() {
  const usuario = useUsuario();
  const puedeEditar = esAdministrador(usuario);
  const cliente = useQueryClient();
  const [busqueda, setBusqueda] = useState('');
  const [textoBusqueda] = useDebouncedValue(busqueda, 300);
  const [estado, setEstado] = useState<string | null>('Activo');
  const [pagina, setPagina] = useState(1);
  const [editando, setEditando] = useState<Conductor | null>(null);
  const [version, setVersion] = useState(0);
  const [panel, { open: abrirPanel, close: cerrarPanel }] = useDisclosure();
  const [detalle, setDetalle] = useState<Conductor | null>(null);
  const [porCambiar, setPorCambiar] = useState<Conductor | null>(null);

  const consulta = useQuery({
    queryKey: ['conductores', 'lista', textoBusqueda, estado, pagina],
    queryFn: () =>
      datos(
        api.GET('/api/conductores', {
          params: { query: { Texto: textoBusqueda || undefined, Estado: estado ?? undefined, Pagina: pagina, TamanoPagina } },
        }),
      ),
    placeholderData: keepPreviousData,
  });
  const refrescar = () => void cliente.invalidateQueries({ queryKey: ['conductores'] });

  const abrir = (conductor: Conductor | null) => {
    setEditando(conductor);
    setVersion((v) => v + 1);
    abrirPanel();
  };

  return (
    <>
      <Encabezado
        titulo={textos.menu.conductores}
        descripcion="Quienes atienden los servicios: el motor solo asigna conductores activos con un turno que cubra el servicio."
        migas={[{ etiqueta: textos.menu.administracion }, { etiqueta: textos.menu.conductores }]}
      >
        {puedeEditar && (
          <Button leftSection={<IconPlus size={16} aria-hidden />} onClick={() => abrir(null)}>
            Nuevo conductor
          </Button>
        )}
      </Encabezado>

      <Group mb="md" align="end" wrap="wrap">
        <TextInput
          label="Buscar"
          placeholder="Nombre, licencia o WhatsApp"
          leftSection={<IconSearch size={16} aria-hidden />}
          value={busqueda}
          onChange={(e) => {
            setBusqueda(e.currentTarget.value);
            setPagina(1);
          }}
          w={280}
        />
        <Select
          label="Estado"
          placeholder="Todos"
          data={EstadosConductor.map((e) => ({ value: e, label: texto(textos.estadoConductor, e) }))}
          value={estado}
          onChange={(v) => {
            setEstado(v);
            setPagina(1);
          }}
          clearable
          w={200}
        />
      </Group>

      <Consulta consulta={consulta}>
        {(resultado) =>
          resultado.elementos.length === 0 ? (
            <EstadoVacio icono={IconSteeringWheel} titulo="Sin conductores" descripcion="No hay conductores con esos filtros." />
          ) : (
            <>
              <Table.ScrollContainer minWidth={760}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Conductor</Table.Th>
                      <Table.Th>WhatsApp</Table.Th>
                      <Table.Th>Licencia</Table.Th>
                      <Table.Th>Vence la licencia</Table.Th>
                      <Table.Th>Estado</Table.Th>
                      <Table.Th>
                        <VisuallyHidden>Acciones</VisuallyHidden>
                      </Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {resultado.elementos.map((c) => (
                      <Table.Tr key={c.id}>
                        <Table.Td>
                          <Persona nombre={c.nombre} tamano="sm" detalle={c.aptoServicioCritico ? 'Apto para servicios críticos' : (c.correo ?? undefined)} />
                        </Table.Td>
                        <Table.Td>{c.telefonoWhatsApp}</Table.Td>
                        <Table.Td>
                          {c.numeroLicencia} · {c.categoriaLicencia}
                        </Table.Td>
                        <Table.Td>
                          <InsigniaVencimiento fecha={c.vencimientoLicencia} />
                        </Table.Td>
                        <Table.Td>
                          <InsigniaConductor estado={c.estado} />
                        </Table.Td>
                        <Table.Td>
                          <MenuAcciones
                            de={c.nombre}
                            acciones={[
                              { etiqueta: 'Turnos y documentos', icono: <IconEye size={16} aria-hidden />, alElegir: () => setDetalle(c) },
                              ...(puedeEditar
                                ? [
                                    { etiqueta: 'Editar', icono: <IconEdit size={16} aria-hidden />, alElegir: () => abrir(c) },
                                    { etiqueta: 'Cambiar estado', icono: <IconToggleRight size={16} aria-hidden />, alElegir: () => setPorCambiar(c) },
                                  ]
                                : []),
                            ]}
                          />
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
              <PiePagina total={resultado.total} pagina={pagina} alCambiar={setPagina} unidad="conductor(es)" />
            </>
          )
        }
      </Consulta>

      {puedeEditar && (
        <FormularioConductor key={`${editando?.id ?? 'nuevo'}-${version}`} abierto={panel} alCerrar={cerrarPanel} conductor={editando} alGuardar={refrescar} />
      )}
      {detalle && <DetalleConductor conductor={detalle} alCerrar={() => setDetalle(null)} />}
      {porCambiar && <CambiarEstado conductor={porCambiar} alCerrar={() => setPorCambiar(null)} alGuardar={refrescar} />}
    </>
  );
}

function FormularioConductor({
  abierto,
  alCerrar,
  conductor,
  alGuardar,
}: {
  abierto: boolean;
  alCerrar: () => void;
  conductor: Conductor | null;
  alGuardar: () => void;
}) {
  const [nombre, setNombre] = useState(conductor?.nombre ?? '');
  const [telefono, setTelefono] = useState(conductor?.telefonoWhatsApp ?? '');
  const [correo, setCorreo] = useState(conductor?.correo ?? '');
  const [licencia, setLicencia] = useState(conductor?.numeroLicencia ?? '');
  const [categoria, setCategoria] = useState(conductor?.categoriaLicencia ?? '');
  const [vencimiento, setVencimiento] = useState(conductor?.vencimientoLicencia ?? '');
  const [critico, setCritico] = useState(conductor?.aptoServicioCritico ?? false);

  const guardar = useMutation({
    mutationFn: () => {
      const body = {
        nombre,
        telefonoWhatsApp: telefono,
        correo: correo.trim() || null,
        numeroLicencia: licencia,
        categoriaLicencia: categoria,
        vencimientoLicencia: vencimiento || null,
        aptoServicioCritico: critico,
      };
      return conductor
        ? datos(api.PUT('/api/conductores/{id}', { params: { path: { id: conductor.id } }, body }))
        : datos(api.POST('/api/conductores', { body }));
    },
    onSuccess: (c) => {
      notifications.show({ color: 'green', message: conductor ? 'Conductor actualizado.' : `${c.nombre} quedó registrado. Prográmale un turno para que reciba servicios.` });
      alGuardar();
      alCerrar();
    },
  });

  return (
    <PanelLateral
      abierto={abierto}
      alCerrar={alCerrar}
      titulo={conductor ? `Editar a ${conductor.nombre}` : 'Nuevo conductor'}
      descripcion="El bot de WhatsApp lo reconoce por su número exacto: escríbelo en formato internacional."
      mutacion={guardar}
      alEnviar={() => guardar.mutate()}
      textoEnviar={conductor ? 'Guardar cambios' : 'Registrar conductor'}
    >
      <TextInput label="Nombre completo" required value={nombre} onChange={(e) => setNombre(e.currentTarget.value)} />
      <TextInput label="WhatsApp" description="Por ejemplo +573001112233" required value={telefono} onChange={(e) => setTelefono(e.currentTarget.value)} />
      <TextInput label="Correo" description="Opcional" type="email" value={correo} onChange={(e) => setCorreo(e.currentTarget.value)} />
      <Group grow>
        <TextInput label="Número de licencia" required value={licencia} onChange={(e) => setLicencia(e.currentTarget.value)} />
        <TextInput label="Categoría" description="Por ejemplo C1" required value={categoria} onChange={(e) => setCategoria(e.currentTarget.value.toUpperCase())} />
      </Group>
      <TextInput label="Vence la licencia" type="date" required value={vencimiento} onChange={(e) => setVencimiento(e.currentTarget.value)} />
      <Checkbox
        label="Apto para servicios críticos"
        description="En los servicios urgentes el motor le da prioridad sobre los demás conductores."
        checked={critico}
        onChange={(e) => setCritico(e.currentTarget.checked)}
      />
    </PanelLateral>
  );
}

/** Activo, suspendido o inactivo: un conductor que no está activo deja de recibir asignaciones nuevas. */
function CambiarEstado({ conductor, alCerrar, alGuardar }: { conductor: Conductor; alCerrar: () => void; alGuardar: () => void }) {
  const [estado, setEstado] = useState<string | null>(conductor.estado);
  const cambiar = useMutation({
    mutationFn: () => datos(api.PUT('/api/conductores/{id}/estado', { params: { path: { id: conductor.id } }, body: { estado } })),
    onSuccess: (c) => {
      notifications.show({ color: 'green', message: `${c.nombre} quedó ${texto(textos.estadoConductor, c.estado).toLowerCase()}.` });
      alGuardar();
      alCerrar();
    },
  });

  return (
    <Confirmacion
      abierta
      alCerrar={alCerrar}
      titulo={`Estado de ${conductor.nombre}`}
      mensaje="Solo los conductores activos reciben asignaciones nuevas. Las asignaciones que ya tiene no cambian; si deja un turno a medias, regístralo como abandono."
      textoConfirmar="Cambiar estado"
      peligrosa={estado !== 'Activo'}
      mutacion={cambiar}
      alConfirmar={() => cambiar.mutate()}
    >
      <Select
        label="Nuevo estado"
        data={EstadosConductor.map((e) => ({ value: e, label: texto(textos.estadoConductor, e) }))}
        value={estado}
        onChange={setEstado}
        allowDeselect={false}
      />
    </Confirmacion>
  );
}
