using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Configuration;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Assignment.Rules.Priority;
using FlowRide.Domain.Assignment.Rules.Scoring;
using FlowRide.Domain.Assignment.Rules.TieBreaking;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Tests.Domain.Assignment;

/// <summary>Datos de prueba deterministas (Ids fijos, fechas fijas) para el motor de asignación.</summary>
internal static class Escenario
{
    public static readonly DateTimeOffset HoraServicio = new(2026, 10, 8, 9, 0, 0, TimeSpan.FromHours(-5));
    public static readonly DateOnly FechaVigente = new(2027, 12, 31);

    public static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    public static SolicitudViaje Solicitud(bool urgente = false, int pasajeros = 2, DateTimeOffset? hora = null) =>
        new(Id(900), hora ?? HoraServicio, pasajeros, urgente);

    public static Candidato Candidato(
        int conductor,
        EstadoConductor estadoConductor = EstadoConductor.Activo,
        DateOnly? vencimientoLicencia = null,
        bool vehiculoActivo = true,
        int capacidad = 4,
        DateOnly? vencimientoSoat = null,
        EstadoTurno estadoTurno = EstadoTurno.Activo,
        DateTimeOffset? inicioTurno = null,
        DateTimeOffset? finTurno = null,
        int asignacionesRecientes = 0,
        DateTimeOffset? ultimaAsignacion = null)
    {
        var conductorId = Id(conductor);
        return new Candidato(
            new Conductor(conductorId, $"LIC-{conductor}", vencimientoLicencia ?? FechaVigente, estadoConductor, ultimaAsignacion),
            new Vehiculo(Id(100 + conductor), $"ABC{conductor:D3}", capacidad, FechaVigente, vencimientoSoat ?? FechaVigente, vehiculoActivo),
            new Turno(
                Id(200 + conductor),
                conductorId,
                inicioTurno ?? HoraServicio.AddHours(-2),
                finTurno ?? HoraServicio.AddHours(6),
                estadoTurno),
            asignacionesRecientes);
    }

    public static IEnumerable<RuleSetting> Eliminatorias() =>
    [
        new(AvailabilityRule.RuleName, true, 1, 1m),
        new(ShiftCompatibilityRule.RuleName, true, 2, 1m),
        new(RegulatoryRestrictionRule.RuleName, true, 3, 1m),
        new(ServiceCapabilityRule.RuleName, true, 4, 1m)
    ];

    public static AssignmentConfiguration Configuracion(
        string tenant = "tenant-a",
        int version = 1,
        decimal pesoCritico = 0.5m,
        decimal pesoCarga = 0.5m,
        bool criticoHabilitado = true,
        bool cargaHabilitada = true,
        bool desempateHabilitado = true) =>
        new(tenant, version,
        [
            .. Eliminatorias(),
            new(CriticalServicePriorityRule.RuleName, criticoHabilitado, 1, pesoCritico),
            new(WorkloadBalanceRule.RuleName, cargaHabilitada, 1, pesoCarga),
            new(StableTieBreaker.RuleName, desempateHabilitado, 1, 1m)
        ]);

    public static AssignmentEngine Motor() => new(DefaultAssignmentRules.Create());

    public static AssignmentContext Contexto(
        SolicitudViaje solicitud,
        AssignmentConfiguration configuracion,
        params Candidato[] candidatos) => new(solicitud, candidatos, configuracion);
}
