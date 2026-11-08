using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hoteleria_App.Migrations
{
    /// <inheritdoc />
    public partial class VerificacionDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "estado_verificacion",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SIN_VERIFICAR");

            migrationBuilder.AddColumn<string>(
                name: "fuente_verificacion",
                table: "Usuarios",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pais_emisor",
                table: "Usuarios",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "verificado_en",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "estado_verificacion",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "fuente_verificacion",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "pais_emisor",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "verificado_en",
                table: "Usuarios");
        }
    }
}
