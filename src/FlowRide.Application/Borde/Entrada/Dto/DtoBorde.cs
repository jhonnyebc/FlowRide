using FlowRide.Domain.Plataforma;

namespace FlowRide.Application.Borde.Entrada.Dto;

/// <summary>Una ruta publicada por el borde: el host público del operador y la URL de su stack.</summary>
public sealed record RutaBordeRespuesta(string TenantId, string Host, Uri UrlStack);

/// <summary>
/// Foto de la tabla de ruteo que el borde usa hasta la próxima recarga: las rutas para el proxy y, por dentro, la tabla
/// del dominio para enrutar los webhooks por número.
/// </summary>
public sealed class RutasVigentes
{
    internal RutasVigentes(TablaRutas tabla, string dominioBase)
    {
        Tabla = tabla;
        DominioBase = dominioBase;
        Rutas = tabla.Rutas.Select(r => new RutaBordeRespuesta(r.OperadorId, r.Host(dominioBase), new Uri(r.UrlStack))).ToList();
    }

    public static RutasVigentes Vacias(string dominioBase) => new(TablaRutas.Vacia, dominioBase);

    public string DominioBase { get; }

    public IReadOnlyList<RutaBordeRespuesta> Rutas { get; }

    internal TablaRutas Tabla { get; }
}

public sealed record DestinoWebhook(string TenantId, Uri UrlStack);

/// <summary>Resultado del router de webhooks: si se acepta y a qué stacks se reenvía.</summary>
public sealed record WebhookEnrutadoRespuesta(
    bool FirmaValida,
    bool FormatoValido,
    IReadOnlyList<DestinoWebhook> Destinos,
    IReadOnlyList<string> NumerosSinOperador);
