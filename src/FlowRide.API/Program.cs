using System.Diagnostics;
using System.Text.Json.Serialization;
using FlowRide.API.Asignaciones;
using FlowRide.API.Auditoria;
using FlowRide.API.Canal;
using FlowRide.API.Identidad;
using FlowRide.API.Notificaciones;
using FlowRide.API.Operacion;
using FlowRide.API.Parametros;
using FlowRide.API.Plataforma.Errores;
using FlowRide.API.Plataforma.Observabilidad;
using FlowRide.API.Plataforma.Salud;
using FlowRide.API.Reportes;
using FlowRide.API.Solicitudes;
using FlowRide.API.Viajes;
using FlowRide.Infrastructure;
using FlowRide.Infrastructure.Plataforma;
using FlowRide.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// En local, los secretos del stack viven en el almacén del operador que crea la herramienta de aprovisionamiento
// (equivalente a su Key Vault, ADR-0014). En la nube se leerán del Key Vault del operador.
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddSecretosLocalesDelOperador(builder.Configuration[$"{SiloOptions.Seccion}:TenantId"]);

builder.AddObservabilidad();
builder.Services.AddInfraestructura(builder.Configuration);
builder.Services.AddIdentidad(builder.Configuration);
builder.Services.AddOperacion(builder.Configuration);
builder.Services.AddSolicitudes(builder.Configuration);
builder.Services.AddAsignaciones(builder.Configuration);
builder.Services.AddViajes();
builder.Services.AddCanalWhatsApp(builder.Configuration);
builder.Services.AddNotificaciones(builder.Configuration);
builder.Services.AddReportes(builder.Configuration);
builder.Services.AddAutenticacionFlowRide();
// Números como números (sin aceptarlos como texto): el contrato OpenAPI del Panel Web queda con tipos exactos.
builder.Services.ConfigureHttpJsonOptions(opciones => opciones.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<ManejadorExcepciones>();
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
app.UseAuthentication();
app.UseAuthorization();

// El contrato OpenAPI es la fuente del cliente tipado del Panel Web (ADR-0019); en pruebas se compara con el del frontend.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi().AllowAnonymous();
}

// El TLS termina en el borde común (ADR-0012); el stack del operador no redirige a HTTPS.

// Liveness: el proceso responde. Readiness: además, la base del Silo acepta conexiones (ADR-0010).
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = RespuestaDeSalud.EscribirAsync
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(DependencyInjection.EtiquetaReadiness),
    ResponseWriter = RespuestaDeSalud.EscribirAsync
}).AllowAnonymous();

// Todo lo que cuelga de /api exige un token válido del realm del operador (ADR-0003).
var api = app.MapGroup("/api").RequireAuthorization();
api.MapUsuarios();
api.MapAuditoria();
api.MapOperacion();
api.MapParametros();
api.MapSolicitudes();
api.MapAsignaciones();
api.MapViajes();
api.MapNotificaciones();
api.MapReportes();

// El simulador del canal solo existe en desarrollo y con el proveedor simulado (ADR-0008).
var proveedorWhatsApp = builder.Configuration["FlowRide:WhatsApp:Proveedor"];
api.MapWhatsApp(app.Environment.IsDevelopment()
                && (string.IsNullOrEmpty(proveedorWhatsApp) || string.Equals(proveedorWhatsApp, "Simulado", StringComparison.OrdinalIgnoreCase)));

app.Run();

public partial class Program;
