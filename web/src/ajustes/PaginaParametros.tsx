import { Badge, Button, Group, NumberInput, Select, Stack, Table, TagsInput, Text, VisuallyHidden } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconAdjustments, IconEdit } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { PanelLateral } from '../administracion/comun';
import { api, datos, type Esquemas } from '../api/cliente';
import { esAdministrador, useUsuario } from '../auth/usuario';
import { Consulta, Encabezado, EstadoVacio } from '../comun/Componentes';
import { instante } from '../comun/fechas';
import { texto, textos } from '../textos';

type Parametro = Esquemas['ParametroRespuesta'];
type Valor = Esquemas['JsonElement'];

/** Unidad del valor, deducida de la clave del catálogo (`…_minutos`, `…_dias`). */
function unidad(clave: string): string {
  if (clave.endsWith('_minutos')) return 'min';
  if (clave.endsWith('_dias')) return 'días';
  return '';
}

/** El valor tal como lo lee una persona: número con unidad, opción traducida o lista de franjas. */
export function valorLegible(p: Parametro, valor: Valor = p.valor): string {
  if (p.tipo === 'Franjas') {
    const franjas = Array.isArray(valor) ? (valor as string[]) : [];
    return franjas.length === 0 ? 'Ninguna' : franjas.join(', ');
  }
  if (p.tipo === 'Opcion') return texto(textos.opcionParametro, String(valor));
  return `${String(valor)} ${unidad(p.clave)}`.trim();
}

/**
 * Parámetros del operador (ADR-0016): se ven desde el Panel Web y solo el Administrador los cambia, con inicio de sesión
 * reciente (si no lo hay, el panel vuelve a pedir la contraseña y el código). Cada cambio queda en la auditoría.
 */
export function PaginaParametros() {
  const usuario = useUsuario();
  const puedeEditar = esAdministrador(usuario);
  const [editando, setEditando] = useState<Parametro | null>(null);
  const consulta = useQuery({ queryKey: ['parametros'], queryFn: () => datos(api.GET('/api/parametros')) });

  return (
    <>
      <Encabezado
        titulo={textos.menu.parametros}
        descripcion="Cómo se comporta la operación de este operador: plazos, recordatorios y visibilidad."
        migas={[{ etiqueta: textos.menu.configuracion }, { etiqueta: textos.menu.parametros }]}
      />
      <Consulta consulta={consulta}>
        {(parametros) =>
          parametros.length === 0 ? (
            <EstadoVacio icono={IconAdjustments} titulo="Sin parámetros" descripcion="La API no devolvió parámetros." />
          ) : (
            <Table.ScrollContainer minWidth={720}>
              <Table verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Parámetro</Table.Th>
                    <Table.Th>Valor</Table.Th>
                    <Table.Th>Último cambio</Table.Th>
                    {puedeEditar && (
                      <Table.Th>
                        <VisuallyHidden>Acciones</VisuallyHidden>
                      </Table.Th>
                    )}
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {parametros.map((p) => (
                    <Table.Tr key={p.clave}>
                      <Table.Td maw={420}>
                        <Text fw={600} size="sm">
                          {texto(textos.parametro, p.clave)}
                        </Text>
                        <Text size="xs" c="var(--fr-texto-suave)">
                          {p.descripcion}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Text size="sm">{valorLegible(p)}</Text>
                          {p.esPorDefecto && (
                            <Badge variant="outline" color="gray" size="sm">
                              Por defecto
                            </Badge>
                          )}
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{p.esPorDefecto ? '—' : instante(p.modificadoEn)}</Text>
                      </Table.Td>
                      {puedeEditar && (
                        <Table.Td>
                          <Button
                            size="xs"
                            variant="default"
                            leftSection={<IconEdit size={14} aria-hidden />}
                            onClick={() => setEditando(p)}
                            aria-label={`Cambiar ${texto(textos.parametro, p.clave)}`}
                          >
                            Cambiar
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
      {editando && <CambiarParametro key={editando.clave} parametro={editando} alCerrar={() => setEditando(null)} />}
    </>
  );
}

/** Cambia un parámetro con el control que corresponde a su tipo; la API vuelve a validar rango y opciones. */
function CambiarParametro({ parametro, alCerrar }: { parametro: Parametro; alCerrar: () => void }) {
  const cliente = useQueryClient();
  const [numero, setNumero] = useState<number | string>(parametro.tipo === 'Entero' ? Number(parametro.valor) : '');
  const [opcion, setOpcion] = useState<string | null>(parametro.tipo === 'Opcion' ? String(parametro.valor) : null);
  const [franjas, setFranjas] = useState<string[]>(parametro.tipo === 'Franjas' && Array.isArray(parametro.valor) ? (parametro.valor as string[]) : []);

  const guardar = useMutation({
    mutationFn: (valor: Valor) => datos(api.PUT('/api/parametros/{clave}', { params: { path: { clave: parametro.clave } }, body: { valor } })),
    onSuccess: (p) => {
      notifications.show({ color: 'green', message: `${texto(textos.parametro, p.clave)}: ${valorLegible(p)}.` });
      void cliente.invalidateQueries({ queryKey: ['parametros'] });
      alCerrar();
    },
  });

  const valorElegido = (): Valor => (parametro.tipo === 'Entero' ? Number(numero) : parametro.tipo === 'Opcion' ? opcion : franjas);
  const nombre = texto(textos.parametro, parametro.clave);

  return (
    <PanelLateral
      abierto
      alCerrar={alCerrar}
      titulo={`Cambiar: ${nombre}`}
      descripcion={parametro.descripcion}
      mutacion={guardar}
      alEnviar={() => guardar.mutate(valorElegido())}
      textoEnviar="Guardar"
    >
      {parametro.tipo === 'Entero' && (
        <NumberInput
          label={`Valor${unidad(parametro.clave) ? ` (${unidad(parametro.clave)})` : ''}`}
          description={parametro.minimo !== null && parametro.maximo !== null ? `Entre ${parametro.minimo} y ${parametro.maximo}` : undefined}
          min={parametro.minimo ?? undefined}
          max={parametro.maximo ?? undefined}
          allowDecimal={false}
          required
          value={numero}
          onChange={setNumero}
        />
      )}
      {parametro.tipo === 'Opcion' && (
        <Select
          label="Valor"
          data={parametro.opciones.map((o) => ({ value: o, label: texto(textos.opcionParametro, o) }))}
          value={opcion}
          onChange={setOpcion}
          allowDeselect={false}
          required
        />
      )}
      {parametro.tipo === 'Franjas' && (
        <TagsInput
          label="Franjas"
          description="Escribe cada franja como HH:mm-HH:mm (por ejemplo 03:00-05:00) y pulsa Enter."
          value={franjas}
          onChange={setFranjas}
          clearable
        />
      )}
      <Stack gap={4}>
        <Text size="sm" c="var(--fr-texto-suave)">
          Valor por defecto: {valorLegible(parametro, parametro.valorPorDefecto)}
        </Text>
        {!parametro.esPorDefecto && (
          <Button variant="subtle" size="xs" w="fit-content" px={0} onClick={() => guardar.mutate(parametro.valorPorDefecto)}>
            Volver al valor por defecto
          </Button>
        )}
      </Stack>
    </PanelLateral>
  );
}
