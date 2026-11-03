using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hoteleria_App.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pisos",
                columns: table => new
                {
                    nro_piso = table.Column<long>(type: "bigint", nullable: false),
                    ref_piso = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pisos", x => x.nro_piso);
                });

            migrationBuilder.CreateTable(
                name: "Tipo Vip",
                columns: table => new
                {
                    tipo_vip = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    sigla = table.Column<string>(type: "char(1)", nullable: false),
                    precio_cama = table.Column<decimal>(type: "decimal(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tipo Vip", x => x.tipo_vip);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo_documento = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    numero_documento = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    nombres = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    apellidos = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    nacionalidad = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    telefono = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    e_mail = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.usuario_id);
                });

            migrationBuilder.CreateTable(
                name: "Habitaciones",
                columns: table => new
                {
                    habitacion_id = table.Column<long>(type: "bigint", nullable: false),
                    nro_piso = table.Column<long>(type: "bigint", nullable: false),
                    nro_camas = table.Column<int>(type: "int", nullable: false),
                    tipo_vip = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Habitaciones", x => x.habitacion_id);
                    table.CheckConstraint("ck_estado", "estado IN ('MANTENIMIENTO', 'LIBRE', 'OCUPADO')");
                    table.CheckConstraint("ck_nro_camas", "nro_camas >= 1");
                    table.ForeignKey(
                        name: "fk_habitacion_piso",
                        column: x => x.nro_piso,
                        principalTable: "Pisos",
                        principalColumn: "nro_piso",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_habitacion_tipovip",
                        column: x => x.tipo_vip,
                        principalTable: "Tipo Vip",
                        principalColumn: "tipo_vip",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reservas",
                columns: table => new
                {
                    reserva_id = table.Column<long>(type: "bigint", nullable: false),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    estado_reserva = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    fecha_reserva_creacion = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    inicio_reserva = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    total_noches = table.Column<int>(type: "int", nullable: false),
                    fin_reserva = table.Column<DateTime>(type: "datetime2(7)", nullable: false, computedColumnSql: "DATEADD(day, total_noches, inicio_reserva)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservas", x => x.reserva_id);
                    table.CheckConstraint("check_estado_reserva", "estado_reserva IN ('CANCELADA', 'FINALIZADA', 'CONFIRMADA', 'PENDIENTE')");
                    table.CheckConstraint("check_fin_reserva", "fin_reserva > inicio_reserva");
                    table.CheckConstraint("check_total_noches", "total_noches >= 1");
                    table.ForeignKey(
                        name: "fk_reserva_usuario",
                        column: x => x.usuario_id,
                        principalTable: "Usuarios",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reserva Habitaciones",
                columns: table => new
                {
                    reserva_id = table.Column<long>(type: "bigint", nullable: false),
                    habitacion_id = table.Column<long>(type: "bigint", nullable: false),
                    precio_noche_pactado = table.Column<decimal>(type: "decimal(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reserva Habitaciones", x => new { x.reserva_id, x.habitacion_id });
                    table.ForeignKey(
                        name: "fk_habitacion_rh",
                        column: x => x.habitacion_id,
                        principalTable: "Habitaciones",
                        principalColumn: "habitacion_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reserva_rh",
                        column: x => x.reserva_id,
                        principalTable: "Reservas",
                        principalColumn: "reserva_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Habitaciones_Estado",
                table: "Habitaciones",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "IX_Habitaciones_Piso",
                table: "Habitaciones",
                column: "nro_piso");

            migrationBuilder.CreateIndex(
                name: "IX_Habitaciones_TipoVip",
                table: "Habitaciones",
                column: "tipo_vip");

            migrationBuilder.CreateIndex(
                name: "IX_Reserva Habitaciones_habitacion_id",
                table: "Reserva Habitaciones",
                column: "habitacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Estado",
                table: "Reservas",
                column: "estado_reserva");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Fechas",
                table: "Reservas",
                columns: new[] { "inicio_reserva", "fin_reserva" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Usuario",
                table: "Reservas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_Tipo Vip_sigla",
                table: "Tipo Vip",
                column: "sigla",
                unique: true);

            // Vista de consulta (no la genera EF Core; la creamos igual que init-docker.sql)
            migrationBuilder.Sql(
                @"EXEC(N'CREATE VIEW [dbo].[TotalPrice_Habitacion_plus_Codigo] AS
SELECT h.[habitacion_id], (CONVERT(VARCHAR(4), h.[nro_piso]) + tv.[sigla]) AS [codigo], h.[nro_piso], h.[nro_camas], h.[tipo_vip], h.[estado], h.[nro_camas] * tv.[precio_cama] AS [precio_total]
FROM [dbo].[Habitaciones] h
JOIN [dbo].[Tipo Vip] tv ON h.[tipo_vip] = tv.[tipo_vip]');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[TotalPrice_Habitacion_plus_Codigo];");

            migrationBuilder.DropTable(
                name: "Reserva Habitaciones");

            migrationBuilder.DropTable(
                name: "Habitaciones");

            migrationBuilder.DropTable(
                name: "Reservas");

            migrationBuilder.DropTable(
                name: "Pisos");

            migrationBuilder.DropTable(
                name: "Tipo Vip");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
