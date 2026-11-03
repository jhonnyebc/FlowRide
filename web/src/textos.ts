// Textos del Panel Web en es-CO (ADR-0016). Las claves agrupan por pantalla; los textos de los enums de la API viven
// aquí para que nunca se muestre un nombre técnico (`SinCandidatos`) al usuario.

export const textos = {
  app: { nombre: 'FlowRide', saltarAlContenido: 'Saltar al contenido', cargando: 'Cargando…', reintentar: 'Reintentar' },
  menu: {
    operacion: 'Operación',
    tablero: 'Tablero',
    vistaDelDia: 'Vista del día',
    solicitudes: 'Solicitudes',
    viajesEnCurso: 'Viajes en curso',
    alertas: 'Alertas',
    administracion: 'Administración',
    usuarios: 'Usuarios',
    empresas: 'Empresas cliente',
    pasajeros: 'Pasajeros',
    conductores: 'Conductores',
    vehiculos: 'Vehículos',
    itinerarios: 'Itinerarios',
    reportes: 'Reportes',
    configuracion: 'Configuración',
    parametros: 'Parámetros',
    plantillas: 'Plantillas de WhatsApp',
    motor: 'Motor de asignación',
    cerrarSesion: 'Cerrar sesión',
    temaClaro: 'Usar tema claro',
    temaOscuro: 'Usar tema oscuro',
    abrirMenu: 'Abrir el menú',
  },
  roles: { Administrador: 'Administrador', OperadorLogistico: 'Operador logístico' } as Record<string, string>,
  sesion: {
    iniciando: 'Abriendo el inicio de sesión…',
    sinAcceso: 'Tu usuario no tiene acceso al Panel Web de este operador.',
    reautenticar: 'Por seguridad, vuelve a iniciar sesión para esta acción.',
  },
  estadoSolicitud: {
    Pendiente: 'Pendiente',
    Asignada: 'Asignada',
    SinCandidatos: 'Sin candidatos',
    EnCurso: 'En curso',
    Completada: 'Completada',
    Cancelada: 'Cancelada',
  } as Record<string, string>,
  estadoViaje: { Programado: 'Programado', EnCurso: 'En curso', Finalizado: 'Finalizado', Cancelado: 'Cancelado' } as Record<string, string>,
  estadoAsignacion: {
    Asignada: 'Esperando al conductor',
    Aceptada: 'Aceptada',
    Rechazada: 'Rechazada',
    EnCurso: 'En curso',
    Finalizada: 'Finalizada',
    Cancelada: 'Cancelada',
    Reemplazada: 'Reemplazada',
  } as Record<string, string>,
  nivel: { Critico: 'Crítico', Atencion: 'Atención', Normal: 'Normal' } as Record<string, string>,
  severidad: { Critica: 'Crítica', Advertencia: 'Advertencia', Info: 'Información' } as Record<string, string>,
  canal: { WhatsApp: 'WhatsApp', PanelWeb: 'Panel Web' } as Record<string, string>,
  origenAsignacion: { Automatica: 'Motor automático', Manual: 'Manual' } as Record<string, string>,
  estadoConductor: { Activo: 'Activo', Suspendido: 'Suspendido', Inactivo: 'Inactivo' } as Record<string, string>,
  estadoPlantilla: { Pendiente: 'Pendiente', Aprobada: 'Aprobada', Rechazada: 'Rechazada' } as Record<string, string>,
  estadoConfiguracion: { Borrador: 'Borrador', Activa: 'Activa', Archivada: 'Archivada' } as Record<string, string>,
  tipoRegla: { Filtering: 'Eliminatoria', Priority: 'Prioridad', Scoring: 'Puntuación', TieBreak: 'Desempate' } as Record<string, string>,
  // Reglas del motor (ADR-0009): el código es el nombre de la clase, en inglés como en el ADR.
  regla: {
    AvailabilityRule: 'Disponibilidad del conductor y el vehículo',
    RegulatoryRestrictionRule: 'Restricciones regulatorias (licencia, RTM y SOAT)',
    ServiceCapabilityRule: 'Capacidad del vehículo para el servicio',
    ShiftCompatibilityRule: 'Turno que cubre el servicio',
    CriticalServicePriorityRule: 'Prioridad en servicios críticos',
    WorkloadBalanceRule: 'Balance de carga entre conductores',
    StableTieBreaker: 'Desempate estable',
  } as Record<string, string>,
  parametro: {
    'recordatorio.anticipacion_minutos': 'Anticipación del recordatorio',
    'solicitud.ventana_cancelacion_minutos': 'Ventana para modificar o cancelar',
    'operacion.franjas_criticas': 'Franjas críticas',
    'solicitud.visibilidad_personales': 'Visibilidad de las solicitudes personales',
    'asignacion.plazo_respuesta_minutos': 'Plazo de respuesta del conductor',
    'calificacion.plazo_dias': 'Plazo para calificar el viaje',
    'reporte.tolerancia_puntualidad_minutos': 'Tolerancia de puntualidad',
  } as Record<string, string>,
  opcionParametro: {
    PasajeroYOperadorLogistico: 'El pasajero y el Operador logístico',
    SoloPasajero: 'Solo el pasajero',
  } as Record<string, string>,
  grupoSolicitud: { Atendida: 'Atendidas', Pendiente: 'Pendientes', Cancelada: 'Canceladas' } as Record<string, string>,
  estadoTurno: { Programado: 'Programado', Activo: 'En curso', Finalizado: 'Finalizado', Abandonado: 'Abandonado' } as Record<string, string>,
  tipoDocumento: { Licencia: 'Licencia de conducción', Rtm: 'Revisión técnico-mecánica', Soat: 'SOAT', Otro: 'Otro' } as Record<string, string>,
  tipoIncidente: {
    Retraso: 'Retraso',
    FallaMecanica: 'Falla mecánica',
    Accidente: 'Accidente',
    AbandonoTurno: 'Abandono de turno',
    Otro: 'Otro',
  } as Record<string, string>,
} as const;

/** Texto de un valor de la API; si no hay traducción, el valor tal cual (nunca vacío). */
export function texto(grupo: Record<string, string>, valor: string | null | undefined): string {
  if (!valor) return '—';
  return grupo[valor] ?? valor;
}
