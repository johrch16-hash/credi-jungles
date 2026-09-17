using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.DAL.Entities;

namespace PrestamosCobros.DAL.Context;

public class AppDbContext : IdentityDbContext<Usuario, Rol, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Bitacora> Bitacoras => Set<Bitacora>();
    public DbSet<Prestamo> Prestamos => Set<Prestamo>();
    public DbSet<Cuota> Cuotas => Set<Cuota>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<Gasto> Gastos => Set<Gasto>();
    public DbSet<Preferencias> Preferencias => Set<Preferencias>();
    public DbSet<ConfiguracionWhatsApp> ConfiguracionesWhatsApp => Set<ConfiguracionWhatsApp>();
    public DbSet<MensajeRecibido> MensajesRecibidos => Set<MensajeRecibido>();
    public DbSet<SoporteTicket> SoporteTickets => Set<SoporteTicket>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Renombrar tablas de Identity
        builder.Entity<Usuario>(b =>
        {
            b.ToTable("Usuarios");
            b.Property(u => u.NombreCompleto).HasMaxLength(100).IsRequired();
            b.Property(u => u.Activo).HasDefaultValue(true);
        });

        builder.Entity<Rol>(b => b.ToTable("Roles"));

        // Cliente
        builder.Entity<Cliente>(b =>
        {
            b.HasKey(c => c.ClienteId);
            b.Property(c => c.NombreCompleto).HasMaxLength(150).IsRequired();
            b.Property(c => c.Telefono).HasMaxLength(20).IsRequired();
            b.Property(c => c.Email).HasMaxLength(150);
            b.Property(c => c.CedulaCifrada).HasMaxLength(500);
            b.Property(c => c.Direccion).HasMaxLength(300);
            b.Property(c => c.LugarTrabajo).HasMaxLength(200);
            b.Property(c => c.Estado).HasMaxLength(20).HasDefaultValue("Normal");
            b.Property(c => c.NivelAtrasos).HasDefaultValue(0);
            b.Property(c => c.Activo).HasDefaultValue(true);

            b.HasIndex(c => c.Telefono).IsUnique();
            b.HasIndex(c => c.CedulaCifrada).IsUnique();
        });

        // Bitacora
        builder.Entity<Bitacora>(b =>
        {
            b.HasKey(a => a.BitacoraId);
            b.Property(a => a.Accion).HasMaxLength(100).IsRequired();
            b.HasOne(a => a.Usuario)
                .WithMany()
                .HasForeignKey(a => a.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(a => a.Fecha);
            b.HasIndex(a => a.UsuarioId);
        });

        // Prestamo
        builder.Entity<Prestamo>(b =>
        {
            b.HasKey(p => p.PrestamoId);
            b.Property(p => p.NombreAlias).HasMaxLength(100);
            b.Property(p => p.MontoPrestado).HasColumnType("decimal(18,2)");
            b.Property(p => p.PorcentajeInteres).HasColumnType("decimal(5,2)").HasDefaultValue(20m);
            b.Property(p => p.MontoTotal).HasColumnType("decimal(18,2)");
            b.Property(p => p.SaldoPendiente).HasColumnType("decimal(18,2)");
            b.Property(p => p.NumeroSinpe).HasMaxLength(30);
            b.Property(p => p.CuentasBancarias).HasMaxLength(500);
            b.Property(p => p.Estado).HasMaxLength(20).HasDefaultValue("Activo");
            b.Property(p => p.PeriodicidadDias).HasDefaultValue(7);

            b.HasOne(p => p.Cliente)
                .WithMany(c => c.Prestamos)
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(p => p.ClienteId);
            b.HasIndex(p => p.Estado);
        });

        // Cuota
        builder.Entity<Cuota>(b =>
        {
            b.HasKey(c => c.CuotaId);
            b.Property(c => c.Monto).HasColumnType("decimal(18,2)");
            b.Property(c => c.Estado).HasMaxLength(15).HasDefaultValue("Pendiente");

            b.HasOne(c => c.Prestamo)
                .WithMany(p => p.Cuotas)
                .HasForeignKey(c => c.PrestamoId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(c => c.PrestamoId);
            b.HasIndex(c => new { c.FechaVencimiento, c.Estado });
        });

        // Pago
        builder.Entity<Pago>(b =>
        {
            b.HasKey(p => p.PagoId);
            b.Property(p => p.MontoPagado).HasColumnType("decimal(18,2)");
            b.Property(p => p.ComprobanteEnviado).HasDefaultValue(false);

            b.HasOne(p => p.Cuota)
                .WithOne(c => c.Pago)
                .HasForeignKey<Pago>(p => p.CuotaId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(p => p.CuotaId).IsUnique();
        });

        // Notificacion
        builder.Entity<Notificacion>(b =>
        {
            b.HasKey(n => n.NotificacionId);
            b.Property(n => n.Tipo).HasMaxLength(30).IsRequired();
            b.Property(n => n.Mensaje).HasMaxLength(1000).IsRequired();
            b.Property(n => n.Canal).HasMaxLength(20).HasDefaultValue("WhatsApp");
            b.Property(n => n.Estado).HasMaxLength(15).HasDefaultValue("Pendiente");

            b.HasOne(n => n.Prestamo)
                .WithMany()
                .HasForeignKey(n => n.PrestamoId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(n => n.Cuota)
                .WithMany()
                .HasForeignKey(n => n.CuotaId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(n => n.Cliente)
                .WithMany()
                .HasForeignKey(n => n.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(n => n.Estado);
            b.HasIndex(n => n.Tipo);
            b.HasIndex(n => n.FechaCreacion);
        });

        // Gasto
        builder.Entity<Gasto>(b =>
        {
            b.HasKey(g => g.GastoId);
            b.Property(g => g.Descripcion).HasMaxLength(200).IsRequired();
            b.Property(g => g.Monto).HasColumnType("decimal(18,2)");
            b.Property(g => g.Categoria).HasMaxLength(50).HasDefaultValue("General");

            b.HasOne(g => g.Usuario)
                .WithMany()
                .HasForeignKey(g => g.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(g => g.Fecha);
            b.HasIndex(g => g.Categoria);
        });

        // Preferencias
        builder.Entity<Preferencias>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Clave).HasMaxLength(100).IsRequired();
            b.Property(p => p.Valor).IsRequired();
            b.HasIndex(p => p.Clave).IsUnique();
        });
        // SoporteTicket
        builder.Entity<SoporteTicket>(b =>
        {
            b.HasKey(s => s.SoporteTicketId);
            b.Property(s => s.NombreUsuario).HasMaxLength(150).IsRequired();
            b.Property(s => s.EmailUsuario).HasMaxLength(150);
            b.Property(s => s.Titulo).HasMaxLength(200).IsRequired();
            b.Property(s => s.Mensaje).HasMaxLength(4000).IsRequired();
            b.Property(s => s.ImagenUrl).HasMaxLength(500);
            b.Property(s => s.Estado).HasMaxLength(30).HasDefaultValue("Pendiente");
            b.Property(s => s.RespuestaMaster).HasMaxLength(4000);

            b.HasOne(s => s.Usuario)
                .WithMany()
                .HasForeignKey(s => s.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(s => s.UsuarioId);
            b.HasIndex(s => s.Estado);
            b.HasIndex(s => s.FechaCreacion);
        });

        // Configuración global para PostgreSQL: Convertir todas las fechas a UTC
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                        v => v.ToUniversalTime(),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                        v => v.HasValue ? v.Value.ToUniversalTime() : v,
                        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v));
                }
            }
        }
    }
}
