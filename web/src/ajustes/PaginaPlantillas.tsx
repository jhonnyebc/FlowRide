import { Badge, Button, Code, Group, Select, Table, Text, TextInput, VisuallyHidden } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconBrandWhatsapp, IconEdit } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { PanelLateral } from '../administracion/comun';
import { api, datos, type Esquemas } from '../api/cliente';
import { esAdministrador, useUsuario } from '../auth/usuario';
import { Consulta, Encabezado, EstadoVacio } from '../comun/Componentes';
import { instante } from '../comun/fechas';
import { texto, textos } from '../textos';

type Plantilla = Esquemas['PlantillaRespuesta'];

const EstadosPlantilla = ['Pendiente', 'Aprobada', 'Rechazada'] as const;

/** Estado con texto (OPLOG-ACC-02): solo las aprobadas se usan fuera de la ventana de 24 h. */
function InsigniaPlantilla({ estado }: { estado: string }) {
  const color = estado === 'Aprobada' ? 'teal' : estado === 'Rechazada' ? 'red' : 'orange';
  return (
    <Badge color={color} variant="light">
      {texto(textos.estadoPlantilla, estado)}
    </Badge>
  );
}

/**
 * Plantillas de WhatsApp (ADR-0016): Meta solo deja escribirle a alguien pasadas 24 horas de su último mensaje con una
 * plantilla aprobada. Aquí se registra el nombre e idioma con que se creó cada una en Meta y su estado; si un aviso cae
 * fuera de la ventana y su plantilla no está aprobada, el sistema crea una alerta en vez de enviarlo.
 */
export function PaginaPlantillas() {
  const usuario = useUsuario();
  const puedeEditar = esAdministrador(usuario);
  const [editando, setEditando] = useState<Plantilla | null>(null);
  const consulta = useQuery({ queryKey: ['plantillas'], queryFn: () => datos(api.GET('/api/notificaciones/plantillas')) });

  return (
    <>
      <Encabezado
        titulo={textos.menu.plantillas}
        descripcion="Los avisos que se envían a pasajeros y conductores fuera de la ventana de 24 horas de WhatsApp."
        migas={[{ etiqueta: textos.menu.configuracion }, { etiqueta: textos.menu.plantillas }]}
      />
      <Consulta consulta={consulta}>
        {(plantillas) =>
          plantillas.length === 0 ? (
            <EstadoVacio icono={IconBrandWhatsapp} titulo="Sin plantillas" descripcion="No hay plantillas registradas." />
          ) : (
            <Table.ScrollContainer minWidth={760}>
              <Table verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Aviso</Table.Th>
                    <Table.Th>Nombre en Meta</Table.Th>
                    <Table.Th>Idioma</Table.Th>
                    <Table.Th>Variables</Table.Th>
                    <Table.Th>Estado</Table.Th>
                    {puedeEditar && (
                      <Table.Th>
                        <VisuallyHidden>Acciones</VisuallyHidden>
                      </Table.Th>
                    )}
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {plantillas.map((p) => (
                    <Table.Tr key={p.clave}>
                      <Table.Td>
                        <Code>{p.clave}</Code>
                        <Text size="xs" c="var(--fr-texto-suave)">
                          Último cambio: {instante(p.modificadaEn)}
                        </Text>
                      </Table.Td>
                      <Table.Td>{p.nombre}</Table.Td>
                      <Table.Td>{p.idioma}</Table.Td>
                      <Table.Td>
                        <Group gap={4}>
                          {p.variables.length === 0
                            ? '—'
                            : p.variables.map((v, i) => (
                                <Badge key={v} variant="default" radius="sm" tt="none">
                                  {`{{${i + 1}}} ${v}`}
                                </Badge>
                              ))}
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <InsigniaPlantilla estado={p.estado} />
                      </Table.Td>
                      {puedeEditar && (
                        <Table.Td>
                          <Button
                            size="xs"
                            variant="default"
                            leftSection={<IconEdit size={14} aria-hidden />}
                            onClick={() => setEditando(p)}
                            aria-label={`Editar la plantilla ${p.clave}`}
                          >
                            Editar
                          </Button>
                        </Table.Td>
                      )}
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )
        }
      </Consulta>
      {editando && <EditarPlantilla key={editando.clave} plantilla={editando} alCerrar={() => setEditando(null)} />}
    </>
  );
}

function EditarPlantilla({ plantilla, alCerrar }: { plantilla: Plantilla; alCerrar: () => void }) {
  const cliente = useQueryClient();
  const [nombre, setNombre] = useState(plantilla.nombre);
  const [idioma, setIdioma] = useState(plantilla.idioma);
  const [estado, setEstado] = useState<string | null>(plantilla.estado);

  const guardar = useMutation({
    mutationFn: () =>
      datos(api.PUT('/api/notificaciones/plantillas/{clave}', { params: { path: { clave: plantilla.clave } }, body: { nombre, idioma, estado } })),
    onSuccess: (p) => {
      notifications.show({ color: 'green', message: `Plantilla ${p.clave}: ${texto(textos.estadoPlantilla, p.estado)}.` });
      void cliente.invalidateQueries({ queryKey: ['plantillas'] });
      alCerrar();
    },
  });

  return (
    <PanelLateral
      abierto
      alCerrar={alCerrar}
      titulo={`Plantilla ${plantilla.clave}`}
      descripcion="Copia el nombre y el idioma exactos con que creaste la plantilla en Meta, y márcala como aprobada cuando Meta la apruebe."
      mutacion={guardar}
      alEnviar={() => guardar.mutate()}
      textoEnviar="Guardar"
    >
      <TextInput label="Nombre en Meta" description="Minúsculas y guiones bajos, como en Meta" required value={nombre} onChange={(e) => setNombre(e.currentTarget.value)} />
      <TextInput label="Idioma" description="Código de idioma de Meta, por ejemplo es_CO" required value={idioma} onChange={(e) => setIdioma(e.currentTarget.value)} />
      <Select
        label="Estado en Meta"
        data={EstadosPlantilla.map((e) => ({ value: e, label: texto(textos.estadoPlantilla, e) }))}
        value={estado}
        onChange={setEstado}
        allowDeselect={false}
      />
      {plantilla.variables.length > 0 && (
        <Text size="sm" c="var(--fr-texto-suave)">
          La plantilla en Meta debe tener {plantilla.variables.length} variable(s), en este orden: {plantilla.variables.map((v, i) => `{{${i + 1}}} ${v}`).join(', ')}. El sistema las llena con los datos de cada servicio.
        </Text>
      )}
    </PanelLateral>
  );
}
