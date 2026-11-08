using Hoteleria_App.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hoteleria_App.Data;

public class HoteleriaDbContext : IdentityDbContext<HotelUser, HotelRole, long>, IDataProtectionKeyContext
{
    public HoteleriaDbContext(DbContextOptions<HoteleriaDbContext> options) : base(options) { }

    public DbSet<Piso> Pisos => Set<Piso>();
    public DbSet<TipoVip> TiposVip => Set<TipoVip>();
    public DbSet<Habitacion> Habitaciones => Set<Habitacion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<ReservaHabitacion> ReservaHabitaciones => Set<ReservaHabitacion>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Invitacion> Invitaciones => Set<Invitacion>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.Entity<Piso>(entity =>
        {
            entity.ToTable("Pisos");
            entity.HasKey(e => e.NroPiso);
            entity.Property(e => e.NroPiso).HasColumnName("nro_piso").ValueGeneratedNever();
            entity.Property(e => e.RefPiso).HasColumnName("ref_piso").HasMaxLength(20).IsRequired(false);
        });

        modelBuilder.Entity<TipoVip>(entity =>
        {
            entity.ToTable("Tipo Vip");
            entity.HasKey(e => e.TipoVipNombre);
            entity.Property(e => e.TipoVipNombre).HasColumnName("tipo_vip").HasMaxLength(15);
            entity.Property(e => e.Sigla).HasColumnName("sigla").HasColumnType("char(1)");
            entity.Property(e => e.PrecioCama).HasColumnName("precio_cama").HasColumnType("numeric(12,2)");
            entity.HasIndex(e => e.Sigla).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("ck_precio_cama", "precio_cama >= 0"));
        });

        modelBuilder.Entity<Habitacion>(entity =>
        {
            entity.ToTable("Habitaciones", t =>
            {
                t.HasCheckConstraint("ck_estado", "estado IN ('MANTENIMIENTO', 'LIBRE', 'OCUPADO')");
                t.HasCheckConstraint("ck_nro_camas", "nro_camas >= 1");
            });
            entity.HasKey(e => e.HabitacionId);
            // El identificador es el número real de la habitación y lo indica el personal.
            entity.Property(e => e.HabitacionId).HasColumnName("habitacion_id").ValueGeneratedNever();
            entity.Property(e => e.NroPiso).HasColumnName("nro_piso");
            entity.Property(e => e.NroCamas).HasColumnName("nro_camas");
            entity.Property(e => e.TipoVipNombre).HasColumnName("tipo_vip").HasMaxLength(15);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(20);

            entity.HasOne(e => e.Piso)
                  .WithMany(p => p.Habitaciones)
                  .HasForeignKey(e => e.NroPiso)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_habitacion_piso");

            entity.HasOne(e => e.TipoVip)
                  .WithMany(t => t.Habitaciones)
                  .HasForeignKey(e => e.TipoVipNombre)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_habitacion_tipovip");

            entity.HasIndex(e => e.Estado).HasDatabaseName("IX_Habitaciones_Estado");
            entity.HasIndex(e => e.NroPiso).HasDatabaseName("IX_Habitaciones_Piso");
            entity.HasIndex(e => e.TipoVipNombre).HasDatabaseName("IX_Habitaciones_TipoVip");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(e => e.UsuarioId);
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id").UseIdentityByDefaultColumn();
            entity.Property(e => e.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(15);
            entity.Property(e => e.NumeroDocumento).HasColumnName("numero_documento").HasMaxLength(25);
            entity.Property(e => e.Nombres).HasColumnName("nombres").HasMaxLength(100);
            entity.Property(e => e.Apellidos).HasColumnName("apellidos").HasMaxLength(100);
            entity.Property(e => e.Nacionalidad).HasColumnName("nacionalidad").HasMaxLength(60);
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(25);
            entity.Property(e => e.EMail).HasColumnName("e_mail").HasMaxLength(254);
            entity.Property(e => e.CorreoContacto).HasColumnName("correo_contacto").HasMaxLength(254);
            entity.Property(e => e.CargoId).HasColumnName("cargo_id");
            entity.HasOne(e => e.Cargo).WithMany().HasForeignKey(e => e.CargoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TipoDocumento, e.NumeroDocumento }).IsUnique().HasDatabaseName("UX_Usuarios_Documento");
            entity.HasIndex(e => e.EMail).IsUnique().HasDatabaseName("UX_Usuarios_Email");
        });

        // Cuentas Identity y empleados comparten la PK de Usuario.
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Empleados");
            entity.HasKey(e => e.UsuarioId);
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
            entity.Property(e => e.FechaContratacion).HasColumnName("fecha_contratacion").HasColumnType("date");
            entity.Property(e => e.Sueldo).HasColumnName("sueldo").HasColumnType("numeric(12,2)");
            entity.HasOne(e => e.Usuario).WithOne(u => u.Employee).HasForeignKey<Employee>(e => e.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HotelRole>(entity =>
        {
            entity.ToTable("Roles");
            entity.Property(r => r.Id).HasColumnName("cargo_id");
            entity.Property(r => r.Name).HasColumnName("cargo_nombre").HasMaxLength(256);
            entity.Property(r => r.NormalizedName).HasColumnName("nombre_normalizado").HasMaxLength(256);
            entity.Property(r => r.Ver).HasColumnName("ver");
            entity.Property(r => r.CrudRequerimientos).HasColumnName("crud_requerimientos");
            entity.Property(r => r.CrudPisos).HasColumnName("crud_pisos");
            entity.Property(r => r.CrudUsuarios).HasColumnName("crud_usuarios");
            entity.Property(r => r.CrudHabitaciones).HasColumnName("crud_habitaciones");
            entity.Property(r => r.CrudTipoVip).HasColumnName("crud_tipo_vip");
        });

        modelBuilder.Entity<HotelUser>(entity =>
        {
            entity.HasOne(u => u.Usuario).WithOne().HasForeignKey<HotelUser>(u => u.Id).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.ToTable("Reservas", t =>
            {
                t.HasCheckConstraint("check_estado_reserva", "estado_reserva IN ('CANCELADA', 'FINALIZADA', 'EN_CURSO', 'CONFIRMADA', 'PENDIENTE')");
                t.HasCheckConstraint("check_total_noches", "total_noches >= 1");
            });
            entity.HasKey(e => e.ReservaId);
            entity.Property(e => e.ReservaId).HasColumnName("reserva_id").UseIdentityByDefaultColumn();
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
            entity.Property(e => e.EstadoReserva).HasColumnName("estado_reserva").HasMaxLength(15);
            entity.Property(e => e.FechaReservaCreacion).HasColumnName("fecha_reserva_creacion").HasColumnType("timestamp with time zone");
            entity.Property(e => e.InicioReserva).HasColumnName("inicio_reserva").HasColumnType("date");
            entity.Property(e => e.TotalNoches).HasColumnName("total_noches");
            entity.Property(e => e.FinReserva).HasColumnName("fin_reserva").HasColumnType("date")
                  .HasComputedColumnSql("inicio_reserva + total_noches", stored: true);

            entity.HasOne(e => e.Usuario)
                  .WithMany(u => u.Reservas)
                  .HasForeignKey(e => e.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_reserva_usuario");

            entity.HasIndex(e => e.UsuarioId).HasDatabaseName("IX_Reservas_Usuario");
            entity.HasIndex(e => e.EstadoReserva).HasDatabaseName("IX_Reservas_Estado");
            entity.HasIndex(e => new { e.InicioReserva, e.FinReserva }).HasDatabaseName("IX_Reservas_Fechas");
        });

        modelBuilder.Entity<ReservaHabitacion>(entity =>
        {
            entity.ToTable("Reserva Habitaciones");
            entity.HasKey(e => new { e.ReservaId, e.HabitacionId });
            entity.Property(e => e.ReservaId).HasColumnName("reserva_id");
            entity.Property(e => e.HabitacionId).HasColumnName("habitacion_id");
            entity.Property(e => e.PrecioNochePactado).HasColumnName("precio_noche_pactado").HasColumnType("numeric(12,2)");
            entity.Property(e => e.Inicio).HasColumnName("inicio").HasColumnType("date");
            entity.Property(e => e.Fin).HasColumnName("fin").HasColumnType("date");
            entity.Property(e => e.Activa).HasColumnName("activa");

            entity.HasOne(e => e.Reserva)
                  .WithMany(r => r.ReservaHabitaciones)
                  .HasForeignKey(e => e.ReservaId)
                  .HasConstraintName("fk_reserva_rh");

            entity.HasOne(e => e.Habitacion)
                  .WithMany(h => h.ReservaHabitaciones)
                  .HasForeignKey(e => e.HabitacionId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_habitacion_rh");
        });

        modelBuilder.Entity<Invitacion>(entity =>
        {
            entity.ToTable("Invitaciones");
            entity.HasKey(e => e.InvitacionId);
            entity.Property(e => e.InvitacionId).HasColumnName("invitacion_id").UseIdentityByDefaultColumn();
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
            entity.Property(e => e.Email).HasColumnName("e_mail").HasMaxLength(254);
            entity.Property(e => e.CargoId).HasColumnName("cargo_id");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
            entity.Property(e => e.CreadaPorId).HasColumnName("creada_por_id");
            entity.Property(e => e.ExpiraEn).HasColumnName("expira_en").HasColumnType("timestamp with time zone");
            entity.Property(e => e.UsadaEn).HasColumnName("usada_en").HasColumnType("timestamp with time zone");
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasOne(e => e.Cargo).WithMany().HasForeignKey(e => e.CargoId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
