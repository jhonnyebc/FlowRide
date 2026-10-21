using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Operacion.GestionRecursos;

// Datos de entrada de los casos de uso de recursos operativos (RF-04, RF-05, RF-07, RF-26 a RF-28, RF-38, RF-41).
// ActorId es el usuario interno que ejecuta la acción (queda en la auditoría).

public sealed record RegistrarEmpresaClienteDominio(string Nombre, string Nit, string CorreoContacto, Guid? ActorId);

public sealed record ActualizarEmpresaClienteDominio(Guid EmpresaId, string Nombre, string CorreoContacto, Guid? ActorId);

public sealed record CambiarEstadoEmpresaClienteDominio(Guid EmpresaId, bool Activa, Guid? ActorId);

public sealed record RegistrarPasajeroDominio(
    string Nombre,
    string TelefonoWhatsApp,
    string? Correo,
    Guid EmpresaClienteId,
    string CodigoInterno,
    Guid? ActorId);

public sealed record ActualizarPasajeroDominio(Guid PasajeroId, string Nombre, string TelefonoWhatsApp, string? Correo, Guid? ActorId);

public sealed record RegistrarConductorDominio(
    string Nombre,
    string TelefonoWhatsApp,
    string? Correo,
    string NumeroLicencia,
    string CategoriaLicencia,
    DateOnly VencimientoLicencia,
    Guid? ActorId,
    bool AptoServicioCritico = false);

public sealed record ActualizarConductorDominio(
    Guid ConductorId,
    string Nombre,
    string TelefonoWhatsApp,
    string? Correo,
    string NumeroLicencia,
    string CategoriaLicencia,
    DateOnly VencimientoLicencia,
    Guid? ActorId,
    bool AptoServicioCritico = false);

public sealed record CambiarEstadoConductorDominio(Guid ConductorId, EstadoConductor Estado, Guid? ActorId);

public sealed record ProgramarTurnoDominio(Guid ConductorId, FechaHoraLocal Inicio, FechaHoraLocal Fin, Guid? ActorId);

/// <summary>Archivo ya validado por el adaptador de entrada (tipo de contenido y tamaño).</summary>
public sealed record SubirDocumentoConductorDominio(
    Guid ConductorId,
    TipoDocumento Tipo,
    DateOnly? FechaVencimiento,
    string NombreArchivo,
    string TipoContenido,
    Stream Contenido,
    Guid? ActorId);

public sealed record DocumentoConductorRefDominio(Guid ConductorId, Guid DocumentoId, Guid? ActorId);

public sealed record RegistrarVehiculoDominio(
    string Placa,
    string Marca,
    string Modelo,
    int Capacidad,
    DateOnly VencimientoRtm,
    DateOnly VencimientoSoat,
    Guid? ActorId);

public sealed record ActualizarVehiculoDominio(
    Guid VehiculoId,
    string Marca,
    string Modelo,
    int Capacidad,
    DateOnly VencimientoRtm,
    DateOnly VencimientoSoat,
    Guid? ActorId);

public sealed record CambiarEstadoVehiculoDominio(Guid VehiculoId, bool Activo, Guid? ActorId);

public sealed record AsignarConductorHabitualDominio(Guid VehiculoId, Guid? ConductorId, Guid? ActorId);

public sealed record ConsultarDisponibilidadDominio(FechaHoraLocal Inicio, FechaHoraLocal Fin);

public sealed record BuscarEmpresasClienteDominio(FiltroRecursos Filtro, Paginacion Paginacion);

public sealed record BuscarPasajerosDominio(FiltroPasajeros Filtro, Paginacion Paginacion);

public sealed record BuscarConductoresDominio(FiltroConductores Filtro, Paginacion Paginacion);

public sealed record BuscarVehiculosDominio(FiltroRecursos Filtro, Paginacion Paginacion);
