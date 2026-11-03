using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowRide.Infrastructure.Tenancy.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plataforma");

            migrationBuilder.CreateTable(
                name: "operador",
                schema: "plataforma",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correo_contacto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operador", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operador_nit",
                schema: "plataforma",
                table: "operador",
                column: "nit",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operador",
                schema: "plataforma");
        }
    }
}
