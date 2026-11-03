import type { Esquemas } from '../api/cliente';

// Datos de ejemplo con la forma exacta del contrato OpenAPI (si la API cambia, estas pruebas dejan de compilar).

export function servicioVista(
  id: string,
  estado: Esquemas['EstadoSolicitud'],
  extra: Partial<Esquemas['SolicitudReporteLectura']> = {},
): Esquemas['SolicitudReporteLectura'] {
  return {
    id,
    pasajeroId: `p-${id}`,
    pasajeroNombre: `Pasajero ${id}`,
    empresaClienteId: 'e-1',
    empresaClienteNombre: 'Minera del Sur',
    direccionOrigen: 'Campamento',
    direccionDestino: 'Mina',
    fechaHoraServicio: '2026-10-21T13:00:00Z',
    cantidadPasajeros: 2,
    urgente: false,
    esPersonal: false,
    canal: 'WhatsApp',
    estado,
    registradaEn: '2026-10-20T12:00:00Z',
    asignacionId: null,
    estadoAsignacion: null,
    conductorId: null,
    conductorNombre: null,
    placa: null,
    viajeId: null,
    estadoViaje: null,
    inicioReal: null,
    finReal: null,
    incidentes: 0,
    puntaje: null,
    detalleRestringido: false,
    ...extra,
  };
}

export function solicitud(id: string, estado: string, extra: Partial<Esquemas['SolicitudRespuesta']> = {}): Esquemas['SolicitudRespuesta'] {
  return {
    id,
    pasajeroId: 'p-1',
    pasajeroNombre: 'Ana Gómez',
    empresaClienteId: 'e-1',
    empresaClienteNombre: 'Minera del Sur',
    itinerarioId: null,
    direccionOrigen: 'Hotel Central',
    direccionDestino: 'Aeropuerto',
    fechaHoraServicio: '2026-10-22T11:00:00Z',
    horaLocalServicio: '2026-10-22T06:00:00',
    zonaHoraria: 'America/Bogota',
    duracionMinutos: 45,
    cantidadPasajeros: 1,
    urgente: false,
    esPersonal: false,
    canal: 'PanelWeb',
    estado,
    registradaEn: '2026-10-20T12:00:00Z',
    detalleRestringido: false,
    ...extra,
  };
}

export function viaje(id: string, estado: Esquemas['EstadoViaje'], extra: Partial<Esquemas['ViajeLectura']> = {}): Esquemas['ViajeLectura'] {
  return {
    id,
    asignacionId: `a-${id}`,
    solicitudId: `s-${id}`,
    estado,
    conductorId: 'c-1',
    conductorNombre: 'Carlos Pérez',
    vehiculoId: 'v-1',
    placa: 'ABC123',
    pasajeroId: 'p-1',
    pasajeroNombre: 'Ana Gómez',
    empresaClienteId: null,
    empresaClienteNombre: null,
    direccionOrigen: 'Hotel',
    direccionDestino: 'Aeropuerto',
    urgente: false,
    esPersonal: false,
    inicioProgramado: '2026-10-21T13:00:00Z',
    finProgramado: '2026-10-21T13:45:00Z',
    fechaHoraInicio: null,
    fechaHoraFin: null,
    incidentes: 0,
    puntaje: null,
    detalleRestringido: false,
    ...extra,
  };
}

export function alerta(id: string, severidad: Esquemas['SeveridadAlerta'], leida = false): Esquemas['AlertaLectura'] {
  return {
    id,
    secuencia: 1,
    tipo: 'solicitud.sin_candidatos',
    severidad,
    titulo: `Alerta ${id}`,
    detalle: 'Detalle de la alerta.',
    creadaEn: '2026-10-21T12:00:00Z',
    referenciaId: null,
    leidaEn: leida ? '2026-10-21T12:05:00Z' : null,
  };
}

export const pagina = <T,>(elementos: T[], total = elementos.length) => ({ elementos, total, pagina: 1, tamanoPagina: 25 });

