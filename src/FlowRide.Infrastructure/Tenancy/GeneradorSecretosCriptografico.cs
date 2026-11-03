using System.Security.Cryptography;
using FlowRide.Application.Tenancy.Salida.Puertos;

namespace FlowRide.Infrastructure.Tenancy;

public sealed class GeneradorSecretosCriptografico : IGeneradorSecretos
{
    /// <summary>32 bytes aleatorios en Base64 URL: sin comillas, punto y coma ni espacios, seguros en una cadena de conexión.</summary>
    public string NuevaContrasena() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
