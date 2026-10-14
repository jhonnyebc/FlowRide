using System.Diagnostics;
using FlowRide.API.Plataforma.Observabilidad;
using FlowRide.API.Plataforma.Salud;
using FlowRide.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservabilidad();
builder.Services.AddInfraestructura(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        // Mismo traceId que en los logs, para correlacionar un error reportado con su traza (ADR-0011).
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
    });

var app = builder.Build();

app.UseMiddleware<RegistroDeSolicitudesMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// El TLS termina en el borde común (ADR-0012); el stack del operador no redirige a HTTPS.

// Liveness: el proceso responde. Readiness: además, la base del Silo acepta conexiones (ADR-0010).
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = RespuestaDeSalud.EscribirAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(DependencyInjection.EtiquetaReadiness),
    ResponseWriter = RespuestaDeSalud.EscribirAsync
});

app.Run();

public partial class Program;
