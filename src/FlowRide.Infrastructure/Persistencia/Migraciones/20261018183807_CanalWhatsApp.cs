using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowRide.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CanalWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "canal");

            migrationBuilder.CreateTable(
                name: "conversacion",
                schema: "canal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    telefono = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    paso = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ultima_actividad = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    datos = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversacion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mensaje_entrante",
                schema: "canal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_externo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    remitente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    contenido = table.Column<string>(type: "text", nullable: false),
                    recibido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    procesado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ultimo_error = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mensaje_entrante", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mensaje_saliente",
                schema: "canal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destinatario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contenido = table.Column<string>(type: "text", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    en_respuesta_a = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    proximo_intento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enviado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    id_externo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    estado_externo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ultimo_error = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mensaje_saliente", x => x.id);
                    table.ForeignKey(
                        name: "fk_mensaje_saliente_mensaje_entrante_en_respuesta_a",
                        column: x => x.en_respuesta_a,
                        principalSchema: "canal",
                        principalTable: "mensaje_entrante",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversacion_telefono",
                schema: "canal",
                table: "conversacion",
                column: "telefono",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_entrante_estado_recibido_en",
                schema: "canal",
                table: "mensaje_entrante",
                columns: new[] { "estado", "recibido_en" });

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_entrante_id_externo",
                schema: "canal",
                table: "mensaje_entrante",
                column: "id_externo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_saliente_en_respuesta_a",
                schema: "canal",
                table: "mensaje_saliente",
                column: "en_respuesta_a");

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_saliente_estado_proximo_intento",
                schema: "canal",
                table: "mensaje_saliente",
                columns: new[] { "estado", "proximo_intento" });

            migrationBuilder.CreateIndex(
                name: "ix_mensaje_saliente_id_externo",
                schema: "canal",
                table: "mensaje_saliente",
                column: "id_externo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversacion",
                schema: "canal");

            migrationBuilder.DropTable(
                name: "mensaje_saliente",
                schema: "canal");

            migrationBuilder.DropTable(
                name: "mensaje_entrante",
                schema: "canal");
        }
    }
}
