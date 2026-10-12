using Microsoft.EntityFrameworkCore;
using Hoteleria_App.Models;

namespace Hoteleria_App.Data;

public class HoteleriaDbContext : DbContext
{
    public HoteleriaDbContext(DbContextOptions<HoteleriaDbContext> options) : base(options) { }

    public DbSet<Piso> Pisos => Set<Piso>();
    public DbSet<TipoVip> TiposVip => Set<TipoVip>();
    public DbSet<Habitacion> Habitaciones => Set<Habitacion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<ReservaHabitacion> ReservaHabitaciones => Set<ReservaHabitacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Piso
        modelBuilder.Entity<Piso>(entity =>
        {
            entity.ToTable("Pisos");
            entity.HasKey(e => e.NroPiso);
            entity.Property(e => e.NroPiso).HasColumnName("nro_piso");
            entity.Property(e => e.RefPiso).HasColumnName("ref_piso").HasMaxLength(10).IsRequired(false);
        });

        // TipoVip
        modelBuilder.Entity<TipoVip>(entity =>
        {
            entity.ToTable("Tipo Vip");
            entity.HasKey(e => e.TipoVipNombre);
            entity.Property(e => e.TipoVipNombre).HasColumnName("tipo_vip").HasMaxLength(15);
            entity.Property(e => e.Sigla).HasColumnName("sigla").HasColumnType("char(1)");
            entity.Property(e => e.PrecioCama).HasColumnName("precio_cama").HasColumnType("decimal(12,2)");
            entity.HasIndex(e => e.Sigla).IsUnique();
        });

        // Habitacion
        modelBuilder.Entity<Habitacion>(entity =>
        {
            entity.ToTable("Habitaciones");
            entity.HasKey(e => e.HabitacionId);
            entity.Property(e => e.HabitacionId).HasColumnName("habitacion_id");
            entity.Property(e => e.NroPiso).HasColumnName("nro_piso");
            entity.Property(e => e.NroCamas).HasColumnName("nro_camas");
            entity.Property(e => e.TipoVipNombre).HasColumnName("tipo_vip").HasMaxLength(15);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(20);

            entity.HasOne(e => e.Piso)
                  .WithMany(p => p.Habitaciones)
                  .HasForeignKey(e => e.NroPiso)
                  .HasConstraintName("fk_habitacion_piso");

            entity.HasOne(e => e.TipoVip)
                  .WithMany(t => t.Habitaciones)
                  .HasForeignKey(e => e.TipoVipNombre)
                  .HasConstraintName("fk_habitacion_tipovip");

            entity.HasCheckConstraint("ck_estado", "estado IN ('MANTENIMIENTO', 'LIBRE', 'OCUPADO')");
            entity.HasCheckConstraint("ck_nro_camas", "nro_camas >= 1");
        });

        // Usuario
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(e => e.UsuarioId);
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
            entity.Property(e => e.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(15);
            entity.Property(e => e.NumeroDocumento).HasColumnName("numero_documento").HasMaxLength(25);
            entity.Property(e => e.Nombres).HasColumnName("nombres").HasMaxLength(25);
            entity.Property(e => e.Apellidos).HasColumnName("apellidos").HasMaxLength(25);
            entity.Property(e => e.Nacionalidad).HasColumnName("nacionalidad").HasMaxLength(25);
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(25);
            entity.Property(e => e.EMail).HasColumnName("e_mail").HasMaxLength(25);
        });

        // Reserva
        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.ToTable("Reservas");
            entity.HasKey(e => e.ReservaId);
            entity.Property(e => e.ReservaId).HasColumnName("reserva_id");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
            entity.Property(e => e.EstadoReserva).HasColumnName("estado_reserva").HasMaxLength(15);
            entity.Property(e => e.FechaReservaCreacion).HasColumnName("fecha_reserva_creacion").HasColumnType("datetime2(7)");
            entity.Property(e => e.InicioReserva).HasColumnName("inicio_reserva").HasColumnType("datetime2(7)");
            entity.Property(e => e.TotalNoches).HasColumnName("total_noches");
            entity.Property(e => e.FinReserva).HasColumnName("fin_reserva").HasColumnType("datetime2(7)").HasComputedColumnSql("DATEADD(day, total_noches, inicio_reserva)", stored: true);

            entity.HasOne(e => e.Usuario)
                  .WithMany(u => u.Reservas)
                  .HasForeignKey(e => e.UsuarioId)
                  .HasConstraintName("fk_reserva_usuario");

            entity.HasCheckConstraint("check_estado_reserva", "estado_reserva IN ('CANCELADA', 'FINALIZADA', 'CONFIRMADA', 'PENDIENTE')");
            entity.HasCheckConstraint("check_fin_reserva", "fin_reserva > inicio_reserva");
            entity.HasCheckConstraint("check_total_noches", "total_noches >= 1");
        });

        // ReservaHabitacion (junction table)
        modelBuilder.Entity<ReservaHabitacion>(entity =>
        {
            entity.ToTable("Reserva Habitaciones");
            entity.HasKey(e => new { e.ReservaId, e.HabitacionId });
            entity.Property(e => e.ReservaId).HasColumnName("reserva_id");
            entity.Property(e => e.HabitacionId).HasColumnName("habitacion_id");
            entity.Property(e => e.PrecioNochePactado).HasColumnName("precio_noche_pactado").HasColumnType("decimal(12,2)");

            entity.HasOne(e => e.Reserva)
                  .WithMany(r => r.ReservaHabitaciones)
                  .HasForeignKey(e => e.ReservaId)
                  .HasConstraintName("fk_reserva_rh");

            entity.HasOne(e => e.Habitacion)
                  .WithMany(h => h.ReservaHabitaciones)
                  .HasForeignKey(e => e.HabitacionId)
                  .HasConstraintName("fk_habitacion_rh");
        });
    }
}