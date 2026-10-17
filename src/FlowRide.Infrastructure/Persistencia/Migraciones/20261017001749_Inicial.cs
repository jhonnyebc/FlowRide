using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowRide.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "empresa_cliente",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correo_contacto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empresa_cliente", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rol",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rol", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "itinerario",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_servicio = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    origen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destino = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hora_salida = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_llegada = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itinerario", x => x.id);
                    table.ForeignKey(
                        name: "fk_itinerario_empresa_cliente_empresa_cliente_id",
                        column: x => x.empresa_cliente_id,
                        principalSchema: "ops",
                        principalTable: "empresa_cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    correo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    telefono_whats_app = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identidad_externa_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuario_rol_rol_id",
                        column: x => x.rol_id,
                        principalSchema: "ops",
                        principalTable: "rol",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conductor",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_licencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    categoria_licencia = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    vencimiento_licencia = table.Column<DateOnly>(type: "date", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_ultima_asignacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conductor", x => x.id);
                    table.ForeignKey(
                        name: "fk_conductor_usuario_id",
                        column: x => x.id,
                        principalSchema: "ops",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pasajero",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_interno = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    empresa_cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pasajero", x => x.id);
                    table.ForeignKey(
                        name: "fk_pasajero_empresa_cliente_empresa_cliente_id",
                        column: x => x.empresa_cliente_id,
                        principalSchema: "ops",
                        principalTable: "empresa_cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pasajero_usuario_id",
                        column: x => x.id,
                        principalSchema: "ops",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documento_conductor",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conductor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referencia_blob = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documento_conductor", x => x.id);
                    table.ForeignKey(
                        name: "fk_documento_conductor_conductor_conductor_id",
                        column: x => x.conductor_id,
                        principalSchema: "ops",
                        principalTable: "conductor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "turno",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conductor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_abandono = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turno", x => x.id);
                    table.CheckConstraint("ck_turno_franja", "fin > inicio");
                    table.ForeignKey(
                        name: "fk_turno_conductor_conductor_id",
                        column: x => x.conductor_id,
                        principalSchema: "ops",
                        principalTable: "conductor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehiculo",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    placa = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    modelo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    capacidad = table.Column<int>(type: "integer", nullable: false),
                    vencimiento_rtm = table.Column<DateOnly>(type: "date", nullable: false),
                    vencimiento_soat = table.Column<DateOnly>(type: "date", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    conductor_habitual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehiculo", x => x.id);
                    table.CheckConstraint("ck_vehiculo_capacidad", "capacidad > 0");
                    table.ForeignKey(
                        name: "fk_vehiculo_conductor_conductor_habitual_id",
                        column: x => x.conductor_habitual_id,
                        principalSchema: "ops",
                        principalTable: "conductor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "solicitud_viaje",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pasajero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    itinerario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    direccion_origen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direccion_destino = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fecha_hora_servicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duracion_estimada = table.Column<TimeSpan>(type: "interval", nullable: false),
                    cantidad_pasajeros = table.Column<int>(type: "integer", nullable: false),
                    urgente = table.Column<bool>(type: "boolean", nullable: false),
                    es_personal = table.Column<bool>(type: "boolean", nullable: false),
                    canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registrada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitud_viaje", x => x.id);
                    table.CheckConstraint("ck_solicitud_viaje_pasajeros", "cantidad_pasajeros > 0");
                    table.ForeignKey(
                        name: "fk_solicitud_viaje_empresa_cliente_empresa_cliente_id",
                        column: x => x.empresa_cliente_id,
                        principalSchema: "ops",
                        principalTable: "empresa_cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_solicitud_viaje_itinerario_itinerario_id",
                        column: x => x.itinerario_id,
                        principalSchema: "ops",
                        principalTable: "itinerario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_solicitud_viaje_pasajero_pasajero_id",
                        column: x => x.pasajero_id,
                        principalSchema: "ops",
                        principalTable: "pasajero",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asignacion",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitud_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conductor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    asignada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asignacion_reemplazada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_cierre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    franja_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    franja_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asignacion", x => x.id);
                    table.CheckConstraint("ck_asignacion_franja", "franja_fin > franja_inicio");
                    table.ForeignKey(
                        name: "fk_asignacion_asignacion_asignacion_reemplazada_id",
                        column: x => x.asignacion_reemplazada_id,
                        principalSchema: "ops",
                        principalTable: "asignacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asignacion_conductor_conductor_id",
                        column: x => x.conductor_id,
                        principalSchema: "ops",
                        principalTable: "conductor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asignacion_solicitud_viaje_solicitud_id",
                        column: x => x.solicitud_id,
                        principalSchema: "ops",
                        principalTable: "solicitud_viaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asignacion_vehiculo_vehiculo_id",
                        column: x => x.vehiculo_id,
                        principalSchema: "ops",
                        principalTable: "vehiculo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cancelacion_solicitud",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitud_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: false),
                    fecha_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelada_por_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cancelacion_solicitud", x => x.id);
                    table.ForeignKey(
                        name: "fk_cancelacion_solicitud_solicitud_viaje_solicitud_id",
                        column: x => x.solicitud_id,
                        principalSchema: "ops",
                        principalTable: "solicitud_viaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cancelacion_solicitud_usuario_cancelada_por_id",
                        column: x => x.cancelada_por_id,
                        principalSchema: "ops",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estado_solicitud_historial",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitud_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_anterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    estado_nuevo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estado_solicitud_historial", x => x.id);
                    table.ForeignKey(
                        name: "fk_estado_solicitud_historial_solicitud_viaje_solicitud_id",
                        column: x => x.solicitud_id,
                        principalSchema: "ops",
                        principalTable: "solicitud_viaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_estado_solicitud_historial_usuario_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "ops",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "viaje",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asignacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    eta_programada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_hora_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_hora_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_viaje", x => x.id);
                    table.ForeignKey(
                        name: "fk_viaje_asignacion_asignacion_id",
                        column: x => x.asignacion_id,
                        principalSchema: "ops",
                        principalTable: "asignacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "calificacion",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    viaje_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pasajero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    puntaje = table.Column<int>(type: "integer", nullable: false),
                    comentario = table.Column<string>(type: "text", nullable: true),
                    fecha_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calificacion", x => x.id);
                    table.CheckConstraint("ck_calificacion_puntaje", "puntaje BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_calificacion_pasajero_pasajero_id",
                        column: x => x.pasajero_id,
                        principalSchema: "ops",
                        principalTable: "pasajero",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_calificacion_viaje_viaje_id",
                        column: x => x.viaje_id,
                        principalSchema: "ops",
                        principalTable: "viaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incidente",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    viaje_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    fecha_hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reportado_por_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incidente", x => x.id);
                    table.ForeignKey(
                        name: "fk_incidente_usuario_reportado_por_id",
                        column: x => x.reportado_por_id,
                        principalSchema: "ops",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incidente_viaje_viaje_id",
                        column: x => x.viaje_id,
                        principalSchema: "ops",
                        principalTable: "viaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parada",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    viaje_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hora_estimada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hora_real = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parada", x => x.id);
                    table.ForeignKey(
                        name: "fk_parada_viaje_viaje_id",
                        column: x => x.viaje_id,
                        principalSchema: "ops",
                        principalTable: "viaje",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_asignacion_reemplazada_id",
                schema: "ops",
                table: "asignacion",
                column: "asignacion_reemplazada_id");

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_conductor_id",
                schema: "ops",
                table: "asignacion",
                column: "conductor_id");

            migrationBuilder.CreateIndex(
                name: "ix_asignacion_vehiculo_id",
                schema: "ops",
                table: "asignacion",
                column: "vehiculo_id");

            migrationBuilder.CreateIndex(
                name: "ux_asignacion_solicitud_vigente",
                schema: "ops",
                table: "asignacion",
                column: "solicitud_id",
                unique: true,
                filter: "estado IN ('Asignada', 'Aceptada', 'EnCurso')");

            migrationBuilder.CreateIndex(
                name: "ix_calificacion_pasajero_id",
                schema: "ops",
                table: "calificacion",
                column: "pasajero_id");

            migrationBuilder.CreateIndex(
                name: "ix_calificacion_viaje_id_pasajero_id",
                schema: "ops",
                table: "calificacion",
                columns: new[] { "viaje_id", "pasajero_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cancelacion_solicitud_cancelada_por_id",
                schema: "ops",
                table: "cancelacion_solicitud",
                column: "cancelada_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_cancelacion_solicitud_solicitud_id",
                schema: "ops",
                table: "cancelacion_solicitud",
                column: "solicitud_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conductor_numero_licencia",
                schema: "ops",
                table: "conductor",
                column: "numero_licencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documento_conductor_conductor_id",
                schema: "ops",
                table: "documento_conductor",
                column: "conductor_id");

            migrationBuilder.CreateIndex(
                name: "ix_empresa_cliente_nit",
                schema: "ops",
                table: "empresa_cliente",
                column: "nit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_estado_solicitud_historial_actor_id",
                schema: "ops",
                table: "estado_solicitud_historial",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_estado_solicitud_historial_solicitud_id",
                schema: "ops",
                table: "estado_solicitud_historial",
                column: "solicitud_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidente_reportado_por_id",
                schema: "ops",
                table: "incidente",
                column: "reportado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidente_viaje_id",
                schema: "ops",
                table: "incidente",
                column: "viaje_id");

            migrationBuilder.CreateIndex(
                name: "ix_itinerario_empresa_cliente_id_codigo_servicio_fecha",
                schema: "ops",
                table: "itinerario",
                columns: new[] { "empresa_cliente_id", "codigo_servicio", "fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parada_viaje_id_orden",
                schema: "ops",
                table: "parada",
                columns: new[] { "viaje_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pasajero_empresa_cliente_id_codigo_interno",
                schema: "ops",
                table: "pasajero",
                columns: new[] { "empresa_cliente_id", "codigo_interno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rol_nombre",
                schema: "ops",
                table: "rol",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_viaje_empresa_cliente_id",
                schema: "ops",
                table: "solicitud_viaje",
                column: "empresa_cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_viaje_estado_fecha_hora_servicio",
                schema: "ops",
                table: "solicitud_viaje",
                columns: new[] { "estado", "fecha_hora_servicio" });

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_viaje_itinerario_id",
                schema: "ops",
                table: "solicitud_viaje",
                column: "itinerario_id");

            migrationBuilder.CreateIndex(
                name: "ix_solicitud_viaje_pasajero_id",
                schema: "ops",
                table: "solicitud_viaje",
                column: "pasajero_id");

            migrationBuilder.CreateIndex(
                name: "ix_turno_conductor_id",
                schema: "ops",
                table: "turno",
                column: "conductor_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_correo",
                schema: "ops",
                table: "usuario",
                column: "correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_identidad_externa_id",
                schema: "ops",
                table: "usuario",
                column: "identidad_externa_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_rol_id",
                schema: "ops",
                table: "usuario",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_telefono_whats_app",
                schema: "ops",
                table: "usuario",
                column: "telefono_whats_app",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehiculo_conductor_habitual_id",
                schema: "ops",
                table: "vehiculo",
                column: "conductor_habitual_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehiculo_placa",
                schema: "ops",
                table: "vehiculo",
                column: "placa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_viaje_asignacion_id",
                schema: "ops",
                table: "viaje",
                column: "asignacion_id",
                unique: true);

            // Sin franjas solapadas (ADR-0007). EF Core no modela EXCLUDE; se escriben a mano y requieren btree_gist.
            migrationBuilder.Sql("""
                ALTER TABLE ops.turno
                    ADD CONSTRAINT ex_turno_conductor_sin_solape
                    EXCLUDE USING gist (conductor_id WITH =, tstzrange(inicio, fin) WITH &&)
                    WHERE (estado IN ('Programado', 'Activo'));

                ALTER TABLE ops.asignacion
                    ADD CONSTRAINT ex_asignacion_conductor_sin_solape
                    EXCLUDE USING gist (conductor_id WITH =, tstzrange(franja_inicio, franja_fin) WITH &&)
                    WHERE (estado IN ('Asignada', 'Aceptada', 'EnCurso'));

                ALTER TABLE ops.asignacion
                    ADD CONSTRAINT ex_asignacion_vehiculo_sin_solape
                    EXCLUDE USING gist (vehiculo_id WITH =, tstzrange(franja_inicio, franja_fin) WITH &&)
                    WHERE (estado IN ('Asignada', 'Aceptada', 'EnCurso'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calificacion",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "cancelacion_solicitud",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "documento_conductor",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "estado_solicitud_historial",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "incidente",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "parada",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "turno",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "viaje",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "asignacion",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "solicitud_viaje",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "vehiculo",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "itinerario",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "pasajero",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "conductor",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "empresa_cliente",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "usuario",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "rol",
                schema: "ops");
        }
    }
}
