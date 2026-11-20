using Microsoft.EntityFrameworkCore;
using PulpeStock.Web.Inventario;

namespace PulpeStock.Web.Datos;

public sealed class InventarioDbContext(DbContextOptions<InventarioDbContext> options)
    : DbContext(options)
{
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ConteoInventario> Conteos => Set<ConteoInventario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var producto = modelBuilder.Entity<Producto>();
        producto.Property(p => p.Nombre).HasMaxLength(100);
        producto.Property(p => p.Marca).HasMaxLength(80);
        producto.Property(p => p.Presentacion).HasMaxLength(80);
        producto.Property(p => p.CodigoBarras).HasMaxLength(40);
        producto.HasIndex(p => p.CodigoBarras).IsUnique();
        producto.HasMany(p => p.Conteos).WithOne().HasForeignKey(c => c.ProductoId);

        var conteo = modelBuilder.Entity<ConteoInventario>();
        conteo.HasIndex(c => new { c.ProductoId, c.FechaUtc, c.Id });
        conteo.OwnsMany(c => c.Paquetes, paquetes =>
        {
            paquetes.ToTable("PaquetesContados");
            paquetes.Property<int>("Id");
            paquetes.HasKey("Id");
        });
    }
}
