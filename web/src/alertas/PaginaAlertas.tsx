import { Badge, Button, Checkbox, Group, Pagination, Paper, Stack, Text } from '@mantine/core';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos } from '../api/cliente';
import { Consulta, Encabezado } from '../comun/Componentes';
import { instante } from '../comun/fechas';
import { texto, textos } from '../textos';
import { colorSeveridad, IconoSeveridad } from './severidad';
import { claveAlertas } from './useAlertas';

const TamanoPagina = 20;

/** Alertas del panel (RF-45): las nuevas llegan en vivo; aquí se revisan y se marcan como leídas. */
export function PaginaAlertas() {
  const cliente = useQueryClient();
  const [soloNoLeidas, setSoloNoLeidas] = useState(true);
  const [pagina, setPagina] = useState(1);

  const consulta = useQuery({
    queryKey: [...claveAlertas, 'lista', soloNoLeidas, pagina],
    queryFn: () => datos(api.GET('/api/alertas', { params: { query: { SoloNoLeidas: soloNoLeidas || undefined, Pagina: pagina, TamanoPagina } } })),
    placeholderData: keepPreviousData,
  });
  const marcar = useMutation({
    mutationFn: (id: string) => datos(api.POST('/api/alertas/{id}/leida', { params: { path: { id } } })),
    onSuccess: () => void cliente.invalidateQueries({ queryKey: claveAlertas }),
  });

  return (
    <>
      <Encabezado titulo={textos.menu.alertas}>
        <Checkbox
          label="Solo sin leer"
          checked={soloNoLeidas}
          onChange={(e) => {
            setSoloNoLeidas(e.currentTarget.checked);
            setPagina(1);
          }}
        />
      </Encabezado>
      <Consulta consulta={consulta}>
        {(resultado) =>
          resultado.elementos.length === 0 ? (
            <Text c="dimmed">{soloNoLeidas ? 'No tienes alertas sin leer.' : 'No hay alertas.'}</Text>
          ) : (
            <>
              <Stack gap="xs" component="ul" p={0} style={{ listStyle: 'none' }}>
                {resultado.elementos.map((a) => (
                  <Paper component="li" key={a.id} withBorder p="sm" style={{ borderLeft: `4px solid var(--mantine-color-${colorSeveridad(a.severidad)}-6)` }}>
                    <Group justify="space-between" align="start" wrap="nowrap">
                      <div>
                        <Group gap={6}>
                          <Badge color={colorSeveridad(a.severidad)} leftSection={<IconoSeveridad severidad={a.severidad} tamano={12} />}>
                            {texto(textos.severidad, a.severidad)}
                          </Badge>
                          <Text fw={600} size="sm">
                            {a.titulo}
                          </Text>
                        </Group>
                        <Text size="sm" mt={4}>
                          {a.detalle}
                        </Text>
                        <Text size="xs" c="dimmed" mt={2}>
                          {instante(a.creadaEn)}
                          {a.leidaEn ? ` · leída ${instante(a.leidaEn)}` : ''}
                        </Text>
                      </div>
                      {!a.leidaEn && (
                        <Button size="xs" variant="light" loading={marcar.isPending && marcar.variables === a.id} onClick={() => marcar.mutate(a.id)}
                          aria-label={`Marcar como leída: ${a.titulo}`}>
                          Marcar leída
                        </Button>
                      )}
                    </Group>
                  </Paper>
                ))}
              </Stack>
              <Group justify="flex-end" mt="sm">
                <Pagination total={Math.max(1, Math.ceil(resultado.total / TamanoPagina))} value={pagina} onChange={setPagina}
                  getControlProps={(control) => ({ 'aria-label': control === 'next' ? 'Página siguiente' : 'Página anterior' })} />
              </Group>
            </>
          )
        }
      </Consulta>
    </>
  );
}