export function asignacion(
  id: string,
  estado: Esquemas['EstadoAsignacion'],
  extra: Partial<Esquemas['AsignacionLectura']> = {},
): Esquemas['AsignacionLectura'] {
  return {
    id,
    solicitudId: 's-1',
    conductorId: 'c-1',
    conductorNombre: 'Carlos Pérez',
    vehiculoId: 'v-1',
    placa: 'ABC123',
    inicio: '2026-10-22T11:00:00Z',
    fin: '2026-10-22T11:45:00Z',
    estado,
    origen: 'Automatica',
    asignadaEn: '2026-10-21T16:34:00Z',
    motivoCierre: null,
    asignacionReemplazadaId: null,
    ...extra,
  };
}

export function indicadores(extra: Partial<Esquemas['IndicadoresLectura']> = {}): Esquemas['IndicadoresLectura'] {
  return {
    desde: '2026-10-21T05:00:00Z',
    hasta: '2026-10-22T05:00:00Z',
    volumen: {
      solicitudes: 12,
      atendidas: 9,
      pendientes: 2,
      canceladas: 1,
      urgentes: 3,
      solicitudesPorEstado: { Completada: 9, Pendiente: 2, Cancelada: 1 },
      viajesRealizados: 9,
      viajesCancelados: 0,
    },
    cumplimiento: { viajesIniciados: 10, puntuales: 8, porcentajePuntualidad: 80, retrasoPromedioMinutos: 4, incidentes: 1, toleranciaMinutos: 10 },
    conductores: {
      conductoresConAsignaciones: 3,
      asignaciones: 11,
      porcentajeAceptacion: 90.9,
      porcentajeRechazo: 9.1,
      porcentajeVencidas: 0,
      calificacionPromedio: 4.6,
      masViajes: [
        {
          conductorId: 'c-1',
          nombre: 'Carlos Pérez',
          placas: 'ABC123',
          asignaciones: 5,
          aceptadas: 5,
          rechazadas: 0,
          vencidas: 0,
          sinRespuesta: 0,
          porcentajeAceptacion: 100,
          viajesRealizados: 5,
          puntuales: 5,
          porcentajePuntualidad: 100,
          incidentes: 0,
          calificacionPromedio: 4.8,
        },
      ],
    },
    empresas: [
      {
        empresaClienteId: 'e-1',
        nombre: 'Minera del Sur',
        solicitudes: 10,
        atendidas: 8,
        pendientes: 1,
        canceladas: 1,
        urgentes: 2,
        pasajeros: 6,
        viajesRealizados: 8,
        incidentes: 1,
        calificacionPromedio: 4.5,
      },
    ],
    ...extra,
  };
}

export function usuarioPanel(id: string, rol: 'Administrador' | 'OperadorLogistico', extra: Partial<Esquemas['UsuarioRespuesta']> = {}): Esquemas['UsuarioRespuesta'] {
  return {
    id,
    nombre: `Usuario ${id}`,
    correo: `${id}@ops.co`,
    telefonoWhatsApp: '+573001112233',
    rol,
    activo: true,
    creadoEn: '2026-10-17T12:00:00Z',
    ...extra,
  };
}

export function empresa(id: string, extra: Partial<Esquemas['EmpresaClienteRespuesta']> = {}): Esquemas['EmpresaClienteRespuesta'] {
  return { id, nombre: `Empresa ${id}`, nit: `900${id}-1`, correoContacto: `contacto@${id}.co`, activa: true, ...extra };
}

export function pasajero(id: string, extra: Partial<Esquemas['PasajeroRespuesta']> = {}): Esquemas['PasajeroRespuesta'] {
  return {
    id,
    nombre: `Pasajero ${id}`,
    telefonoWhatsApp: '+573005556677',
    correo: null,
    empresaClienteId: 'e-1',
    codigoInterno: `COD-${id}`,
    activo: true,
    ...extra,
  };
}

export function conductor(id: string, extra: Partial<Esquemas['ConductorRespuesta']> = {}): Esquemas['ConductorRespuesta'] {
  return {
    id,
    nombre: `Conductor ${id}`,
    telefonoWhatsApp: '+573007778899',
    correo: null,
    numeroLicencia: `LIC-${id}`,
    categoriaLicencia: 'C1',
    vencimientoLicencia: '2099-12-31',
    estado: 'Activo',
    activo: true,
    aptoServicioCritico: false,
    ...extra,
  };
}

