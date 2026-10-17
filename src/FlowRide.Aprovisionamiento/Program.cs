using FlowRide.Aprovisionamiento;
using FlowRide.Infrastructure;
using FlowRide.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Herramienta de plataforma: aprovisiona y migra los Silos de los operadores (RF-03, ADR-0010).
// Secretos requeridos (user-secrets de este proyecto o variables de entorno, ADR-0014):
//   ConnectionStrings:Catalogo                → rol del catálogo de plataforma
//   ConnectionStrings:AdministracionServidor  → rol con permiso para crear roles y bases

if (!Comando.TryParse(args, out var comando, out var errorUso))
{
    Console.Error.WriteLine(errorUso);
    Console.Error.WriteLine();
    Console.Error.WriteLine(Comando.Ayuda);
    return 2;
}

if (comando.Nombre == Comando.AyudaNombre)
{
    Console.WriteLine(Comando.Ayuda);
    return 0;
}

var configuracion = new ConfigurationBuilder()
    .AddUserSecrets<Comando>(optional: true)
    .AddEnvironmentVariables()
    .Build();

using var cancelacion = new CancellationTokenSource();
Console.CancelKeyPress += (_, evento) =>
{
    evento.Cancel = true;
    cancelacion.Cancel();
};

try
{
    await using var servicios = new ServiceCollection().AddAprovisionamiento(configuracion).BuildServiceProvider();
    await using var alcance = servicios.CreateAsyncScope();

    // El catálogo es de la plataforma: se deja al día antes de cualquier comando (idempotente).
    await alcance.ServiceProvider.GetRequiredService<CatalogoDbContext>().Database.MigrateAsync(cancelacion.Token);

    return await Comandos.EjecutarAsync(comando, alcance.ServiceProvider, Console.Out, cancelacion.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelado.");
    return 130;
}
catch (Exception ex) when (ex is FlowRide.Domain.Transversal.FlowRideExcepcion or InvalidOperationException
                               or ArgumentException or DbUpdateException or Npgsql.NpgsqlException)
{
    // Sin trazas ni cadenas de conexión en la salida: solo el motivo (ADR-0011).
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
