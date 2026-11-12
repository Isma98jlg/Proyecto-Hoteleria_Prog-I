using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hoteleria_App.Migrations
{
    /// <inheritdoc />
    public partial class BusquedasRecomendacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Busquedas",
                columns: table => new
                {
                    busqueda_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    creada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    inicio = table.Column<DateTime>(type: "date", nullable: false),
                    noches = table.Column<int>(type: "integer", nullable: false),
                    personas = table.Column<int>(type: "integer", nullable: true),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    con_texto = table.Column<bool>(type: "boolean", nullable: false),
                    reservada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Busquedas", x => x.busqueda_id);
                    table.CheckConstraint("ck_busqueda_noches", "noches BETWEEN 1 AND 365");
                    table.CheckConstraint("ck_busqueda_personas", "personas IS NULL OR personas >= 1");
                    table.ForeignKey(
                        name: "fk_busqueda_usuario",
                        column: x => x.usuario_id,
                        principalTable: "Usuarios",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Busqueda Resultados",
                columns: table => new
                {
                    busqueda_id = table.Column<long>(type: "bigint", nullable: false),
                    habitacion_id = table.Column<long>(type: "bigint", nullable: false),
                    posicion = table.Column<int>(type: "integer", nullable: false),
                    elegida = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Busqueda Resultados", x => new { x.busqueda_id, x.habitacion_id });
                    table.CheckConstraint("ck_resultado_posicion", "posicion >= 1");
                    table.ForeignKey(
                        name: "fk_resultado_busqueda",
                        column: x => x.busqueda_id,
                        principalTable: "Busquedas",
                        principalColumn: "busqueda_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_resultado_habitacion",
                        column: x => x.habitacion_id,
                        principalTable: "Habitaciones",
                        principalColumn: "habitacion_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Busqueda Resultados_habitacion_id",
                table: "Busqueda Resultados",
                column: "habitacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_Busquedas_Fecha",
                table: "Busquedas",
                column: "creada_en");

            migrationBuilder.CreateIndex(
                name: "IX_Busquedas_Usuario",
                table: "Busquedas",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Busqueda Resultados");

            migrationBuilder.DropTable(
                name: "Busquedas");
        }
    }
}
