import { Alert, Button, Divider, Drawer, FileInput, Group, Select, Stack, Table, Text, Textarea, TextInput, Title, VisuallyHidden } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconCalendarPlus, IconDownload, IconFileUpload, IconTrash, IconUserX } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { type FormEvent, useState } from 'react';
import { api, datos, type Esquemas } from '../api/cliente';
import { esAdministrador, esOperador, useUsuario } from '../auth/usuario';
import { Consulta, Persona } from '../comun/Componentes';
import { horaLocal } from '../comun/fechas';
import { texto, textos } from '../textos';
import { Confirmacion, InsigniaConductor, InsigniaVencimiento } from './comun';

type Conductor = Esquemas['ConductorRespuesta'];
type Turno = Esquemas['TurnoRespuesta'];
type Documento = Esquemas['DocumentoConductorRespuesta'];

/** Tamaño máximo de un documento; la API lo vuelve a validar junto con el tipo real del archivo. */
const TamanoMaximoDocumento = 5 * 1024 * 1024;
const TiposDocumento = ['Licencia', 'Rtm', 'Soat', 'Otro'] as const;
const TurnoVigente = (t: Turno) => t.estado === 'Programado' || t.estado === 'Activo';

/**
 * Turnos y documentos de un conductor (RF-07, RF-12, RF-26). Programar turnos lo hacen el Administrador y el Operador
 * logístico; registrar un abandono, solo el Operador logístico (es quien coordina el día); los documentos los sube y
 * elimina el Administrador, y ambos los descargan.
 */
