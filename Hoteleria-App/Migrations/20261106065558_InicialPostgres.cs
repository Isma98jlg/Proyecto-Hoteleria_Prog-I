using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hoteleria_App.Migrations
{
    /// <inheritdoc />
    public partial class InicialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pisos",
                columns: table => new
                {
                    nro_piso = table.Column<long>(type: "bigint", nullable: false),
                    ref_piso = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pisos", x => x.nro_piso);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    cargo_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ver = table.Column<bool>(type: "boolean", nullable: false),
                    crud_requerimientos = table.Column<bool>(type: "boolean", nullable: false),
                    crud_pisos = table.Column<bool>(type: "boolean", nullable: false),
                    crud_usuarios = table.Column<bool>(type: "boolean", nullable: false),
                    crud_habitaciones = table.Column<bool>(type: "boolean", nullable: false),
                    crud_tipo_vip = table.Column<bool>(type: "boolean", nullable: false),
                    cargo_nombre = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    nombre_normalizado = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.cargo_id);
                });

            migrationBuilder.CreateTable(
                name: "Tipo Vip",
                columns: table => new
                {
                    tipo_vip = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    sigla = table.Column<char>(type: "char(1)", nullable: false),
                    precio_cama = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tipo Vip", x => x.tipo_vip);
                    table.CheckConstraint("ck_precio_cama", "precio_cama >= 0");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "cargo_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invitaciones",
                columns: table => new
                {
                    invitacion_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    e_mail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    cargo_id = table.Column<long>(type: "bigint", nullable: false),
                    usuario_id = table.Column<long>(type: "bigint", nullable: true),
                    creada_por_id = table.Column<long>(type: "bigint", nullable: false),
                    expira_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitaciones", x => x.invitacion_id);
                    table.ForeignKey(
                        name: "FK_Invitaciones_Roles_cargo_id",
                        column: x => x.cargo_id,
                        principalTable: "Roles",
                        principalColumn: "cargo_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    usuario_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tipo_documento = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    numero_documento = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    nombres = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    apellidos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nacionalidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    telefono = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    e_mail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    cargo_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.usuario_id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Roles_cargo_id",
                        column: x => x.cargo_id,
                        principalTable: "Roles",
                        principalColumn: "cargo_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Habitaciones",
                columns: table => new
                {
                    habitacion_id = table.Column<long>(type: "bigint", nullable: false),
                    nro_piso = table.Column<long>(type: "bigint", nullable: false),
                    nro_camas = table.Column<int>(type: "integer", nullable: false),
                    tipo_vip = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
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
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_habitacion_tipovip",
                        column: x => x.tipo_vip,
                        principalTable: "Tipo Vip",
                        principalColumn: "tipo_vip",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Usuarios_Id",
                        column: x => x.Id,
                        principalTable: "Usuarios",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Empleados",
                columns: table => new
                {
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha_contratacion = table.Column<DateTime>(type: "date", nullable: false),
                    sueldo = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empleados", x => x.usuario_id);
                    table.ForeignKey(
                        name: "FK_Empleados_Usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "Usuarios",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reservas",
                columns: table => new
                {
                    reserva_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<long>(type: "bigint", nullable: false),
                    estado_reserva = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    fecha_reserva_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    inicio_reserva = table.Column<DateTime>(type: "date", nullable: false),
                    total_noches = table.Column<int>(type: "integer", nullable: false),
                    fin_reserva = table.Column<DateTime>(type: "date", nullable: false, computedColumnSql: "inicio_reserva + total_noches", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservas", x => x.reserva_id);
                    table.CheckConstraint("check_estado_reserva", "estado_reserva IN ('CANCELADA', 'FINALIZADA', 'EN_CURSO', 'CONFIRMADA', 'PENDIENTE')");
                    table.CheckConstraint("check_total_noches", "total_noches >= 1");
                    table.ForeignKey(
                        name: "fk_reserva_usuario",
                        column: x => x.usuario_id,
                        principalTable: "Usuarios",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "cargo_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reserva Habitaciones",
                columns: table => new
                {
                    reserva_id = table.Column<long>(type: "bigint", nullable: false),
                    habitacion_id = table.Column<long>(type: "bigint", nullable: false),
                    precio_noche_pactado = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    inicio = table.Column<DateTime>(type: "date", nullable: false),
                    fin = table.Column<DateTime>(type: "date", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reserva Habitaciones", x => new { x.reserva_id, x.habitacion_id });
                    table.ForeignKey(
                        name: "fk_habitacion_rh",
                        column: x => x.habitacion_id,
                        principalTable: "Habitaciones",
                        principalColumn: "habitacion_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reserva_rh",
                        column: x => x.reserva_id,
                        principalTable: "Reservas",
                        principalColumn: "reserva_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

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
                name: "IX_Invitaciones_cargo_id",
                table: "Invitaciones",
                column: "cargo_id");

            migrationBuilder.CreateIndex(
                name: "IX_Invitaciones_token_hash",
                table: "Invitaciones",
                column: "token_hash",
                unique: true);

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
                name: "RoleNameIndex",
                table: "Roles",
                column: "nombre_normalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tipo Vip_sigla",
                table: "Tipo Vip",
                column: "sigla",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_cargo_id",
                table: "Usuarios",
                column: "cargo_id");

            migrationBuilder.CreateIndex(
                name: "UX_Usuarios_Documento",
                table: "Usuarios",
                columns: new[] { "tipo_documento", "numero_documento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Usuarios_Email",
                table: "Usuarios",
                column: "e_mail",
                unique: true);

            // Impide en la propia base de datos dos reservas activas de una habitación en fechas solapadas.
            migrationBuilder.Sql("""
                ALTER TABLE "Reserva Habitaciones"
                ADD CONSTRAINT ex_habitacion_sin_solapes
                EXCLUDE USING gist (habitacion_id WITH =, daterange(inicio, fin, '[)') WITH &&)
                WHERE (activa);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "Empleados");

            migrationBuilder.DropTable(
                name: "Invitaciones");

            migrationBuilder.DropTable(
                name: "Reserva Habitaciones");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

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

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
