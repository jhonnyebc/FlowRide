import { Badge, Button, Group, Paper, SegmentedControl, Select, SimpleGrid, Table, Text, TextInput, VisuallyHidden } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconEdit, IconPlus, IconSearch, IconShieldLock, IconUserCheck, IconUserOff, IconUsers } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos, type Esquemas } from '../api/cliente';
import { useUsuario } from '../auth/usuario';
import { Consulta, Encabezado, EstadoVacio, Persona } from '../comun/Componentes';
import { instante } from '../comun/fechas';
import { texto, textos } from '../textos';
import { Confirmacion, InsigniaActivo, MenuAcciones, PanelLateral } from './comun';

type Usuario = Esquemas['UsuarioRespuesta'];
type Filtro = 'activos' | 'inactivos' | 'todos';

const rolesPanel = [
  { value: 'OperadorLogistico', label: textos.roles.OperadorLogistico! },
  { value: 'Administrador', label: textos.roles.Administrador! },
];

/**
 * Usuarios del Panel Web (RF-02, RF-24, RF-25): solo el Administrador. Crear le envía al usuario el correo para definir
 * su contraseña; nadie se cambia el rol ni se desactiva a sí mismo. Las acciones piden un inicio de sesión reciente.
 */
export function PaginaUsuarios() {
  const yo = useUsuario();
  const cliente = useQueryClient();
  const [busqueda, setBusqueda] = useState('');
  const [filtro, setFiltro] = useState<Filtro>('activos');
  const [rol, setRol] = useState<string | null>(null);
  const [editando, setEditando] = useState<Usuario | null>(null);
  const [panel, { open: abrirPanel, close: cerrarPanel }] = useDisclosure();
  const [accion, setAccion] = useState<{ tipo: 'estado' | 'rol'; usuario: Usuario } | null>(null);
  const [nuevoRol, setNuevoRol] = useState<string | null>(null);
  const [version, setVersion] = useState(0);

  const consulta = useQuery({ queryKey: ['usuarios'], queryFn: () => datos(api.GET('/api/usuarios')) });
  const refrescar = () => void cliente.invalidateQueries({ queryKey: ['usuarios'] });

  const cambiarEstado = useMutation({
    mutationFn: (u: Usuario) =>
      u.activo
        ? datos(api.POST('/api/usuarios/{id}/desactivar', { params: { path: { id: u.id } } }))
        : datos(api.POST('/api/usuarios/{id}/activar', { params: { path: { id: u.id } } })),
    onSuccess: (u) => {
      notifications.show({ color: 'green', message: u.activo ? `${u.nombre} puede volver a entrar.` : `${u.nombre} ya no puede entrar al panel.` });
      setAccion(null);
      refrescar();
    },
  });
  const cambiarRol = useMutation({
    mutationFn: (u: Usuario) => datos(api.PUT('/api/usuarios/{id}/rol', { params: { path: { id: u.id } }, body: { rol: nuevoRol } })),
    onSuccess: (u) => {
      notifications.show({ color: 'green', message: `${u.nombre} ahora es ${texto(textos.roles, u.rol)}.` });
      setAccion(null);
      refrescar();
    },
  });

  const abrir = (usuario: Usuario | null) => {
    setEditando(usuario);
    setVersion((v) => v + 1);
    abrirPanel();
  };

  return (
    <>
      <Encabezado
        titulo={textos.menu.usuarios}
        descripcion="Quién entra al panel de este operador y con qué rol."
        migas={[{ etiqueta: textos.menu.administracion }, { etiqueta: textos.menu.usuarios }]}
      >
        <Button leftSection={<IconPlus size={16} aria-hidden />} onClick={() => abrir(null)}>
          Nuevo usuario
        </Button>
      </Encabezado>

      <Consulta consulta={consulta}>
        {(todos) => {
          const delPanel = todos.filter((u) => u.rol === 'Administrador' || u.rol === 'OperadorLogistico');
          const termino = busqueda.trim().toLowerCase();
          const visibles = delPanel.filter(
            (u) =>
              (filtro === 'todos' || u.activo === (filtro === 'activos')) &&
              (!rol || u.rol === rol) &&
              (!termino || [u.nombre, u.correo ?? '', u.telefonoWhatsApp].some((v) => v.toLowerCase().includes(termino))),
          );
          return (
            <>
              <SimpleGrid cols={{ base: 1, xs: 3 }} mb="md">
                <Cifra etiqueta="Administradores" valor={delPanel.filter((u) => u.activo && u.rol === 'Administrador').length} />
                <Cifra etiqueta="Operadores logísticos" valor={delPanel.filter((u) => u.activo && u.rol === 'OperadorLogistico').length} />
                <Cifra etiqueta="Desactivados" valor={delPanel.filter((u) => !u.activo).length} />
              </SimpleGrid>

              <Group mb="md" align="end" wrap="wrap">
                <TextInput
                  label="Buscar"
                  placeholder="Nombre, correo o WhatsApp"
                  leftSection={<IconSearch size={16} aria-hidden />}
                  value={busqueda}
                  onChange={(e) => setBusqueda(e.currentTarget.value)}
                  w={280}
                />
                <Select label="Rol" placeholder="Todos" data={rolesPanel} value={rol} onChange={setRol} clearable w={200} />
                <SegmentedControl
                  aria-label="Estado"
                  value={filtro}
                  onChange={(v) => setFiltro(v as Filtro)}
                  data={[
                    { value: 'activos', label: 'Activos' },
                    { value: 'inactivos', label: 'Inactivos' },
                    { value: 'todos', label: 'Todos' },
                  ]}
                />
              </Group>

              {visibles.length === 0 ? (
                <EstadoVacio icono={IconUsers} titulo="Sin usuarios" descripcion="No hay usuarios del panel con esos filtros." />
              ) : (
                <Table.ScrollContainer minWidth={760}>
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Usuario</Table.Th>
                        <Table.Th>WhatsApp</Table.Th>
                        <Table.Th>Rol</Table.Th>
                        <Table.Th>Estado</Table.Th>
                        <Table.Th>Creado</Table.Th>
                        <Table.Th>
                          <VisuallyHidden>Acciones</VisuallyHidden>
                        </Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {visibles.map((u) => {
                        const soyYo = u.id === yo.id;
                        return (
                          <Table.Tr key={u.id}>
                            <Table.Td>
                              <Persona nombre={u.nombre} tamano="sm" detalle={soyYo ? `${u.correo ?? ''} · tú` : u.correo} />
                            </Table.Td>
                            <Table.Td>{u.telefonoWhatsApp}</Table.Td>
                            <Table.Td>
                              <Badge variant={u.rol === 'Administrador' ? 'filled' : 'light'} color="primario">
                                {texto(textos.roles, u.rol)}
                              </Badge>
                            </Table.Td>
                            <Table.Td>
                              <InsigniaActivo activo={u.activo} />
                            </Table.Td>
                            <Table.Td>{instante(u.creadoEn)}</Table.Td>
                            <Table.Td>
                              <MenuAcciones
                                de={u.nombre}
                                acciones={[
                                  { etiqueta: 'Editar datos', icono: <IconEdit size={16} aria-hidden />, alElegir: () => abrir(u) },
                                  {
                                    etiqueta: 'Cambiar rol',
                                    icono: <IconShieldLock size={16} aria-hidden />,
                                    deshabilitada: soyYo,
                                    alElegir: () => {
                                      setNuevoRol(u.rol === 'Administrador' ? 'OperadorLogistico' : 'Administrador');
                                      cambiarRol.reset();
                                      setAccion({ tipo: 'rol', usuario: u });
                                    },
                                  },
                                  {
                                    etiqueta: u.activo ? 'Desactivar' : 'Activar',
                                    icono: u.activo ? <IconUserOff size={16} aria-hidden /> : <IconUserCheck size={16} aria-hidden />,
                                    peligrosa: u.activo,
                                    deshabilitada: soyYo,
                                    alElegir: () => {
                                      cambiarEstado.reset();
                                      setAccion({ tipo: 'estado', usuario: u });
                                    },
                                  },
                                ]}
                              />
                            </Table.Td>
                          </Table.Tr>
                        );
                      })}
                    </Table.Tbody>
                  </Table>
                </Table.ScrollContainer>
              )}
            </>
          );
        }}
      </Consulta>

      <FormularioUsuario key={`${editando?.id ?? 'nuevo'}-${version}`} abierto={panel} alCerrar={cerrarPanel} usuario={editando} alGuardar={refrescar} />

      {accion?.tipo === 'estado' && (
        <Confirmacion
          abierta
          alCerrar={() => setAccion(null)}
          titulo={accion.usuario.activo ? 'Desactivar usuario' : 'Activar usuario'}
          mensaje={
            accion.usuario.activo
              ? `${accion.usuario.nombre} no podrá entrar al panel y se cerrarán sus sesiones abiertas.`
              : `${accion.usuario.nombre} podrá volver a entrar al panel con su contraseña.`
          }
          textoConfirmar={accion.usuario.activo ? 'Desactivar' : 'Activar'}
          peligrosa={accion.usuario.activo}
          mutacion={cambiarEstado}
          alConfirmar={() => cambiarEstado.mutate(accion.usuario)}
        />
      )}
      {accion?.tipo === 'rol' && (
        <Confirmacion
          abierta
          alCerrar={() => setAccion(null)}
          titulo="Cambiar rol"
          mensaje={`El cambio aplica en la siguiente acción de ${accion.usuario.nombre}. Un Administrador debe configurar doble factor.`}
          textoConfirmar="Cambiar rol"
          mutacion={cambiarRol}
          alConfirmar={() => cambiarRol.mutate(accion.usuario)}
        >
          <Select label="Nuevo rol" data={rolesPanel} value={nuevoRol} onChange={setNuevoRol} allowDeselect={false} />
        </Confirmacion>
      )}
    </>
  );
}

