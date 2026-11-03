using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlowRide.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class IdentidadYAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "evento_auditoria",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    recurso = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    recurso_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fecha_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    valores_antes = table.Column<string>(type: "jsonb", nullable: true),
                    valores_despues = table.Column<string>(type: "jsonb", nullable: true),
                    hash_previo = table.Column<string>(type: "char(64)", nullable: false),
                    hash = table.Column<string>(type: "char(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evento_auditoria", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "ops",
                table: "rol",
                columns: new[] { "id", "descripcion", "nombre" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), "Administrador del operador: gestiona usuarios, recursos y parámetros.", "Administrador" },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), "Operador logístico: supervisa solicitudes, asignaciones y viajes.", "OperadorLogistico" },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), "Pasajero: solicita y sigue sus viajes por WhatsApp.", "Pasajero" },
                    { new Guid("a1000000-0000-0000-0000-000000000004"), "Conductor: recibe y ejecuta asignaciones por WhatsApp.", "Conductor" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_evento_auditoria_hash",
                schema: "audit",
                table: "evento_auditoria",
                column: "hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evento_auditoria_recurso_recurso_id",
                schema: "audit",
                table: "evento_auditoria",
                columns: new[] { "recurso", "recurso_id" });

            // Solo INSERT (ADR-0011): la base rechaza modificar, borrar o vaciar eventos de auditoría.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit.impedir_cambios_auditoria() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'La auditoría solo admite inserciones (ADR-0011).';
                END;
                $$;

                CREATE TRIGGER tg_evento_auditoria_solo_insercion
                    BEFORE UPDATE OR DELETE ON audit.evento_auditoria
                    FOR EACH ROW EXECUTE FUNCTION audit.impedir_cambios_auditoria();

                CREATE TRIGGER tg_evento_auditoria_sin_vaciado
                    BEFORE TRUNCATE ON audit.evento_auditoria
                    FOR EACH STATEMENT EXECUTE FUNCTION audit.impedir_cambios_auditoria();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DROP TABLE no dispara los triggers de fila ni de TRUNCATE; la función se borra después.
            migrationBuilder.DropTable(
                name: "evento_auditoria",
                schema: "audit");

            migrationBuilder.Sql("DROP FUNCTION audit.impedir_cambios_auditoria();");

            migrationBuilder.DeleteData(
                schema: "ops",
                table: "rol",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "ops",
                table: "rol",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "ops",
                table: "rol",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "ops",
                table: "rol",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000004"));
        }
    }
}
