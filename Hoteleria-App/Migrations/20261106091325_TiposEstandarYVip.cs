using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hoteleria_App.Migrations
{
    /// <inheritdoc />
    public partial class TiposEstandarYVip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Unifica las categorías anteriores en VIP. Las reservas conservan su precio pactado.
            // En una base nueva no hay nada que convertir: los tipos los crea el sembrado.
            migrationBuilder.Sql("""
                DO $tipos$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Tipo Vip" WHERE tipo_vip IN ('SUPERIOR', 'SUITE', 'PRESIDENCIAL')) THEN
                        INSERT INTO "Tipo Vip" (tipo_vip, sigla, precio_cama) VALUES ('VIP', 'V', 150.00)
                        ON CONFLICT (tipo_vip) DO NOTHING;
                        UPDATE "Habitaciones" SET tipo_vip = 'VIP' WHERE tipo_vip IN ('SUPERIOR', 'SUITE', 'PRESIDENCIAL');
                        DELETE FROM "Tipo Vip" WHERE tipo_vip IN ('SUPERIOR', 'SUITE', 'PRESIDENCIAL');
                    END IF;
                END
                $tipos$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No es reversible: no se guarda a qué categoría anterior pertenecía cada habitación.
        }
    }
}
