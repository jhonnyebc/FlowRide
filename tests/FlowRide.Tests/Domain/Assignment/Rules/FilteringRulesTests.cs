using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Enumeraciones;
using static FlowRide.Tests.Domain.Assignment.Escenario;

namespace FlowRide.Tests.Domain.Assignment.Rules;

public class AvailabilityRuleTests
{
    private readonly AvailabilityRule _rule = new();

    private bool Evaluar(Candidato candidato) =>
        _rule.Evaluate(Contexto(Solicitud(), Configuracion(), candidato), candidato).Passed;

    [Fact]
    public void ConductorYVehiculoActivos_Pasa() => Assert.True(Evaluar(Candidato(1)));

    [Theory]
    [InlineData(EstadoConductor.Suspendido)]
    [InlineData(EstadoConductor.Inactivo)]
    public void ConductorNoActivo_Falla(EstadoConductor estado) =>
        Assert.False(Evaluar(Candidato(1, estadoConductor: estado)));

    [Fact]
    public void VehiculoInactivo_Falla() => Assert.False(Evaluar(Candidato(1, vehiculoActivo: false)));
}

public class ShiftCompatibilityRuleTests
{
    private readonly ShiftCompatibilityRule _rule = new();

    private bool Evaluar(Candidato candidato) =>
        _rule.Evaluate(Contexto(Solicitud(), Configuracion(), candidato), candidato).Passed;

    [Theory]
    [InlineData(EstadoTurno.Programado)]
    [InlineData(EstadoTurno.Activo)]
    public void HoraDentroDeTurnoVigente_Pasa(EstadoTurno estado) =>
        Assert.True(Evaluar(Candidato(1, estadoTurno: estado)));

    [Fact]
    public void HoraIgualAlInicioDelTurno_Pasa() =>
        Assert.True(Evaluar(Candidato(1, inicioTurno: HoraServicio)));

    [Fact]
    public void HoraIgualAlFinDelTurno_Falla() =>
        Assert.False(Evaluar(Candidato(1, inicioTurno: HoraServicio.AddHours(-8), finTurno: HoraServicio)));

    [Fact]
    public void TurnoPosteriorALaSolicitud_Falla() =>
        Assert.False(Evaluar(Candidato(1, inicioTurno: HoraServicio.AddHours(1), finTurno: HoraServicio.AddHours(9))));

    [Theory]
    [InlineData(EstadoTurno.Finalizado)]
    [InlineData(EstadoTurno.Abandonado)]
    public void TurnoNoVigente_Falla(EstadoTurno estado) =>
        Assert.False(Evaluar(Candidato(1, estadoTurno: estado)));
}

public class RegulatoryRestrictionRuleTests
{
    private readonly RegulatoryRestrictionRule _rule = new();

    private bool Evaluar(Candidato candidato) =>
        _rule.Evaluate(Contexto(Solicitud(), Configuracion(), candidato), candidato).Passed;

    [Fact]
    public void LicenciaYDocumentosVigentes_Pasa() => Assert.True(Evaluar(Candidato(1)));

    [Fact]
    public void LicenciaQueVenceElDiaDelServicio_Pasa() =>
        Assert.True(Evaluar(Candidato(1, vencimientoLicencia: DateOnly.FromDateTime(HoraServicio.DateTime))));

    [Fact]
    public void LicenciaVencida_Falla() =>
        Assert.False(Evaluar(Candidato(1, vencimientoLicencia: new DateOnly(2026, 10, 7))));

    [Fact]
    public void SoatVencido_Falla() =>
        Assert.False(Evaluar(Candidato(1, vencimientoSoat: new DateOnly(2026, 1, 1))));
}

public class ServiceCapabilityRuleTests
{
    private readonly ServiceCapabilityRule _rule = new();

    private bool Evaluar(int capacidad, int pasajeros)
    {
        var candidato = Candidato(1, capacidad: capacidad);
        return _rule.Evaluate(Contexto(Solicitud(pasajeros: pasajeros), Configuracion(), candidato), candidato).Passed;
    }

    [Theory]
    [InlineData(4, 3, true)]
    [InlineData(4, 4, true)]
    [InlineData(4, 5, false)]
    public void CapacidadMayorOIgualAPasajeros(int capacidad, int pasajeros, bool esperado) =>
        Assert.Equal(esperado, Evaluar(capacidad, pasajeros));
}
