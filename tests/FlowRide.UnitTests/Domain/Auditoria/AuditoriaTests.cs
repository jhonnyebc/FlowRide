using System.Reflection;
using FlowRide.Domain.Auditoria;

namespace FlowRide.UnitTests.Domain.Auditoria;

public class EventoAuditoriaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = new("0199f000-0000-7000-8000-000000000001");

    internal static EventoAuditoria Evento(string recursoId = "u-1", string? despues = "true") => new(
        Actor, AccionesAuditoria.UsuarioDesactivar, AccionesAuditoria.RecursoUsuario, recursoId, Ahora,
        ResultadoAuditoria.Exitoso,
        valoresAntes: new Dictionary<string, string?> { ["rol"] = "Administrador", ["activo"] = "false" },
        valoresDespues: despues is null ? null : new Dictionary<string, string?> { ["activo"] = despues });

    [Fact]
    public void Constructor_RecortaTruncaAMicrosegundosYOrdenaLosValores()
    {
        var evento = new EventoAuditoria(null, " usuario.crear ", " usuario ", " u-1 ", Ahora.AddTicks(7),
            ResultadoAuditoria.Fallido, motivo: "  ", valoresDespues: new Dictionary<string, string?> { ["b"] = "2", ["a"] = null });

        Assert.Equal("usuario.crear", evento.Accion);
        Assert.Equal("u-1", evento.RecursoId);
        Assert.Equal(Ahora, evento.FechaHora);
        Assert.Null(evento.Motivo);
        Assert.Null(evento.ValoresAntes);
        Assert.Equal("""{"a":null,"b":"2"}""", evento.ValoresDespues);
        Assert.False(evento.Sellado);
        Assert.False(evento.HashValido());
    }

    [Fact]
    public void Sellar_EncadenaConElHashPrevio()
    {
        var primero = Evento();
        primero.Sellar(EventoAuditoria.HashInicial);
        var segundo = Evento();
        segundo.Sellar(primero.Hash);

        Assert.True(primero.HashValido());
        Assert.Equal(64, primero.Hash.Length);
        Assert.Equal(primero.Hash, segundo.HashPrevio);
        Assert.NotEqual(primero.Hash, segundo.Hash);
    }

    [Fact]
    public void Sellar_DosVecesOConHashInvalido_Falla()
    {
        var evento = Evento();
        Assert.Throws<ArgumentException>(() => evento.Sellar("abc"));

        evento.Sellar(EventoAuditoria.HashInicial);
        Assert.Throws<InvalidOperationException>(() => evento.Sellar(EventoAuditoria.HashInicial));
    }

    [Fact]
    public void ElHash_ToleraElFormatoDeJsonb_PeroNoUnCambioDeValor()
    {
        var evento = Evento();
        evento.Sellar(EventoAuditoria.HashInicial);

        // jsonb devuelve las claves reordenadas y con espacios: el contenido es el mismo.
        Asignar(evento, nameof(EventoAuditoria.ValoresAntes), """{"activo": "false", "rol": "Administrador"}""");
        Assert.True(evento.HashValido());

        Asignar(evento, nameof(EventoAuditoria.ValoresAntes), """{"activo": "true", "rol": "Administrador"}""");
        Assert.False(evento.HashValido());
    }

    [Fact]
    public void Constructor_SinAccion_Falla() =>
        Assert.Throws<ArgumentException>(() => new EventoAuditoria(null, " ", "usuario", "u-1", Ahora, ResultadoAuditoria.Exitoso));

    internal static void Asignar(EventoAuditoria evento, string propiedad, object? valor) =>
        typeof(EventoAuditoria).GetProperty(propiedad, BindingFlags.Instance | BindingFlags.Public)!.SetValue(evento, valor);
}

public class CadenaAuditoriaTests
{
    private static List<EventoAuditoria> Cadena(int cantidad)
    {
        var eventos = new List<EventoAuditoria>();
        var anterior = EventoAuditoria.HashInicial;
        for (var i = 1; i <= cantidad; i++)
        {
            var evento = EventoAuditoriaTests.Evento($"u-{i}");
            evento.Sellar(anterior);
            EventoAuditoriaTests.Asignar(evento, nameof(EventoAuditoria.Id), (long)i);
            anterior = evento.Hash;
            eventos.Add(evento);
        }
        return eventos;
    }

    [Fact]
    public void CadenaVaciaOIntegra_NoTieneEventosAlterados()
    {
        Assert.Null(CadenaAuditoria.PrimerEventoAlterado([]));
        Assert.Null(CadenaAuditoria.PrimerEventoAlterado(Cadena(3)));
    }

    [Fact]
    public void EventoModificado_SeDetecta()
    {
        var cadena = Cadena(3);
        EventoAuditoriaTests.Asignar(cadena[1], nameof(EventoAuditoria.Accion), AccionesAuditoria.UsuarioActivar);

        Assert.Equal(2, CadenaAuditoria.PrimerEventoAlterado(cadena)!.Id);
    }

    [Fact]
    public void EventoBorrado_RompeLaCadenaEnElSiguiente()
    {
        var cadena = Cadena(3);
        cadena.RemoveAt(1);

        Assert.Equal(3, CadenaAuditoria.PrimerEventoAlterado(cadena)!.Id);
    }

    [Fact]
    public void PrimerEventoQueNoParteDelHashInicial_SeDetecta()
    {
        var cadena = Cadena(2);
        cadena.RemoveAt(0);

        Assert.Equal(2, CadenaAuditoria.PrimerEventoAlterado(cadena)!.Id);
    }
}
