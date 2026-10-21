import { Badge, Button, Group, Select, Table, TextInput, VisuallyHidden } from '@mantine/core';
import { useDebouncedValue, useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconEdit, IconPlus, IconSearch, IconUsers } from '@tabler/icons-react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos, type Esquemas } from '../api/cliente';
import { esAdministrador, useUsuario } from '../auth/usuario';
import { Consulta, Encabezado, EstadoVacio, Persona } from '../comun/Componentes';
import { textos } from '../textos';
import { InsigniaActivo, MenuAcciones, PanelLateral, PiePagina, TamanoPagina } from './comun';

type Pasajero = Esquemas['PasajeroRespuesta'];

/** Las empresas cliente, para elegir la del pasajero y mostrar su nombre (son pocas: una sola página basta). */
function useEmpresas() {
  return useQuery({
    queryKey: ['empresas', 'todas'],
    queryFn: () => datos(api.GET('/api/empresas-cliente', { params: { query: { TamanoPagina: 200 } } })),
    select: (p) => p.elementos,
  });
}

/**
 * Pasajeros del operador (RF-05): su número de WhatsApp es su identidad en el bot (ADR-0008). El Administrador los
 * registra y edita; el Operador logístico los consulta.
 */
export function PaginaPasajeros() {
  const usuario = useUsuario();
  const puedeEditar = esAdministrador(usuario);
  const cliente = useQueryClient();
  const [busqueda, setBusqueda] = useState('');
  const [texto] = useDebouncedValue(busqueda, 300);
  const [empresaId, setEmpresaId] = useState<string | null>(null);
  const [pagina, setPagina] = useState(1);
  const [editando, setEditando] = useState<Pasajero | null>(null);
  const [version, setVersion] = useState(0);
  const [panel, { open: abrirPanel, close: cerrarPanel }] = useDisclosure();

  const empresas = useEmpresas();
  const nombreEmpresa = new Map((empresas.data ?? []).map((e) => [e.id, e.nombre]));
  const consulta = useQuery({
    queryKey: ['pasajeros', 'lista', texto, empresaId, pagina],
    queryFn: () =>
      datos(
        api.GET('/api/pasajeros', {
          params: { query: { Texto: texto || undefined, EmpresaClienteId: empresaId ?? undefined, Pagina: pagina, TamanoPagina } },
        }),
      ),
    placeholderData: keepPreviousData,
  });

  const abrir = (pasajero: Pasajero | null) => {
    setEditando(pasajero);
    setVersion((v) => v + 1);
    abrirPanel();
  };

  return (
    <>
      <Encabezado
        titulo={textos.menu.pasajeros}
        descripcion="Quienes piden viajes por WhatsApp: el bot los reconoce por su número."
        migas={[{ etiqueta: textos.menu.administracion }, { etiqueta: textos.menu.pasajeros }]}
      >
        {puedeEditar && (
          <Button leftSection={<IconPlus size={16} aria-hidden />} onClick={() => abrir(null)}>
            Nuevo pasajero
          </Button>
        )}
      </Encabezado>

      <Group mb="md" align="end" wrap="wrap">
        <TextInput
          label="Buscar"
          placeholder="Nombre, código o WhatsApp"
          leftSection={<IconSearch size={16} aria-hidden />}
          value={busqueda}
          onChange={(e) => {
            setBusqueda(e.currentTarget.value);
            setPagina(1);
          }}
          w={280}
        />
        <Select
          label="Empresa"
          placeholder="Todas"
          data={(empresas.data ?? []).map((e) => ({ value: e.id, label: e.nombre }))}
          value={empresaId}
          onChange={(v) => {
            setEmpresaId(v);
            setPagina(1);
          }}
          searchable
          clearable
          w={240}
        />
      </Group>

      <Consulta consulta={consulta}>
        {(resultado) =>
          resultado.elementos.length === 0 ? (
            <EstadoVacio icono={IconUsers} titulo="Sin pasajeros" descripcion="No hay pasajeros con esos filtros." />
          ) : (
            <>
              <Table.ScrollContainer minWidth={720}>
                <Table>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Pasajero</Table.Th>
                      <Table.Th>WhatsApp</Table.Th>
                      <Table.Th>Empresa</Table.Th>
                      <Table.Th>Código</Table.Th>
                      <Table.Th>Estado</Table.Th>
                      {puedeEditar && (
                        <Table.Th>
                          <VisuallyHidden>Acciones</VisuallyHidden>
                        </Table.Th>
                      )}
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {resultado.elementos.map((p) => (
                      <Table.Tr key={p.id}>
                        <Table.Td>
                          <Persona nombre={p.nombre} tamano="sm" detalle={p.correo ?? undefined} />
                        </Table.Td>
                        <Table.Td>{p.telefonoWhatsApp}</Table.Td>
                        <Table.Td>{nombreEmpresa.get(p.empresaClienteId) ?? '—'}</Table.Td>
                        <Table.Td>
                          <Badge variant="default" radius="sm">
                            {p.codigoInterno}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <InsigniaActivo activo={p.activo} />
                        </Table.Td>
                        {puedeEditar && (
                          <Table.Td>
                            <MenuAcciones
                              de={p.nombre}
                              acciones={[{ etiqueta: 'Editar', icono: <IconEdit size={16} aria-hidden />, alElegir: () => abrir(p) }]}
                            />
                          </Table.Td>
                        )}
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
              <PiePagina total={resultado.total} pagina={pagina} alCambiar={setPagina} unidad="pasajero(s)" />
            </>
          )
        }
      </Consulta>

      {puedeEditar && (
        <FormularioPasajero
          key={`${editando?.id ?? 'nuevo'}-${version}`}
          abierto={panel}
          alCerrar={cerrarPanel}
          pasajero={editando}
          empresas={(empresas.data ?? []).filter((e) => e.activa || e.id === editando?.empresaClienteId)}
          alGuardar={() => void cliente.invalidateQueries({ queryKey: ['pasajeros'] })}
        />
      )}
    </>
  );
}

function FormularioPasajero({
  abierto,
  alCerrar,
  pasajero,
  empresas,
  alGuardar,
}: {
  abierto: boolean;
  alCerrar: () => void;
  pasajero: Pasajero | null;
  empresas: Esquemas['EmpresaClienteRespuesta'][];
  alGuardar: () => void;
}) {
  const [nombre, setNombre] = useState(pasajero?.nombre ?? '');
  const [telefono, setTelefono] = useState(pasajero?.telefonoWhatsApp ?? '');
  const [correo, setCorreo] = useState(pasajero?.correo ?? '');
  const [empresaId, setEmpresaId] = useState<string | null>(pasajero?.empresaClienteId ?? null);
  const [codigo, setCodigo] = useState(pasajero?.codigoInterno ?? '');

  const guardar = useMutation({
    mutationFn: () => {
      const body = { nombre, telefonoWhatsApp: telefono, correo: correo.trim() || null, empresaClienteId: empresaId, codigoInterno: codigo };
      return pasajero
        ? datos(api.PUT('/api/pasajeros/{id}', { params: { path: { id: pasajero.id } }, body }))
        : datos(api.POST('/api/pasajeros', { body }));
    },
    onSuccess: (p) => {
      notifications.show({ color: 'green', message: pasajero ? 'Pasajero actualizado.' : `${p.nombre} ya puede pedir viajes por WhatsApp.` });
      alGuardar();
      alCerrar();
    },
  });

  return (
    <PanelLateral
      abierto={abierto}
      alCerrar={alCerrar}
      titulo={pasajero ? `Editar a ${pasajero.nombre}` : 'Nuevo pasajero'}
      descripcion="El bot de WhatsApp lo reconoce por su número exacto: escríbelo en formato internacional."
      mutacion={guardar}
      alEnviar={() => guardar.mutate()}
      textoEnviar={pasajero ? 'Guardar cambios' : 'Registrar pasajero'}
    >
      <TextInput label="Nombre completo" required value={nombre} onChange={(e) => setNombre(e.currentTarget.value)} />
      <TextInput
        label="WhatsApp"
        description="Por ejemplo +573001112233"
        required
        value={telefono}
        onChange={(e) => setTelefono(e.currentTarget.value)}
      />
      <TextInput label="Correo" description="Opcional" type="email" value={correo} onChange={(e) => setCorreo(e.currentTarget.value)} />
      <Select
        label="Empresa"
        data={empresas.map((e) => ({ value: e.id, label: e.nombre }))}
        value={empresaId}
        onChange={setEmpresaId}
        searchable
        required
        nothingFoundMessage="No hay empresas activas"
      />
      <TextInput
        label="Código interno"
        description="El código del pasajero en su empresa (empleado, contrato…)"
        required
        value={codigo}
        onChange={(e) => setCodigo(e.currentTarget.value)}
      />
    </PanelLateral>
  );
}
