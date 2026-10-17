using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Tenancy.CasosUso;
using FlowRide.Application.Tenancy.Entrada.Dto;
using FlowRide.Application.Tenancy.Entrada.Puertos;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Plataforma.MigrarSilos;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Tenancy.Entrada.Adaptadores;

/// <summary>Valida la petición, la convierte en datos del dominio, ejecuta el caso de uso y arma la respuesta.</summary>
public sealed class AprovisionarOperadorAdaptadorEntrada(
    ICasoUsoConRetorno<AprovisionarOperadorDominio, ResultadoAprovisionamiento> casoUso) : IAprovisionarOperadorPuertoEntrada
{
    public async Task<AprovisionarOperadorRespuesta> AprovisionarAsync(AprovisionarOperadorPeticion peticion, CancellationToken ct)
    {
        if (peticion is null
            || string.IsNullOrWhiteSpace(peticion.TenantId)
            || string.IsNullOrWhiteSpace(peticion.RazonSocial)
            || string.IsNullOrWhiteSpace(peticion.Nit)
            || string.IsNullOrWhiteSpace(peticion.CorreoContacto))
        {
            throw new FlowRideExcepcion("tenantId, razonSocial, nit y correoContacto son obligatorios.");
        }

        var resultado = await casoUso.EjecutarAsync(new AprovisionarOperadorDominio(
            peticion.TenantId.Trim(), peticion.RazonSocial.Trim(), peticion.Nit.Trim(), peticion.CorreoContacto.Trim()), ct);

        var operador = resultado.Operador;
        return new AprovisionarOperadorRespuesta(
            operador.Id,
            operador.Estado.ToString(),
            operador.NombreSilo,
            operador.AmbitoSecretos,
            operador.NombreRealm,
            resultado.OperadorNuevo,
            resultado.ContrasenaNueva,
            resultado.MigracionesAplicadas,
            resultado.OperadorNuevo ? "Operador aprovisionado correctamente." : "Operador actualizado; su Silo ya estaba aprovisionado.");
    }
}

public sealed class MigrarSilosAdaptadorEntrada(
    ICasoUsoConRetorno<MigrarSilosDominio, IReadOnlyList<ResultadoMigracion>> casoUso) : IMigrarSilosPuertoEntrada
{
    public async Task<MigrarSilosRespuesta> MigrarAsync(MigrarSilosPeticion peticion, CancellationToken ct)
    {
        if (peticion is null)
            throw new FlowRideExcepcion("La petición de migración es obligatoria.");

        var datos = string.IsNullOrWhiteSpace(peticion.TenantId)
            ? MigrarSilosDominio.Todos
            : new MigrarSilosDominio(peticion.TenantId.Trim());

        var resultados = await casoUso.EjecutarAsync(datos, ct);
        return new MigrarSilosRespuesta(resultados
            .Select(resultado => new MigracionSiloRespuesta(resultado.TenantId, resultado.MigracionesAplicadas, resultado.Error))
            .ToList());
    }
}

public sealed class ConsultarOperadoresAdaptadorEntrada(
    ICasoUsoConRetorno<EstadoOperador?, IReadOnlyList<Operador>> casoUso) : IConsultarOperadoresPuertoEntrada
{
    public async Task<IReadOnlyList<OperadorRespuesta>> ConsultarAsync(CancellationToken ct) =>
        (await casoUso.EjecutarAsync(null, ct))
        .Select(operador => new OperadorRespuesta(
            operador.Id, operador.RazonSocial, operador.Nit, operador.CorreoContacto, operador.Estado.ToString()))
        .ToList();
}
