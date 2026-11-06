using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hoteleria_App.Migrations
{
    /// <inheritdoc />
    public partial class HuespedSinVerHabitaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El huésped ya no consulta el inventario: solo accede a sus reservas.
            migrationBuilder.Sql("""UPDATE "Roles" SET ver = false WHERE cargo_nombre = 'huesped';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Roles" SET ver = true WHERE cargo_nombre = 'huesped';""");
        }
    }
}
