import { Anchor, Badge, Table, Text } from '@mantine/core';
import { IconClockExclamation } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { monitoreo } from '../api/consultas';
import { Consulta, Encabezado, InsigniaViaje } from '../comun/Componentes';
import { hora } from '../comun/fechas';
import { textos } from '../textos';

/** Monitoreo de la operación (RF-37): viajes en curso y los que empiezan en las próximas horas; sin GPS (ADR-0005). */
export function PaginaViajes() {
  const consulta = useQuery(monitoreo());

  return (
    <>
      <Encabezado titulo={textos.menu.viajesEnCurso} />
      <Consulta consulta={consulta}>
        {(viajes) =>
          viajes.length === 0 ? (
            <Text c="dimmed">No hay viajes en curso ni programados para las próximas horas.</Text>
          ) : (
            <Table.ScrollContainer minWidth={720}>
              <Table striped>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Inicio programado</Table.Th>
                    <Table.Th>Estado</Table.Th>
                    <Table.Th>Conductor</Table.Th>
                    <Table.Th>Pasajero</Table.Th>
                    <Table.Th>Incidentes</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {viajes.map(({ viaje: v, retrasado, minutosRetraso }) => (
                    <Table.Tr key={v.id}>
                      <Table.Td>
                        <Anchor component={Link} to={`/viajes/${v.id}`}>
                          {hora(v.inicioProgramado)}
                        </Anchor>
                        {retrasado && (
                          <Badge color="red" variant="filled" size="sm" ml={6} leftSection={<IconClockExclamation size={12} aria-hidden />}>
                            Retrasado {minutosRetraso} min
                          </Badge>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <InsigniaViaje estado={v.estado} />
                      </Table.Td>
                      <Table.Td>
                        {v.conductorNombre} · {v.placa}
                      </Table.Td>
                      <Table.Td>{v.pasajeroNombre}</Table.Td>
                      <Table.Td>{v.incidentes > 0 ? <Badge color="orange">{v.incidentes}</Badge> : '—'}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )
        }
      </Consulta>
    </>
  );
}
