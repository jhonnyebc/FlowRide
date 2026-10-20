using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowRide.Infrastructure.Tenancy.Migraciones
{
    /// <inheritdoc />
    public partial class RutasBorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ruta_operador",
                schema: "plataforma",
                columns: table => new
                {
                    operador_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subdominio = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    url_stack = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    phone_number_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ruta_operador", x => x.operador_id);
                    table.ForeignKey(
                        name: "fk_ruta_operador_operador_operador_id",
                        column: x => x.operador_id,
                        principalSchema: "plataforma",
                        principalTable: "operador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ruta_operador_phone_number_id",
                schema: "plataforma",
                table: "ruta_operador",
                column: "phone_number_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ruta_operador_subdominio",
                schema: "plataforma",
                table: "ruta_operador",
                column: "subdominio",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ruta_operador",
                schema: "plataforma");
        }
    }
}
