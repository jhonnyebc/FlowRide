using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlowRide.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MotorYOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "motor");

            migrationBuilder.EnsureSchema(
                name: "notif");

            migrationBuilder.AddColumn<bool>(
                name: "apto_servicio_critico",
                schema: "ops",
                table: "conductor",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "configuracion_asignacion",
                schema: "motor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creada_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracion_asignacion", x => x.id);
                    table.CheckConstraint("ck_configuracion_version", "version > 0");
                    table.ForeignKey(
                        name: "fk_configuracion_asignacion_usuario_creada_por_id",
                        column: x => x.creada_por_id,
                        principalSchema: "ops",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "outbox_mensaje",
                schema: "notif",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    ocurrido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    proximo_intento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    intentos = table.Column<short>(type: "smallint", nullable: false),
                    procesado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_mensaje", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "decision_asignacion",
                schema: "motor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitud_id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_configuracion = table.Column<int>(type: "integer", nullable: false),
                    origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    evaluada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resultado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    conductor_seleccionado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vehiculo_seleccionado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo = table.Column<string>(type: "text", nullable: false),
                    desempate = table.Column<string>(type: "jsonb", nullable: true),
                    entrada_snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decision_asignacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_decision_asignacion_configuracion_asignacion_configuracion_",
                        column: x => x.configuracion_id,
                        principalSchema: "motor",
                        principalTable: "configuracion_asignacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decision_asignacion_solicitud_viaje_solicitud_id",
                        column: x => x.solicitud_id,
                        principalSchema: "ops",
                        principalTable: "solicitud_viaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "regla_configurada",
                schema: "motor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuracion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_regla = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    habilitada = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<short>(type: "smallint", nullable: false),
                    peso = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    parametros = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regla_configurada", x => x.id);
                    table.CheckConstraint("ck_regla_orden", "orden > 0");
                    table.CheckConstraint("ck_regla_peso", "peso BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "fk_regla_configurada_configuracion_asignacion_configuracion_id",
                        column: x => x.configuracion_id,
                        principalSchema: "motor",
                        principalTable: "configuracion_asignacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluacion_candidato",
                schema: "motor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conductor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    elegible = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_descarte = table.Column<string>(type: "text", nullable: true),
                    prioridad = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    puntaje_total = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    posicion_ranking = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluacion_candidato", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluacion_candidato_decision_asignacion_decision_id",
                        column: x => x.decision_id,
                        principalSchema: "motor",
                        principalTable: "decision_asignacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resultado_regla",
                schema: "motor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_regla = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    version_regla = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    orden = table.Column<short>(type: "smallint", nullable: false),
                    aprobado = table.Column<bool>(type: "boolean", nullable: true),
                    valor = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    peso = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    valor_ponderado = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    motivo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resultado_regla", x => x.id);
                    table.ForeignKey(
                        name: "fk_resultado_regla_evaluacion_candidato_evaluacion_id",
                        column: x => x.evaluacion_id,
                        principalSchema: "motor",
                        principalTable: "evaluacion_candidato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "motor",
                table: "configuracion_asignacion",
                columns: new[] { "id", "activada_en", "creada_en", "creada_por_id", "estado", "version" },
                values: new object[] { new Guid("b1000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Activa", 1 });

            migrationBuilder.InsertData(
                schema: "motor",
                table: "regla_configurada",
                columns: new[] { "id", "codigo_regla", "configuracion_id", "habilitada", "orden", "parametros", "peso", "tipo" },
                values: new object[,]
                {
                    { new Guid("b2000000-0000-0000-0000-000000000001"), "AvailabilityRule", new Guid("b1000000-0000-0000-0000-000000000001"), true, (short)1, null, 1m, "Filtering" },
                    { new Guid("b2000000-0000-0000-0000-000000000002"), "ShiftCompatibilityRule", new Guid("b1000000-0000-0000-0000-000000000001"), true, (short)2, null, 1m, "Filtering" },
                    { new Guid("b2000000-0000-0000-0000-000000000003"), "RegulatoryRestrictionRule", new Guid("b1000000-0000-0000-0000-000000000001"), true, (short)3, null, 1m, "Filtering" },
                    { new Guid("b2000000-0000-0000-0000-000000000004"), "ServiceCapabilityRule", new Guid("b1000000-0000-0000-0000-000000000001"), true, (short)4, null, 1m, "Filtering" },
                    { new Guid("b2000000-0000-0000-0000-000000000005"), "CriticalServicePriorityRule", new Guid("b1000000-0000-0000-0000-000000000001"), true, (short)1, null, 1m, "Priority" },
                    { new Guid("b2000000-0000-0000-0000-000000000006"), "WorkloadBalanceRule", new Guid("b1000000-0000-0000-0000-000000000001"), true, (short)1, "{\"ventanaDias\":\"7\"}", 1m, "Scoring" },
                    { new Guid("b2000000-0000-0000-0000-000000000007"), "StableTieBreaker", new Guid("b1000000-0000-0000-0000-000000000001"), true, (short)1, null, 1m, "TieBreak" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuracion_asignacion_creada_por_id",
                schema: "motor",
                table: "configuracion_asignacion",
                column: "creada_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuracion_asignacion_version",
                schema: "motor",
                table: "configuracion_asignacion",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_decision_asignacion_configuracion_id",
                schema: "motor",
                table: "decision_asignacion",
                column: "configuracion_id");

            migrationBuilder.CreateIndex(
                name: "ix_decision_asignacion_solicitud_id_evaluada_en",
                schema: "motor",
                table: "decision_asignacion",
                columns: new[] { "solicitud_id", "evaluada_en" });

            migrationBuilder.CreateIndex(
                name: "ix_evaluacion_candidato_decision_id_conductor_id_vehiculo_id",
                schema: "motor",
                table: "evaluacion_candidato",
                columns: new[] { "decision_id", "conductor_id", "vehiculo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_mensaje_event_id",
                schema: "notif",
                table: "outbox_mensaje",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_mensaje_proximo_intento",
                schema: "notif",
                table: "outbox_mensaje",
                column: "proximo_intento",
                filter: "procesado_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_regla_configurada_configuracion_id_codigo_regla",
                schema: "motor",
                table: "regla_configurada",
                columns: new[] { "configuracion_id", "codigo_regla" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regla_configurada_configuracion_id_tipo_orden",
                schema: "motor",
                table: "regla_configurada",
                columns: new[] { "configuracion_id", "tipo", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resultado_regla_evaluacion_id_codigo_regla",
                schema: "motor",
                table: "resultado_regla",
                columns: new[] { "evaluacion_id", "codigo_regla" },
                unique: true);

            // ADR-0007: las exclusiones de franjas se comprueban al confirmar (DEFERRABLE INITIALLY DEFERRED). Siguen
            // impidiendo dobles asignaciones entre transacciones, y permiten reemplazar una asignación (RF-12) en la misma
            // transacción sin importar el orden de las sentencias.
            migrationBuilder.Sql("""
                ALTER TABLE ops.asignacion DROP CONSTRAINT ex_asignacion_conductor_sin_solape;
                ALTER TABLE ops.asignacion DROP CONSTRAINT ex_asignacion_vehiculo_sin_solape;

                ALTER TABLE ops.asignacion
                    ADD CONSTRAINT ex_asignacion_conductor_sin_solape
                    EXCLUDE USING gist (conductor_id WITH =, tstzrange(franja_inicio, franja_fin) WITH &&)
                    WHERE (estado IN ('Asignada', 'Aceptada', 'EnCurso'))
                    DEFERRABLE INITIALLY DEFERRED;

                ALTER TABLE ops.asignacion
                    ADD CONSTRAINT ex_asignacion_vehiculo_sin_solape
                    EXCLUDE USING gist (vehiculo_id WITH =, tstzrange(franja_inicio, franja_fin) WITH &&)
                    WHERE (estado IN ('Asignada', 'Aceptada', 'EnCurso'))
                    DEFERRABLE INITIALLY DEFERRED;

                -- RF-21: una sola configuración activa; diferible para archivar la anterior y activar otra en una transacción.
                ALTER TABLE motor.configuracion_asignacion
                    ADD CONSTRAINT ex_configuracion_una_activa
                    EXCLUDE USING btree (estado WITH =) WHERE (estado = 'Activa')
                    DEFERRABLE INITIALLY DEFERRED;

                -- ADR-0009: el decision log solo admite inserciones.
                CREATE FUNCTION motor.impedir_cambios_decision() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'El decision log del motor solo admite inserciones (ADR-0009).';
                END;
                $$;

                CREATE TRIGGER tg_decision_solo_insercion BEFORE UPDATE OR DELETE ON motor.decision_asignacion
                    FOR EACH ROW EXECUTE FUNCTION motor.impedir_cambios_decision();
                CREATE TRIGGER tg_evaluacion_solo_insercion BEFORE UPDATE OR DELETE ON motor.evaluacion_candidato
                    FOR EACH ROW EXECUTE FUNCTION motor.impedir_cambios_decision();
                CREATE TRIGGER tg_resultado_solo_insercion BEFORE UPDATE OR DELETE ON motor.resultado_regla
                    FOR EACH ROW EXECUTE FUNCTION motor.impedir_cambios_decision();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER tg_resultado_solo_insercion ON motor.resultado_regla;
                DROP TRIGGER tg_evaluacion_solo_insercion ON motor.evaluacion_candidato;
                DROP TRIGGER tg_decision_solo_insercion ON motor.decision_asignacion;
                DROP FUNCTION motor.impedir_cambios_decision();

                ALTER TABLE ops.asignacion DROP CONSTRAINT ex_asignacion_conductor_sin_solape;
                ALTER TABLE ops.asignacion DROP CONSTRAINT ex_asignacion_vehiculo_sin_solape;
                ALTER TABLE ops.asignacion
                    ADD CONSTRAINT ex_asignacion_conductor_sin_solape
                    EXCLUDE USING gist (conductor_id WITH =, tstzrange(franja_inicio, franja_fin) WITH &&)
                    WHERE (estado IN ('Asignada', 'Aceptada', 'EnCurso'));
                ALTER TABLE ops.asignacion
                    ADD CONSTRAINT ex_asignacion_vehiculo_sin_solape
                    EXCLUDE USING gist (vehiculo_id WITH =, tstzrange(franja_inicio, franja_fin) WITH &&)
                    WHERE (estado IN ('Asignada', 'Aceptada', 'EnCurso'));
                """);

            migrationBuilder.DropTable(
                name: "outbox_mensaje",
                schema: "notif");

            migrationBuilder.DropTable(
                name: "regla_configurada",
                schema: "motor");

            migrationBuilder.DropTable(
                name: "resultado_regla",
                schema: "motor");

            migrationBuilder.DropTable(
                name: "evaluacion_candidato",
                schema: "motor");

            migrationBuilder.DropTable(
                name: "decision_asignacion",
                schema: "motor");

            migrationBuilder.DropTable(
                name: "configuracion_asignacion",
                schema: "motor");

            migrationBuilder.DropColumn(
                name: "apto_servicio_critico",
                schema: "ops",
                table: "conductor");
        }
    }
}