export function turno(id: string, estado: string, extra: Partial<Esquemas['TurnoRespuesta']> = {}): Esquemas['TurnoRespuesta'] {
  return {
    id,
    inicio: '2026-10-27T11:00:00Z',
    fin: '2026-10-27T19:00:00Z',
    estado,
    inicioLocal: '2026-10-27T06:00:00',
    finLocal: '2026-10-27T14:00:00',
    zonaHoraria: 'America/Bogota',
    ...extra,
  };
}

export function documento(id: string, tipo: string, fechaVencimiento: string | null = null): Esquemas['DocumentoConductorRespuesta'] {
  return { id, tipo, fechaVencimiento };
}

export function vehiculo(id: string, extra: Partial<Esquemas['VehiculoRespuesta']> = {}): Esquemas['VehiculoRespuesta'] {
  return {
    id,
    placa: `ABC${id}`,
    marca: 'Renault',
    modelo: 'Master',
    capacidad: 12,
    vencimientoRtm: '2099-06-30',
    vencimientoSoat: '2099-06-30',
    activo: true,
    conductorHabitualId: null,
    ...extra,
  };
}

export function itinerario(id: string, extra: Partial<Esquemas['ItinerarioRespuesta']> = {}): Esquemas['ItinerarioRespuesta'] {
  return {
    id,
    empresaClienteId: 'e-1',
    codigoServicio: 'AV-123',
    fecha: '2026-10-27',
    origen: 'Hotel Central',
    destino: 'Aeropuerto',
    horaSalida: '05:30:00',
    horaLlegada: '06:15:00',
    zonaHoraria: 'America/Bogota',
    ...extra,
  };
}

export function parametroEntero(clave: string, valor: number, extra: Partial<Esquemas['ParametroRespuesta']> = {}): Esquemas['ParametroRespuesta'] {
  return {
    clave,
    tipo: 'Entero',
    descripcion: `Descripción de ${clave}.`,
    valor,
    valorPorDefecto: valor,
    version: 0,
    esPorDefecto: true,
    modificadoEn: null,
    minimo: 1,
    maximo: 1440,
    opciones: [],
    ...extra,
  };
}

export function plantilla(clave: string, estado: string, extra: Partial<Esquemas['PlantillaRespuesta']> = {}): Esquemas['PlantillaRespuesta'] {
  return {
    clave,
    nombre: clave.replaceAll('.', '_'),
    idioma: 'es_CO',
    estado,
    variables: ['conductor', 'placa'],
    modificadaEn: '2026-10-18T00:00:00Z',
    ...extra,
  };
}

/** Las siete reglas sembradas en la v1 del motor. */
export function reglasMotor(): Esquemas['ReglaRespuesta'][] {
  const regla = (codigo: string, tipo: string, extra: Partial<Esquemas['ReglaRespuesta']> = {}): Esquemas['ReglaRespuesta'] => ({
    codigo,
    tipo,
    habilitada: true,
    orden: 1,
    peso: 1,
    parametros: {},
    ...extra,
  });
  return [
    regla('AvailabilityRule', 'Filtering'),
    regla('RegulatoryRestrictionRule', 'Filtering', { orden: 2 }),
    regla('ServiceCapabilityRule', 'Filtering', { orden: 3 }),
    regla('ShiftCompatibilityRule', 'Filtering', { orden: 4 }),
    regla('CriticalServicePriorityRule', 'Priority'),
    regla('WorkloadBalanceRule', 'Scoring', { parametros: { ventanaDias: '7' } }),
    regla('StableTieBreaker', 'TieBreak'),
  ];
}

export function configuracionMotor(version: number, estado: string, extra: Partial<Esquemas['ConfiguracionMotorRespuesta']> = {}): Esquemas['ConfiguracionMotorRespuesta'] {
  return {
    id: `cfg-${version}`,
    version,
    estado,
    creadaEn: '2026-10-18T00:00:00Z',
    activadaEn: estado === 'Borrador' ? null : '2026-10-18T00:00:00Z',
    reglas: reglasMotor(),
    ...extra,
  };
}
