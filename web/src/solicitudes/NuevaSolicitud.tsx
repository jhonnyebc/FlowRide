import { Alert, Button, Checkbox, Group, Modal, NumberInput, Select, Stack, TextInput } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { type FormEvent, useState } from 'react';
import { useNavigate } from 'react-router';
import { api, datos } from '../api/cliente';

/**
 * Nueva solicitud desde el panel (RF-09). La fecha y hora es local y se envía sin desfase ("2026-10-22T06:00"): la API
 * la guarda con el nombre de la zona del operador (requisito del profesor sobre fechas, 2026-10-20).
 */
export function NuevaSolicitud({ abierta, alCerrar }: { abierta: boolean; alCerrar: () => void }) {
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const [busqueda, setBusqueda] = useState('');
  const [texto] = useDebouncedValue(busqueda, 300);
  const [pasajeroId, setPasajeroId] = useState<string | null>(null);
  const [origen, setOrigen] = useState('');
  const [destino, setDestino] = useState('');
  const [servicio, setServicio] = useState('');
  const [duracion, setDuracion] = useState<number | string>(60);
  const [cantidad, setCantidad] = useState<number | string>(1);
  const [urgente, setUrgente] = useState(false);
  const [personal, setPersonal] = useState(false);

  const pasajeros = useQuery({
    queryKey: ['pasajeros', texto],
    queryFn: () => datos(api.GET('/api/pasajeros', { params: { query: { Texto: texto || undefined, Activo: true, TamanoPagina: 20 } } })),
    enabled: abierta,
  });

  const crear = useMutation({
    mutationFn: () =>
      datos(
        api.POST('/api/solicitudes', {
          body: {
            pasajeroId,
            direccionOrigen: origen,
            direccionDestino: destino,
            fechaHoraServicio: servicio,
            duracionMinutos: Number(duracion),
            cantidadPasajeros: Number(cantidad),
            urgente,
            esPersonal: personal,
          },
        }),
      ),
    onSuccess: (detalle) => {
      notifications.show({ color: 'green', message: 'Solicitud registrada: queda en la cola de asignación.' });
      void cliente.invalidateQueries({ queryKey: ['solicitudes'] });
      alCerrar();
      void navegar(`/solicitudes/${detalle.solicitud.id}`);
    },
  });

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    crear.mutate();
  };

  return (
    <Modal opened={abierta} onClose={alCerrar} title="Nueva solicitud" size="lg">
      <form onSubmit={enviar}>
        <Stack>
          {crear.isError && (
            <Alert color="red" role="alert">
              {crear.error.message}
            </Alert>
          )}
          <Select
            label="Pasajero"
            placeholder="Busca por nombre o código"
            searchable
            required
            searchValue={busqueda}
            onSearchChange={setBusqueda}
            data={(pasajeros.data?.elementos ?? []).map((p) => ({ value: p.id, label: `${p.nombre} · ${p.codigoInterno}` }))}
            value={pasajeroId}
            onChange={setPasajeroId}
            nothingFoundMessage={pasajeros.isFetching ? 'Buscando…' : 'Sin resultados'}
          />
          <TextInput label="Dirección de recogida" required value={origen} onChange={(e) => setOrigen(e.currentTarget.value)} />
          <TextInput label="Dirección de destino" required value={destino} onChange={(e) => setDestino(e.currentTarget.value)} />
          <Group grow>
            <TextInput label="Fecha y hora (hora local)" type="datetime-local" required value={servicio} onChange={(e) => setServicio(e.currentTarget.value)} />
            <NumberInput label="Duración (minutos)" min={1} max={1440} required value={duracion} onChange={setDuracion} />
            <NumberInput label="Pasajeros" min={1} max={60} required value={cantidad} onChange={setCantidad} />
          </Group>
          <Group>
            <Checkbox label="Urgente" checked={urgente} onChange={(e) => setUrgente(e.currentTarget.checked)} />
            <Checkbox label="Personal (dirección visible solo según el parámetro de visibilidad)" checked={personal}
              onChange={(e) => setPersonal(e.currentTarget.checked)} />
          </Group>
          <Group justify="flex-end">
            <Button variant="default" onClick={alCerrar}>
              Cancelar
            </Button>
            <Button type="submit" loading={crear.isPending}>
              Registrar
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
