namespace FlowRide.Application.Tenancy.Entrada.Dto;

public sealed record AprovisionarOperadorRespuesta(
    string TenantId,
    string Estado,
    string NombreSilo,
    string AmbitoSecretos,
    string NombreRealm,
    string NombreBucket,
    bool OperadorNuevo,
    bool ContrasenaNueva,
    IReadOnlyList<string> MigracionesAplicadas,
    string Mensaje);

public sealed record MigracionSiloRespuesta(string TenantId, IReadOnlyList<string> MigracionesAplicadas, string? Error)
{
    public bool Exitoso => Error is null;
}

public sealed record MigrarSilosRespuesta(IReadOnlyList<MigracionSiloRespuesta> Silos)
{
    public bool TodosExitosos => Silos.All(silo => silo.Exitoso);
}

public sealed record OperadorRespuesta(string TenantId, string RazonSocial, string Nit, string CorreoContacto, string Estado);

public sealed record RutaOperadorRespuesta(string TenantId, string Subdominio, string UrlStack, string? PhoneNumberId);