function Cifra({ etiqueta, valor }: { etiqueta: string; valor: number }) {
  return (
    <Paper p="md" className="superficie">
      <Text fz={28} lh={1.1} className="cifra">
        {valor}
      </Text>
      <Text size="sm" c="var(--fr-texto-suave)">
        {etiqueta}
      </Text>
    </Paper>
  );
}

/** Crear (con rol) o editar (nombre, correo y WhatsApp) en el panel lateral. */
function FormularioUsuario({
  abierto,
  alCerrar,
  usuario,
  alGuardar,
}: {
  abierto: boolean;
  alCerrar: () => void;
  usuario: Usuario | null;
  alGuardar: () => void;
}) {
  const [nombre, setNombre] = useState(usuario?.nombre ?? '');
  const [correo, setCorreo] = useState(usuario?.correo ?? '');
  const [telefono, setTelefono] = useState(usuario?.telefonoWhatsApp ?? '');
  const [rol, setRol] = useState<string | null>('OperadorLogistico');

  const guardar = useMutation({
    mutationFn: () =>
      usuario
        ? datos(api.PUT('/api/usuarios/{id}', { params: { path: { id: usuario.id } }, body: { nombre, correo, telefonoWhatsApp: telefono } }))
        : datos(api.POST('/api/usuarios', { body: { nombre, correo, telefonoWhatsApp: telefono, rol } })),
    onSuccess: (u) => {
      notifications.show({
        color: 'green',
        message: usuario
          ? correo.trim().toLowerCase() !== (usuario.correo ?? '').toLowerCase()
            ? `Datos guardados. ${u.nombre} debe confirmar su nuevo correo.`
            : 'Datos guardados.'
          : `Usuario creado: a ${u.correo} le llegó el correo para definir su contraseña.`,
      });
      alGuardar();
      alCerrar();
    },
  });

  return (
    <PanelLateral
      abierto={abierto}
      alCerrar={alCerrar}
      titulo={usuario ? `Editar a ${usuario.nombre}` : 'Nuevo usuario'}
      descripcion={
        usuario
          ? 'El correo es su usuario de inicio de sesión: si lo cambias, deberá confirmarlo desde el correo que le llegará.'
          : 'Le llegará un correo para definir su contraseña. Un Administrador además configura doble factor al entrar.'
      }
      mutacion={guardar}
      alEnviar={() => guardar.mutate()}
      textoEnviar={usuario ? 'Guardar cambios' : 'Crear usuario'}
    >
      <TextInput label="Nombre completo" required value={nombre} onChange={(e) => setNombre(e.currentTarget.value)} />
      <TextInput label="Correo" type="email" required value={correo} onChange={(e) => setCorreo(e.currentTarget.value)} />
      <TextInput
        label="WhatsApp"
        description="Formato internacional, por ejemplo +573001112233"
        required
        value={telefono}
        onChange={(e) => setTelefono(e.currentTarget.value)}
      />
      {!usuario && <Select label="Rol" data={rolesPanel} value={rol} onChange={setRol} allowDeselect={false} required />}
    </PanelLateral>
  );
}
