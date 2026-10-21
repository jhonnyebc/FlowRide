import { Button, Group, SegmentedControl, Table, TextInput, VisuallyHidden } from '@mantine/core';
import { useDebouncedValue, useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconBuildingSkyscraper, IconCircleCheck, IconCircleOff, IconEdit, IconPlus, IconSearch } from '@tabler/icons-react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos, type Esquemas } from '../api/cliente';
import { esAdministrador, useUsuario } from '../auth/usuario';
import { Consulta, Encabezado, EstadoVacio, Persona } from '../comun/Componentes';
import { textos } from '../textos';
import { Confirmacion, InsigniaActivo, MenuAcciones, PanelLateral, PiePagina, TamanoPagina } from './comun';

type Empresa = Esquemas['EmpresaClienteRespuesta'];
type Filtro = 'activas' | 'inactivas' | 'todas';

/**
 * Empresas cliente del operador (RF-04): las atiende con sus pasajeros. El Administrador las registra, edita y
 * activa o desactiva; el Operador logístico las consulta.
 */
export function PaginaEmpresas() {
  const usuario = useUsuario();
  const puedeEditar = esAdministrador(usuario);
  const cliente = useQueryClient();
  const [busqueda, setBusqueda] = useState('');
  const [texto] = useDebouncedValue(busqueda, 300);
  const [filtro, setFiltro] = useState<Filtro>('activas');
  const [pagina, setPagina] = useState(1);
  const [editando, setEditando] = useState<Empresa | null>(null);
  const [version, setVersion] = useState(0);
  const [panel, { open: abrirPanel, close: cerrarPanel }] = useDisclosure();
  const [porCambiar, setPorCambiar] = useState<Empresa | null>(null);

  const consulta = useQuery({
    queryKey: ['empresas', 'lista', texto, filtro, pagina],
    queryFn: () =>
      datos(
        api.GET('/api/empresas-cliente', {
          params: { query: { Texto: texto || undefined, Activo: filtro === 'todas' ? undefined : filtro === 'activas', Pagina: pagina, TamanoPagina } },
        }),
      ),
    placeholderData: keepPreviousData,
  });
  const refrescar = () => void cliente.invalidateQueries({ queryKey: ['empresas'] });

  const cambiarEstado = useMutation({
    mutationFn: (e: Empresa) =>
      e.activa
        ? datos(api.POST('/api/empresas-cliente/{id}/desactivar', { params: { path: { id: e.id } } }))
        : datos(api.POST('/api/empresas-cliente/{id}/activar', { params: { path: { id: e.id } } })),
    onSuccess: (e) => {
      notifications.show({ color: 'green', message: e.activa ? `${e.nombre} está activa.` : `${e.nombre} quedó inactiva.` });
      setPorCambiar(null);
      refrescar();
    },
  });

  const abrir = (empresa: Empresa | null) => {
    setEditando(empresa);
    setVersion((v) => v + 1);
    abrirPanel();
  };

  return (
    <>
      <Encabezado
        titulo={textos.menu.empresas}
        descripcion="Los clientes empresariales que atiende este operador."
        migas={[{ etiqueta: textos.menu.administracion }, { etiqueta: textos.menu.empresas }]}
      >
        {puedeEditar && (
          <Button leftSection={<IconPlus size={16} aria-hidden />} onClick={() => abrir(null)}>
            Nueva empresa
          </Button>
        )}
      </Encabezado>

      <Group mb="md" align="end" wrap="wrap">
        <TextInput
          label="Buscar"
          placeholder="Nombre o NIT"
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
            setFiltro(v as Filtro);
            setPagina(1);
          }}
          data={[
            { value: 'activas', label: 'Activas' },
            { value: 'inactivas', label: 'Inactivas' },
            { value: 'todas', label: 'Todas' },
          ]}
        />
      </Group>

      <Consulta consulta={consulta}>
        {(resultado) =>
          resultado.elementos.length === 0 ? (
            <EstadoVacio icono={IconBuildingSkyscraper} titulo="Sin empresas" descripcion="No hay empresas cliente con esos filtros." />
          ) : (
            <>
              <Table.ScrollContainer minWidth={640}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Empresa</Table.Th>
                      <Table.Th>NIT</Table.Th>
                      <Table.Th>Estado</Table.Th>
                      {puedeEditar && (
                        <Table.Th>
                          <VisuallyHidden>Acciones</VisuallyHidden>
                        </Table.Th>
                      )}
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {resultado.elementos.map((e) => (
                      <Table.Tr key={e.id}>
                        <Table.Td>
                          <Persona nombre={e.nombre} tamano="sm" detalle={e.correoContacto} />
                        </Table.Td>
                        <Table.Td>{e.nit}</Table.Td>
                        <Table.Td>
                          <InsigniaActivo activo={e.activa} femenino />
                        </Table.Td>
                        {puedeEditar && (
                          <Table.Td>
                            <MenuAcciones
                              de={e.nombre}
                              acciones={[
                                { etiqueta: 'Editar', icono: <IconEdit size={16} aria-hidden />, alElegir: () => abrir(e) },
                                {
                                  etiqueta: e.activa ? 'Desactivar' : 'Activar',
                                  icono: e.activa ? <IconCircleOff size={16} aria-hidden /> : <IconCircleCheck size={16} aria-hidden />,
                                  peligrosa: e.activa,
                                  alElegir: () => {
                                    cambiarEstado.reset();
                                    setPorCambiar(e);
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
              <PiePagina total={resultado.total} pagina={pagina} alCambiar={setPagina} unidad="empresa(s)" />
            </>
          )
        }
      </Consulta>

      {puedeEditar && (
        <FormularioEmpresa key={`${editando?.id ?? 'nueva'}-${version}`} abierto={panel} alCerrar={cerrarPanel} empresa={editando} alGuardar={refrescar} />
      )}
      {porCambiar && (
        <Confirmacion
          abierta
          alCerrar={() => setPorCambiar(null)}
          titulo={porCambiar.activa ? 'Desactivar empresa' : 'Activar empresa'}
          mensaje={
            porCambiar.activa
              ? `Los pasajeros de ${porCambiar.nombre} no podrán pedir viajes y no se le podrán agregar pasajeros. Las solicitudes que ya existen no cambian.`
              : `Los pasajeros de ${porCambiar.nombre} podrán volver a pedir viajes.`
          }
          textoConfirmar={porCambiar.activa ? 'Desactivar' : 'Activar'}
          peligrosa={porCambiar.activa}
          mutacion={cambiarEstado}
          alConfirmar={() => cambiarEstado.mutate(porCambiar)}
        />
      )}
    </>
  );
}

function FormularioEmpresa({
  abierto,
  alCerrar,
  empresa,
  alGuardar,
}: {
  abierto: boolean;
  alCerrar: () => void;
  empresa: Empresa | null;
  alGuardar: () => void;
}) {
  const [nombre, setNombre] = useState(empresa?.nombre ?? '');
  const [nit, setNit] = useState(empresa?.nit ?? '');
  const [correo, setCorreo] = useState(empresa?.correoContacto ?? '');

  const guardar = useMutation({
    mutationFn: () => {
      const body = { nombre, nit, correoContacto: correo };
      return empresa
        ? datos(api.PUT('/api/empresas-cliente/{id}', { params: { path: { id: empresa.id } }, body }))
        : datos(api.POST('/api/empresas-cliente', { body }));
    },
    onSuccess: (e) => {
      notifications.show({ color: 'green', message: empresa ? 'Empresa actualizada.' : `${e.nombre} quedó registrada.` });
      alGuardar();
      alCerrar();
    },
  });

  return (
    <PanelLateral
      abierto={abierto}
      alCerrar={alCerrar}
      titulo={empresa ? `Editar ${empresa.nombre}` : 'Nueva empresa'}
      mutacion={guardar}
      alEnviar={() => guardar.mutate()}
      textoEnviar={empresa ? 'Guardar cambios' : 'Registrar empresa'}
    >
      <TextInput label="Razón social" required value={nombre} onChange={(e) => setNombre(e.currentTarget.value)} />
      <TextInput label="NIT" description="Con dígito de verificación, por ejemplo 900123456-7" required value={nit} onChange={(e) => setNit(e.currentTarget.value)} />
      <TextInput label="Correo de contacto" type="email" required value={correo} onChange={(e) => setCorreo(e.currentTarget.value)} />
    </PanelLateral>
  );
}
