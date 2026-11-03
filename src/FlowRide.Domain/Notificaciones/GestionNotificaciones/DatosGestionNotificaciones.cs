namespace FlowRide.Domain.Notificaciones.GestionNotificaciones;

// Datos de entrada de los casos de uso de notificaciones (RF-16, RF-17, RF-35, RF-43 a RF-45; ADR-0015, ADR-0016).

/// <summary>Un evento de la Outbox para convertirlo en notificaciones: tipo, contenido JSON y su identificador.</summary>
public sealed record ProcesarEventoDominio(Guid EventId, string TipoEvento, string Contenido, DateTimeOffset OcurridoEn);

public sealed record EnviarCorreoDominio(Guid CorreoId);

public sealed record ConsultarCorreosPendientesDominio(int Maximo);

public sealed record BuscarAlertasDominio(bool SoloNoLeidas, Comun.Consultas.Paginacion Paginacion);

public sealed record AlertasPosterioresDominio(long DespuesDe, int Maximo);

public sealed record MarcarAlertaLeidaDominio(Guid AlertaId, Guid ActorId);

public sealed record ConsultarPlantillasDominio
{
    public static ConsultarPlantillasDominio Todas { get; } = new();
}

public sealed record ActualizarPlantillaDominio(string Clave, string Nombre, string Idioma, EstadoPlantilla Estado, Guid ActorId);
