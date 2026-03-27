using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RedGenealogica.Web.Enumeraciones;
using RedGenealogica.Web.Models;

namespace RedGenealogica.Web.Data;

public class ContextoAplicacion : IdentityDbContext<Usuario, IdentityRole<int>, int>
{
    public ContextoAplicacion(DbContextOptions<ContextoAplicacion> options)
        : base(options)
    {
    }

    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Referido> Referidos => Set<Referido>();
    public DbSet<MovimientoPuntos> MovimientosPuntos => Set<MovimientoPuntos>();
    public DbSet<RangoUsuario> RangosUsuario => Set<RangoUsuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");

            entity.Property(x => x.Nombres)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Apellidos)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.CodigoReferido)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.DocumentoIdentidad)
                .HasMaxLength(50);

            entity.Property(x => x.FotoPerfilUrl)
                .HasMaxLength(250);

            entity.HasIndex(x => x.CodigoReferido).IsUnique();
            entity.HasIndex(x => x.DocumentoIdentidad).IsUnique(false);

            entity.HasOne(x => x.UsuarioPadre)
                .WithMany(x => x.ReferidosDirectos)
                .HasForeignKey(x => x.IdUsuarioPadre)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");

            entity.Property(x => x.Nombre)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(x => x.Descripcion)
                .HasMaxLength(500);

            entity.Property(x => x.Precio)
                .HasPrecision(18, 2);
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.ToTable("Pagos");

            entity.Property(x => x.Monto)
                .HasPrecision(18, 2);

            entity.Property(x => x.PlataformaPago)
                .HasMaxLength(100);

            entity.Property(x => x.ReferenciaExterna)
                .HasMaxLength(150);

            entity.Property(x => x.NombreCuentaEnmascarado)
                .HasMaxLength(150);

            entity.HasOne(x => x.Usuario)
                .WithMany(x => x.Pagos)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Producto)
                .WithMany(x => x.Pagos)
                .HasForeignKey(x => x.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Referido>(entity =>
        {
            entity.ToTable("Referidos");

            entity.Property(x => x.NombreCompleto)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.CorreoElectronico)
                .HasMaxLength(150);

            entity.Property(x => x.Telefono)
                .HasMaxLength(30);

            entity.HasOne(x => x.Usuario)
                .WithMany(x => x.ReferidosRegistrados)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UsuarioConvertido)
            .WithMany()
            .HasForeignKey(x => x.UsuarioConvertidoId)
            .OnDelete(DeleteBehavior.SetNull);
            

            entity.HasOne(x => x.Producto)
                .WithMany(x => x.Referidos)
                .HasForeignKey(x => x.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PagoUsuario)
                .WithMany()
                .HasForeignKey(x => x.PagoUsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.PagoReferido)
                .WithMany()
                .HasForeignKey(x => x.PagoReferidoId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MovimientoPuntos>(entity =>
        {
            entity.ToTable("MovimientosPuntos");

            entity.Property(x => x.Motivo)
                .HasMaxLength(150)
                .IsRequired();

            entity.HasOne(x => x.Usuario)
                .WithMany(x => x.MovimientosPuntos)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Referido)
                .WithMany()
                .HasForeignKey(x => x.ReferidoId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RangoUsuario>(entity =>
        {
            entity.ToTable("RangosUsuario");

            entity.Property(x => x.NombreVisible)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.ColorPrincipal)
                .HasMaxLength(30);

            entity.Property(x => x.IconoCss)
                .HasMaxLength(80);
        });

        modelBuilder.Entity<RangoUsuario>().HasData(
            new RangoUsuario
            {
                Id = 1,
                TipoRango = TipoRango.Cobre,
                NombreVisible = "Cobre",
                PuntosMinimos = 0,
                PuntosMaximos = 99,
                Orden = 1,
                ColorPrincipal = "#8B5A2B",
                IconoCss = "bi-shield",
                Activo = true
            },
            new RangoUsuario
            {
                Id = 2,
                TipoRango = TipoRango.Bronce,
                NombreVisible = "Bronce",
                PuntosMinimos = 100,
                PuntosMaximos = 299,
                Orden = 2,
                ColorPrincipal = "#B08D57",
                IconoCss = "bi-shield-check",
                Activo = true
            },
            new RangoUsuario
            {
                Id = 3,
                TipoRango = TipoRango.Plata,
                NombreVisible = "Plata",
                PuntosMinimos = 300,
                PuntosMaximos = 599,
                Orden = 3,
                ColorPrincipal = "#BFC1C2",
                IconoCss = "bi-award",
                Activo = true
            },
            new RangoUsuario
            {
                Id = 4,
                TipoRango = TipoRango.Oro,
                NombreVisible = "Oro",
                PuntosMinimos = 600,
                PuntosMaximos = 999,
                Orden = 4,
                ColorPrincipal = "#D4AF37",
                IconoCss = "bi-trophy",
                Activo = true
            },
            new RangoUsuario
            {
                Id = 5,
                TipoRango = TipoRango.Platino,
                NombreVisible = "Platino",
                PuntosMinimos = 1000,
                PuntosMaximos = 1499,
                Orden = 5,
                ColorPrincipal = "#62D0FF",
                IconoCss = "bi-gem",
                Activo = true
            },
            new RangoUsuario
            {
                Id = 6,
                TipoRango = TipoRango.Diamante,
                NombreVisible = "Diamante",
                PuntosMinimos = 1500,
                PuntosMaximos = int.MaxValue,
                Orden = 6,
                ColorPrincipal = "#00E5FF",
                IconoCss = "bi-gem",
                Activo = true
            }
        );
    }
}