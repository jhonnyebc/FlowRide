using FlowRide.Application.Tenancy.Entrada.Dto;

namespace FlowRide.Application.Tenancy.Entrada.Puertos;

// Puertos de entrada del módulo Tenancy: lo único que conocen los adaptadores de entrada de infraestructura
// (la herramienta de aprovisionamiento hoy; un endpoint protegido si algún día se necesita).

public interface IAprovisionarOperadorPuertoEntrada
{
    Task<AprovisionarOperadorRespuesta> AprovisionarAsync(AprovisionarOperadorPeticion peticion, CancellationToken ct);
}

public interface IMigrarSilosPuertoEntrada
{
    Task<MigrarSilosRespuesta> MigrarAsync(MigrarSilosPeticion peticion, CancellationToken ct);
}

public interface IConsultarOperadoresPuertoEntrada
{
    Task<IReadOnlyList<OperadorRespuesta>> ConsultarAsync(CancellationToken ct);
}

/// <summary>Rutas del borde común (ADR-0012): subdominio, URL del stack y número de WhatsApp de cada operador.</summary>
public interface IRutasOperadorPuertoEntrada
{
    Task<RutaOperadorRespuesta> RegistrarAsync(RegistrarRutaOperadorPeticion peticion, CancellationToken ct);

    Task<IReadOnlyList<RutaOperadorRespuesta>> ConsultarAsync(CancellationToken ct);
}
