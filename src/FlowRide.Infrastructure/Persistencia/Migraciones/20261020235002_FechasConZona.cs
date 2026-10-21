using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowRide.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class FechasConZona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "fin_local",
                schema: "ops",
                table: "turno",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "inicio_local",
                schema: "ops",
                table: "turno",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "zona_horaria",
                schema: "ops",
                table: "turno",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "hora_local_servicio",
                schema: "ops",
                table: "solicitud_viaje",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "zona_horaria",
                schema: "ops",
                table: "solicitud_viaje",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "hora_estimada_local",
                schema: "ops",
                table: "parada",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "zona_horaria",
                schema: "ops",
                table: "parada",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "zona_horaria",
                schema: "ops",
                table: "itinerario",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Datos existentes: se guardaron como instante UTC. Pasan a hora local de la zona por defecto del producto
            // (America/Bogota, la de FlowRide:ZonaHoraria); desde aquí la hora local y la zona son la fuente de verdad.
            migrationBuilder.Sql("""
                UPDATE ops.solicitud_viaje SET zona_horaria = 'America/Bogota',
                    hora_local_servicio = fecha_hora_servicio AT TIME ZONE 'America/Bogota';
                UPDATE ops.turno SET zona_horaria = 'America/Bogota',
                    inicio_local = inicio AT TIME ZONE 'America/Bogota', fin_local = fin AT TIME ZONE 'America/Bogota';
                UPDATE ops.parada SET zona_horaria = 'America/Bogota',
                    hora_estimada_local = hora_estimada AT TIME ZONE 'America/Bogota';
                UPDATE ops.itinerario SET zona_horaria = 'America/Bogota';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fin_local",
                schema: "ops",
                table: "turno");

            migrationBuilder.DropColumn(
                name: "inicio_local",
                schema: "ops",
                table: "turno");

            migrationBuilder.DropColumn(
                name: "zona_horaria",
                schema: "ops",
                table: "turno");

            migrationBuilder.DropColumn(
                name: "hora_local_servicio",
                schema: "ops",
                table: "solicitud_viaje");

            migrationBuilder.DropColumn(
                name: "zona_horaria",
                schema: "ops",
                table: "solicitud_viaje");

            migrationBuilder.DropColumn(
                name: "hora_estimada_local",
                schema: "ops",
                table: "parada");

            migrationBuilder.DropColumn(
                name: "zona_horaria",
                schema: "ops",
                table: "parada");

            migrationBuilder.DropColumn(
                name: "zona_horaria",
                schema: "ops",
                table: "itinerario");
        }
    }
}