export function DetalleConductor({ conductor, alCerrar }: { conductor: Conductor; alCerrar: () => void }) {
  const usuario = useUsuario();
  const cliente = useQueryClient();
  const [porAbandonar, setPorAbandonar] = useState<Turno | null>(null);
  const [porEliminar, setPorEliminar] = useState<Documento | null>(null);

  const consulta = useQuery({
    queryKey: ['conductores', 'detalle', conductor.id],
    queryFn: () => datos(api.GET('/api/conductores/{id}', { params: { path: { id: conductor.id } } })),
  });
  const refrescar = () => void cliente.invalidateQueries({ queryKey: ['conductores'] });

  const descargar = useMutation({
    mutationFn: (d: Documento) =>
      datos(api.GET('/api/conductores/{id}/documentos/{documentoId}/descarga', { params: { path: { id: conductor.id, documentoId: d.id } } })),
    // El enlace es temporal y firmado: se abre en otra pestaña sin darle acceso a esta.
    onSuccess: (u) => void window.open(u.url, '_blank', 'noopener,noreferrer'),
  });

  const eliminar = useMutation({
    mutationFn: (d: Documento) =>
      datos(api.DELETE('/api/conductores/{id}/documentos/{documentoId}', { params: { path: { id: conductor.id, documentoId: d.id } } })),
    onSuccess: () => {
      notifications.show({ color: 'green', message: 'Documento eliminado.' });
      setPorEliminar(null);
      refrescar();
    },
  });

  return (
    <Drawer opened onClose={alCerrar} title={conductor.nombre} position="right" size="xl" padding="lg" closeButtonProps={{ 'aria-label': 'Cerrar' }}>
      <Consulta consulta={consulta}>
        {({ conductor: c, turnos, documentos }) => {
          const vigentes = turnos.filter(TurnoVigente);
          const anteriores = turnos.filter((t) => !TurnoVigente(t)).slice(-5).reverse();
          return (
            <Stack gap="lg">
              <Group justify="space-between" align="start">
                <Persona nombre={c.nombre} detalle={`${c.telefonoWhatsApp} · licencia ${c.numeroLicencia} (${c.categoriaLicencia})`} />
                <InsigniaConductor estado={c.estado} />
              </Group>

              <section aria-labelledby="titulo-turnos">
                <Title order={3} size="h4" id="titulo-turnos" mb="xs">
                  Turnos
                </Title>
                <Text size="sm" c="var(--fr-texto-suave)" mb="sm">
                  El motor solo le asigna servicios que caben completos dentro de un turno vigente. Las horas son las del lugar del turno.
                </Text>
                {vigentes.length === 0 ? (
                  <Text size="sm">No tiene turnos programados ni en curso.</Text>
                ) : (
                  <TablaTurnos
                    turnos={vigentes}
                    puedeAbandonar={esOperador(usuario)}
                    alAbandonar={(t) => setPorAbandonar(t)}
                  />
                )}
                {anteriores.length > 0 && (
                  <>
                    <Text size="sm" fw={600} mt="md" mb="xs">
                      Últimos turnos terminados
                    </Text>
                    <TablaTurnos turnos={anteriores} />
                  </>
                )}
                <ProgramarTurno conductorId={c.id} alGuardar={refrescar} />
              </section>

              <Divider />

              <section aria-labelledby="titulo-documentos">
                <Title order={3} size="h4" id="titulo-documentos" mb="xs">
                  Documentos
                </Title>
                {descargar.isError && (
                  <Alert color="red" role="alert" mb="sm">
                    {descargar.error.message}
                  </Alert>
                )}
                {documentos.length === 0 ? (
                  <Text size="sm">No tiene documentos cargados.</Text>
                ) : (
                  <Table>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Documento</Table.Th>
                        <Table.Th>Vence</Table.Th>
                        <Table.Th>
                          <VisuallyHidden>Acciones</VisuallyHidden>
                        </Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {documentos.map((d) => (
                        <Table.Tr key={d.id}>
                          <Table.Td>{texto(textos.tipoDocumento, d.tipo)}</Table.Td>
                          <Table.Td>
                            <InsigniaVencimiento fecha={d.fechaVencimiento} />
                          </Table.Td>
                          <Table.Td>
                            <Group gap="xs" justify="flex-end" wrap="nowrap">
                              <Button
                                size="xs"
                                variant="default"
                                leftSection={<IconDownload size={14} aria-hidden />}
                                loading={descargar.isPending && descargar.variables?.id === d.id}
                                onClick={() => descargar.mutate(d)}
                                aria-label={`Descargar ${texto(textos.tipoDocumento, d.tipo)}`}
                              >
                                Descargar
                              </Button>
                              {esAdministrador(usuario) && (
                                <Button
                                  size="xs"
                                  variant="subtle"
                                  color="red"
                                  leftSection={<IconTrash size={14} aria-hidden />}
                                  onClick={() => {
                                    eliminar.reset();
                                    setPorEliminar(d);
                                  }}
                                  aria-label={`Eliminar ${texto(textos.tipoDocumento, d.tipo)}`}
                                >
                                  Eliminar
                                </Button>
                              )}
                            </Group>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                )}
                {esAdministrador(usuario) && <SubirDocumento conductorId={c.id} alGuardar={refrescar} />}
              </section>
            </Stack>
          );
        }}
      </Consulta>

      {porAbandonar && (
        <RegistrarAbandono conductor={conductor} turno={porAbandonar} alCerrar={() => setPorAbandonar(null)} alGuardar={refrescar} />
      )}
      {porEliminar && (
        <Confirmacion
          abierta
          alCerrar={() => setPorEliminar(null)}
          titulo="Eliminar documento"
          mensaje={`Se eliminará el documento «${texto(textos.tipoDocumento, porEliminar.tipo)}» de ${conductor.nombre}. Esta acción queda en la auditoría.`}
          textoConfirmar="Eliminar"
          peligrosa
          mutacion={eliminar}
          alConfirmar={() => eliminar.mutate(porEliminar)}
        />
      )}
    </Drawer>
  );
}

function TablaTurnos({ turnos, puedeAbandonar = false, alAbandonar }: { turnos: Turno[]; puedeAbandonar?: boolean; alAbandonar?: (t: Turno) => void }) {
  return (
    <Table>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>Inicio</Table.Th>
          <Table.Th>Fin</Table.Th>
          <Table.Th>Estado</Table.Th>
          {puedeAbandonar && (
            <Table.Th>
              <VisuallyHidden>Acciones</VisuallyHidden>
            </Table.Th>
          )}
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {turnos.map((t) => (
          <Table.Tr key={t.id}>
            <Table.Td>{horaLocal(t.inicioLocal, t.zonaHoraria)}</Table.Td>
            <Table.Td>{horaLocal(t.finLocal, null)}</Table.Td>
            <Table.Td>{texto(textos.estadoTurno, t.estado)}</Table.Td>
            {puedeAbandonar && (
              <Table.Td>
                <Button
                  size="xs"
                  variant="subtle"
                  color="red"
                  leftSection={<IconUserX size={14} aria-hidden />}
                  onClick={() => alAbandonar?.(t)}
                  aria-label={`Registrar abandono del turno que inicia ${horaLocal(t.inicioLocal, null)}`}
                >
                  Registrar abandono
                </Button>
              </Table.Td>
            )}
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  );
}

/** Nuevo turno en hora local del operador, sin desfase ("2026-10-22T06:00"), como pide ADR-0020. */
function ProgramarTurno({ conductorId, alGuardar }: { conductorId: string; alGuardar: () => void }) {
  const [inicio, setInicio] = useState('');
  const [fin, setFin] = useState('');
  const programar = useMutation({
    mutationFn: () => datos(api.POST('/api/conductores/{id}/turnos', { params: { path: { id: conductorId } }, body: { inicio, fin } })),
    onSuccess: (t) => {
      notifications.show({ color: 'green', message: `Turno programado: ${horaLocal(t.inicioLocal, t.zonaHoraria)}.` });
      setInicio('');
      setFin('');
      alGuardar();
    },
  });

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    programar.mutate();
  };

  return (
    <form onSubmit={enviar} aria-label="Programar turno">
      <Stack gap="xs" mt="md">
        {programar.isError && (
          <Alert color="red" role="alert">
            {programar.error.message}
          </Alert>
        )}
        <Group align="end" grow>
          <TextInput label="Inicio del turno" type="datetime-local" required value={inicio} onChange={(e) => setInicio(e.currentTarget.value)} />
          <TextInput label="Fin del turno" type="datetime-local" required value={fin} onChange={(e) => setFin(e.currentTarget.value)} />
        </Group>
        <Group justify="flex-end">
          <Button type="submit" variant="light" leftSection={<IconCalendarPlus size={16} aria-hidden />} loading={programar.isPending}>
            Programar turno
          </Button>
        </Group>
      </Stack>
    </form>
  );
}

/** Abandono de un turno (RF-12): cancela lo que no ha empezado y devuelve esas solicitudes a la cola del motor. */
function RegistrarAbandono({ conductor, turno, alCerrar, alGuardar }: { conductor: Conductor; turno: Turno; alCerrar: () => void; alGuardar: () => void }) {
  const cliente = useQueryClient();
  const [motivo, setMotivo] = useState('');
  const abandonar = useMutation({
    mutationFn: () =>
      datos(
        api.POST('/api/conductores/{conductorId}/turnos/{turnoId}/abandonar', {
          params: { path: { conductorId: conductor.id, turnoId: turno.id } },
          body: { motivo },
        }),
      ),
    onSuccess: (r) => {
      const n = r.solicitudesDevueltas?.length ?? 0;
      notifications.show({
        color: 'green',
        message: n === 0 ? 'Abandono registrado. No tenía servicios pendientes.' : `Abandono registrado: ${n} solicitud(es) volvieron a la cola de asignación.`,
      });
      void cliente.invalidateQueries({ queryKey: ['solicitudes'] });
      alGuardar();
      alCerrar();
    },
  });

  return (
    <Confirmacion
      abierta
      alCerrar={alCerrar}
      titulo="Registrar abandono de turno"
      mensaje={`${conductor.nombre} deja el turno que inicia ${horaLocal(turno.inicioLocal, turno.zonaHoraria)}. Sus servicios que no han empezado se cancelan y las solicitudes vuelven a la cola para que el motor las reasigne; los viajes en curso no se interrumpen.`}
      textoConfirmar="Registrar abandono"
      peligrosa
      mutacion={abandonar}
      alConfirmar={() => abandonar.mutate()}
    >
      <Textarea label="Motivo" required autosize minRows={2} value={motivo} onChange={(e) => setMotivo(e.currentTarget.value)} />
    </Confirmacion>
  );
}

/** Carga un PDF, PNG o JPG de hasta 5 MB al almacenamiento privado del operador (ADR-0013). */
function SubirDocumento({ conductorId, alGuardar }: { conductorId: string; alGuardar: () => void }) {
  const [tipo, setTipo] = useState<string | null>('Licencia');
  const [vence, setVence] = useState('');
  const [archivo, setArchivo] = useState<File | null>(null);
  const [errorLocal, setErrorLocal] = useState<string | null>(null);

  const subir = useMutation({
    mutationFn: () => {
      const formulario = new FormData();
      if (archivo) formulario.append('archivo', archivo);
      if (tipo) formulario.append('tipo', tipo);
      if (vence) formulario.append('fechaVencimiento', vence);
      return datos(
        api.POST('/api/conductores/{id}/documentos', {
          params: { path: { id: conductorId } },
          body: {},
          bodySerializer: () => formulario,
        }),
      );
    },
    onSuccess: (d) => {
      notifications.show({ color: 'green', message: `Documento cargado: ${texto(textos.tipoDocumento, d.tipo)}.` });
      setArchivo(null);
      setVence('');
      alGuardar();
    },
  });

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (archivo && archivo.size > TamanoMaximoDocumento) {
      setErrorLocal('El archivo pesa más de 5 MB.');
      return;
    }
    setErrorLocal(null);
    subir.mutate();
  };

  return (
    <form onSubmit={enviar} aria-label="Cargar documento">
      <Stack gap="xs" mt="md">
        {(errorLocal || subir.isError) && (
          <Alert color="red" role="alert">
            {errorLocal ?? subir.error?.message}
          </Alert>
        )}
        <Group align="end" grow>
          <Select
            label="Tipo de documento"
            data={TiposDocumento.map((t) => ({ value: t, label: texto(textos.tipoDocumento, t) }))}
            value={tipo}
            onChange={setTipo}
            allowDeselect={false}
          />
          <TextInput label="Vence" description="Si aplica" type="date" value={vence} onChange={(e) => setVence(e.currentTarget.value)} />
        </Group>
        <FileInput
          label="Archivo"
          description="PDF, PNG o JPG de hasta 5 MB"
          placeholder="Elige el archivo"
          accept="application/pdf,image/png,image/jpeg"
          required
          clearable
          value={archivo}
          onChange={setArchivo}
        />
        <Group justify="flex-end">
          <Button type="submit" variant="light" leftSection={<IconFileUpload size={16} aria-hidden />} loading={subir.isPending}>
            Cargar documento
          </Button>
        </Group>
      </Stack>
    </form>
  );
}
