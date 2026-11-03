using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlowRide.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Notificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "clave_mensaje",
                schema: "canal",
                table: "mensaje_saliente",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                schema: "canal",
                table: "mensaje_saliente",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idioma_plantilla",
                schema: "canal",
                table: "mensaje_saliente",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parametros_plantilla",
                schema: "canal",
                table: "mensaje_saliente",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plantilla",
                schema: "canal",
                table: "mensaje_saliente",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alerta",
                schema: "notif",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    secuencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    severidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    detalle = table.Column<string>(type: "text", nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    leida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    leida_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plantilla_whatsapp",
                schema: "notif",
                columns: table => new
                {
                    clave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nombre = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    idioma = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    modificada_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modificada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plantilla_whatsapp", x => x.clave);
                });

            migrationBuilder.CreateTable(
                name: "correo_saliente",
                schema: "notif",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destinatario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    asunto = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    cuerpo = table.Column<string>(type: "text", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    alerta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    proximo_intento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enviado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_error = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correo_saliente", x => x.id);
                    table.ForeignKey(
                        name: "fk_correo_saliente_alerta_alerta_id",
                        column: x => x.alerta_id,
                        principalSchema: "notif",
                        principalTable: "alerta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "notif",
                table: "plantilla_whatsapp",
                columns: new[] { "clave", "estado", "idioma", "modificada_en", "modificada_por_id", "nombre" },
                values: new object[,]
                {
                    { "asignacion.conductor.cancelada", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "asignacion_conductor_cancelada" },
                    { "asignacion.conductor.nueva", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "asignacion_conductor_nueva" },
                    { "asignacion.conductor.retirada", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "asignacion_conductor_retirada" },
                    { "recordatorio.conductor", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "recordatorio_conductor" },
                    { "recordatorio.pasajero", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "recordatorio_pasajero" },
                    { "solicitud.pasajero.cambio_conductor", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "solicitud_pasajero_cambio_conductor" },
                    { "solicitud.pasajero.cancelada", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "solicitud_pasajero_cancelada" },
                    { "solicitud.pasajero.conductor_asignado", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "solicitud_pasajero_conductor_asignado" },
                    { "viaje.pasajero.finalizado", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "viaje_pasajero_finalizado" },
                    { "viaje.pasajero.iniciado", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "viaje_pasajero_iniciado" },
                    { "viaje.pasajero.retraso", "Pendiente", "es_CO", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "viaje_pasajero_retraso" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_saliente_event_id",
                schema: "canal",
                table: "mensaje_saliente",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_alerta_creada_en",
                schema: "notif",
                table: "alerta",
                column: "creada_en",
                filter: "leida_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_alerta_secuencia",
                schema: "notif",
                table: "alerta",
                column: "secuencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_correo_saliente_alerta_id",
                schema: "notif",
                table: "correo_saliente",
                column: "alerta_id");

            migrationBuilder.CreateIndex(
                name: "ix_correo_saliente_estado_proximo_intento",
                schema: "notif",
                table: "correo_saliente",
                columns: new[] { "estado", "proximo_intento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "correo_saliente",
                schema: "notif");

            migrationBuilder.DropTable(
                name: "plantilla_whatsapp",
                schema: "notif");

            migrationBuilder.DropTable(
                name: "alerta",
                schema: "notif");

            migrationBuilder.DropIndex(
                name: "ix_mensaje_saliente_event_id",
                schema: "canal",
                table: "mensaje_saliente");

            migrationBuilder.DropColumn(
                name: "clave_mensaje",
                schema: "canal",
                table: "mensaje_saliente");

            migrationBuilder.DropColumn(
                name: "event_id",
                schema: "canal",
                table: "mensaje_saliente");

            migrationBuilder.DropColumn(
                name: "idioma_plantilla",
                schema: "canal",
                table: "mensaje_saliente");

            migrationBuilder.DropColumn(
                name: "parametros_plantilla",
                schema: "canal",
                table: "mensaje_saliente");

            migrationBuilder.DropColumn(
                name: "plantilla",
                schema: "canal",
                table: "mensaje_saliente");
        }
    }
}
