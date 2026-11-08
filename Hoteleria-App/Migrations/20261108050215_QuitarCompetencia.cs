using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hoteleria_App.Migrations
{
    /// <inheritdoc />
    public partial class QuitarCompetencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Competidores");

            migrationBuilder.DropTable(
                name: "Consultas Competencia");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Competidores",
                columns: table => new
                {
                    competidor_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ciudad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    property_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competidores", x => x.competidor_id);
                });

            migrationBuilder.CreateTable(
                name: "Consultas Competencia",
                columns: table => new
                {
                    consulta_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    adultos = table.Column<int>(type: "integer", nullable: false),
                    ciudad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entrada = table.Column<DateTime>(type: "date", nullable: false),
                    noches = table.Column<int>(type: "integer", nullable: false),
                    realizada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resultado = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consultas Competencia", x => x.consulta_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competidores_property_token",
                table: "Competidores",
                column: "property_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consultas_Competencia_Criterios",
                table: "Consultas Competencia",
                columns: new[] { "ciudad", "entrada", "noches", "adultos", "realizada_en" });

            migrationBuilder.CreateIndex(
                name: "IX_Consultas_Competencia_Fecha",
                table: "Consultas Competencia",
                column: "realizada_en");
        }
    }
}
