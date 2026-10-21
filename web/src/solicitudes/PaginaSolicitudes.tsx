import { Anchor, Badge, Button, Checkbox, Group, Pagination, Select, Table, Text } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconPlus } from '@tabler/icons-react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { api, datos } from '../api/cliente';
import { Consulta, Encabezado, InsigniaSolicitud } from '../comun/Componentes';
import { horaLocal } from '../comun/fechas';
import { texto, textos } from '../textos';
import { NuevaSolicitud } from './NuevaSolicitud';

const TamanoPagina = 25;
const estados = Object.entries(textos.estadoSolicitud).map(([value, label]) => ({ value, label }));

/** Solicitudes (RF-30, RF-31): la cola de pendientes va primero por urgencia y hora; el historial, de lo más reciente. */
export function PaginaSolicitudes() {
  const [estado, setEstado] = useState<string | null>(null);
  const [soloUrgentes, setSoloUrgentes] = useState(false);
  const [pagina, setPagina] = useState(1);
  const [nueva, { open: abrirNueva, close: cerrarNueva }] = useDisclosure();

  const consulta = useQuery({
    queryKey: ['solicitudes', estado, soloUrgentes, pagina],
    queryFn: () =>
      datos(
        api.GET('/api/solicitudes', {
          params: { query: { Estado: estado ?? undefined, Urgente: soloUrgentes || undefined, Pagina: pagina, TamanoPagina } },
        }),
      ),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <Encabezado titulo={textos.menu.solicitudes}>
        <Button leftSection={<IconPlus size={16} aria-hidden />} onClick={abrirNueva}>
          Nueva solicitud
        </Button>
      </Encabezado>
      <Group mb="md" align="end">
        <Select
          label="Estado"
          placeholder="Todos"
          data={estados}
          value={estado}
          onChange={(valor) => {
            setEstado(valor);
            setPagina(1);
          }}
          clearable
          w={200}
        />
        <Checkbox
          label="Solo urgentes"
          checked={soloUrgentes}
          onChange={(e) => {
            setSoloUrgentes(e.currentTarget.checked);
            setPagina(1);
          }}
          mb={8}
        />
      </Group>
      <Consulta consulta={consulta}>
        {(resultado) =>
          resultado.elementos.length === 0 ? (
            <Text c="dimmed">No hay solicitudes con esos filtros.</Text>
          ) : (
            <>
              <Table.ScrollContainer minWidth={760}>
                <Table striped>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Servicio</Table.Th>
                      <Table.Th>Pasajero</Table.Th>
                      <Table.Th>Empresa</Table.Th>
                      <Table.Th>Recorrido</Table.Th>
                      <Table.Th>Estado</Table.Th>
                      <Table.Th>Canal</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {resultado.elementos.map((s) => (
                      <Table.Tr key={s.id}>
                        <Table.Td>
                          <Anchor component={Link} to={`/solicitudes/${s.id}`}>
                            {horaLocal(s.horaLocalServicio, s.zonaHoraria)}
                          </Anchor>
                          {s.urgente && (
                            <Badge color="red" variant="filled" size="xs" ml={6}>
                              Urgente
                            </Badge>
                          )}
                        </Table.Td>
                        <Table.Td>{s.pasajeroNombre}</Table.Td>
                        <Table.Td>{s.empresaClienteNombre ?? '—'}</Table.Td>
                        <Table.Td>{s.detalleRestringido ? <Text c="dimmed" size="sm">Personal (restringida)</Text> : `${s.direccionOrigen} → ${s.direccionDestino}`}</Table.Td>
                        <Table.Td>
                          <InsigniaSolicitud estado={s.estado} />
                        </Table.Td>
                        <Table.Td>{texto(textos.canal, s.canal)}</Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
              <Group justify="space-between" mt="sm">
                <Text size="sm" c="dimmed">
                  {resultado.total} solicitud(es)
                </Text>
                <Pagination total={Math.max(1, Math.ceil(resultado.total / TamanoPagina))} value={pagina} onChange={setPagina}
                  getControlProps={(control) => ({ 'aria-label': control === 'next' ? 'Página siguiente' : 'Página anterior' })} />
              </Group>
            </>
          )
        }
      </Consulta>
      <NuevaSolicitud abierta={nueva} alCerrar={cerrarNueva} />
    </>
  );
}
