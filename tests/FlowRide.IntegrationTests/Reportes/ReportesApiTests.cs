using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using FlowRide.Application.Reportes.Entrada.Dto;
using FlowRide.Application.Reportes.Salida.Puertos;
using FlowRide.Application.Viajes.Entrada.Dto;
using FlowRide.Domain.Reportes;
using FlowRide.IntegrationTests.Asignaciones;
using FlowRide.IntegrationTests.Infraestructura;

namespace FlowRide.IntegrationTests.Reportes;

/// <summary>Reportes, indicadores, vista centralizada y exporte CSV (RF-36, RF-42, RF-46 a RF-49) con PostgreSQL y Azurite reales.</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionAzurite.Nombre)]
public class ReportesApiTests(PostgresFixture postgres, AzuriteFixture azurite) : PruebasAsignacionBase(postgres, azurite)
{
    private static string Periodo() =>
        $"desde={Uri.EscapeDataString(Servicio.AddDays(-1).ToString("O", CultureInfo.InvariantCulture))}" +
        $"&hasta={Uri.EscapeDataString(Servicio.AddDays(1).ToString("O", CultureInfo.InvariantCulture))}";

    /// <summary>Un viaje realizado y calificado, una asignación rechazada y una solicitud que sigue pendiente.</summary>
    private async Task<(Guid ConductorId, Guid ViajeId)> OperacionDelDiaAsync()
    {
        var conductor = await ConductorListoAsync();
        var realizada = await SolicitudAsync();
        var asignacion = await AsignarAsync(realizada);
        var viaje = (await (await Operador.PostAsync($"/api/asignaciones/{asignacion.AsignacionId}/aceptar", null))
            .Content.ReadFromJsonAsync<ViajeRespuesta>())!;
        await Operador.PostAsync($"/api/viajes/{viaje.Id}/iniciar", null);
        await Operador.PostAsync($"/api/viajes/{viaje.Id}/finalizar", null);
        await Operador.PostAsJsonAsync($"/api/viajes/{viaje.Id}/calificacion", new CalificacionPeticion { Puntaje = 4 });

        await SolicitudAsync(Servicio.AddHours(2), urgente: true);
        return (conductor.ConductorId, viaje.Id);
    }

    [Fact]
    public async Task LosReportes_AgreganLaOperacionDelPeriodo()
    {
        var (conductorId, viajeId) = await OperacionDelDiaAsync();

        var viajes = (await Administrador.GetFromJsonAsync<ReporteRespuesta<ResumenViajes, ViajeReporteLectura>>($"/api/reportes/viajes?{Periodo()}"))!;
        var solicitudes = (await Operador.GetFromJsonAsync<ReporteRespuesta<ResumenSolicitudes, SolicitudReporteFila>>(
            $"/api/reportes/solicitudes?{Periodo()}&grupo=Pendiente"))!;
        var conductores = (await Operador.GetFromJsonAsync<ReporteRespuesta<ResumenConductores, ConductorReporteLectura>>(
            $"/api/reportes/conductores?{Periodo()}&conductorId={conductorId}"))!;
        var empresas = (await Administrador.GetFromJsonAsync<ReporteRespuesta<ResumenEmpresas, EmpresaReporteLectura>>(
            $"/api/reportes/empresas?{Periodo()}&empresaClienteId={EmpresaId}"))!;
        var indicadores = (await Administrador.GetFromJsonAsync<IndicadoresLectura>($"/api/reportes/indicadores?{Periodo()}"))!;

        var realizado = Assert.Single(viajes.Filas.Elementos);
        Assert.Equal((viajeId, true), (realizado.Viaje.Id, realizado.Puntual));
        Assert.Equal((1, (decimal?)4.0m), (viajes.Resumen.Viajes, viajes.Resumen.CalificacionPromedio));

        Assert.Equal((2, 1, 1), (solicitudes.Resumen.Total, solicitudes.Resumen.Atendidas, solicitudes.Resumen.Pendientes));
        Assert.Equal(GrupoSolicitud.Pendiente, Assert.Single(solicitudes.Filas.Elementos).Grupo);

        var conductor = Assert.Single(conductores.Filas.Elementos);
        Assert.Equal((conductorId, 1, 1, 1), (conductor.ConductorId, conductor.Asignaciones, conductor.Aceptadas, conductor.ViajesRealizados));
        Assert.Equal((decimal?)4.0m, conductor.CalificacionPromedio);

        var empresa = Assert.Single(empresas.Filas.Elementos);
        Assert.Equal(((Guid?)EmpresaId, "Minera", 2, 1, 1, 2), (empresa.EmpresaClienteId, empresa.Nombre, empresa.Solicitudes, empresa.Urgentes,
            empresa.ViajesRealizados, empresa.Pasajeros));

        Assert.Equal((2, 1, 1), (indicadores.Volumen.Solicitudes, indicadores.Volumen.ViajesRealizados, indicadores.Cumplimiento.ViajesIniciados));
        Assert.Equal(conductorId, Assert.Single(indicadores.Conductores.MasViajes).ConductorId);
    }

    [Fact]
    public async Task LaVistaCentralizada_SemaforizaLosServiciosDelDia()
    {
        await OperacionDelDiaAsync();
        var fecha = TimeZoneInfo.ConvertTime(Servicio, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota")).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var vista = (await Operador.GetFromJsonAsync<VistaCentralizadaLectura>($"/api/reportes/vista-centralizada?fecha={fecha}"))!;

        Assert.Equal((0, 1, 1), (vista.Criticos, vista.Atencion, vista.Normales));
        Assert.Equal(NivelAtencion.Atencion, vista.Servicios[0].Nivel);
        Assert.Equal("Espera asignación.", vista.Servicios[0].Motivo);
    }

    [Fact]
    public async Task Exportar_DejaElCsvEnElBucket_ConEnlaceFirmadoYAuditoria()
    {
        await OperacionDelDiaAsync();
        var peticion = new ReportePeticion { Desde = Servicio.AddDays(-1), Hasta = Servicio.AddDays(1) };

        var respuesta = await Operador.PostAsJsonAsync("/api/reportes/solicitudes/exportar", peticion);
        var invalido = await Operador.PostAsJsonAsync("/api/reportes/facturas/exportar", peticion);
        var sinPeriodo = await Administrador.GetAsync("/api/reportes/viajes");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, sinPeriodo.StatusCode);
        var exporte = (await respuesta.Content.ReadFromJsonAsync<ExporteRespuesta>())!;
        Assert.Equal(("Solicitudes", 2), (exporte.Tipo, exporte.Filas));
        Assert.True(exporte.ExpiraEn <= DateTimeOffset.UtcNow.AddMinutes(15));
        Assert.Contains("/exports/solicitudes/", exporte.Url.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("sig=", exporte.Url.Query, StringComparison.Ordinal);

        using var descarga = new HttpClient();
        var bytes = await descarga.GetByteArrayAsync(exporte.Url);
        var texto = Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("﻿Solicitud;Grupo;Estado;", texto, StringComparison.Ordinal);
        Assert.Equal(3, texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);

        Assert.Equal(1L, await EjecutarEnSiloAsync<long>("SELECT count(*) FROM audit.evento_auditoria WHERE accion = 'reporte.exportar'"));
    }
}
