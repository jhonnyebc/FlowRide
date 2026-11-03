import { Anchor, Group, Paper, SimpleGrid, Table, Text, TextInput, ThemeIcon, VisuallyHidden } from '@mantine/core';
import { IconAlertOctagon, IconCircleCheck, IconEye } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { vistaDelDia } from '../api/consultas';
import { Consulta, Encabezado, InsigniaNivel, InsigniaSolicitud } from '../comun/Componentes';
import { hora, hoyIso } from '../comun/fechas';
import { textos } from '../textos';

/**
 * Vista centralizada del día (RF-36, OPLOG-UX-02): primero lo crítico, con el porqué de cada nivel. Se actualiza sola
 * cada 30 s; es estado operacional y siempre viene de la base (ADR-0006).
 */
export function VistaDelDia() {
  const [fecha, setFecha] = useState(hoyIso());
  const consulta = useQuery(vistaDelDia(fecha));

  return (
    <>
      <Encabezado titulo={textos.menu.vistaDelDia}>
        <TextInput type="date" label="Día" value={fecha} onChange={(e) => setFecha(e.currentTarget.value || hoyIso())} w={170} />
      </Encabezado>
      <Consulta consulta={consulta}>
        {(vista) => (
          <>
            <SimpleGrid cols={{ base: 1, xs: 3 }} mb="md" aria-label="Resumen del día">
              <Resumen etiqueta="Críticos" cantidad={vista.criticos} color="red" icono={<IconAlertOctagon aria-hidden />} />
              <Resumen etiqueta="Requieren atención" cantidad={vista.atencion} color="yellow" icono={<IconEye aria-hidden />} />
              <Resumen etiqueta="Normales" cantidad={vista.normales} color="green" icono={<IconCircleCheck aria-hidden />} />
            </SimpleGrid>
            {vista.servicios.length === 0 ? (
              <Text c="dimmed">No hay servicios programados para este día.</Text>
            ) : (
              <Table.ScrollContainer minWidth={760}>
                <Table striped>
                  <Table.Caption>
                    <VisuallyHidden>Servicios del día, primero los que requieren atención inmediata</VisuallyHidden>
                  </Table.Caption>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Nivel</Table.Th>
                      <Table.Th>Hora</Table.Th>
                      <Table.Th>Pasajero</Table.Th>
                      <Table.Th>Empresa</Table.Th>
                      <Table.Th>Estado</Table.Th>
                      <Table.Th>Conductor</Table.Th>
                      <Table.Th>Por qué</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {vista.servicios.map(({ servicio, nivel, motivo }) => (
                      <Table.Tr key={servicio.id}>
                        <Table.Td>
                          <InsigniaNivel nivel={nivel} />
                        </Table.Td>
                        <Table.Td>
                          <Anchor component={Link} to={`/solicitudes/${servicio.id}`}>
                            {hora(servicio.fechaHoraServicio)}
                          </Anchor>
                        </Table.Td>
                        <Table.Td>{servicio.pasajeroNombre}</Table.Td>
                        <Table.Td>{servicio.empresaClienteNombre ?? '—'}</Table.Td>
                        <Table.Td>
                          <InsigniaSolicitud estado={servicio.estado} />
                        </Table.Td>
                        <Table.Td>{servicio.conductorNombre ? `${servicio.conductorNombre} · ${servicio.placa}` : '—'}</Table.Td>
                        <Table.Td>{motivo ?? '—'}</Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
            )}
          </>
        )}
      </Consulta>
    </>
  );
}

function Resumen({ etiqueta, cantidad, color, icono }: { etiqueta: string; cantidad: number; color: string; icono: React.ReactNode }) {
  return (
    <Paper p="md" className="superficie">
      <Group wrap="nowrap">
        <ThemeIcon color={color} variant="light" size="lg">
          {icono}
        </ThemeIcon>
        <div>
          <Text fz={28} fw={400} lh={1} className="cifra">
            {cantidad}
          </Text>
          <Text size="sm" c="dimmed">
            {etiqueta}
          </Text>
        </div>
      </Group>
    </Paper>
  );
}
