using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PulpeStock.Web.Datos;
using PulpeStock.Web.Inventario;

namespace PulpeStock.Tests;

public sealed class MigracionReposicionTests
{
    [Fact]
    public async Task AgregaElMinimoSinPerderProductosYConteosExistentes()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<InventarioDbContext>().UseSqlite(conexion).Options;
        await using var db = new InventarioDbContext(opciones);
        await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().First());
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Productos (Id, Nombre, Marca, Presentacion) VALUES (1, 'Azúcar', 'Marca A', 'Bolsa de 1 libra');
            INSERT INTO Conteos (ProductoId, UnidadesSueltas, TotalUnidades, FechaUtc)
                VALUES (1, 3, 3, '2026-11-18 12:00:00');
            """);

        await db.Database.MigrateAsync();

        var producto = await db.Productos.SingleAsync();
        Assert.Null(producto.ExistenciaMinima);
        Assert.Equal(3, (await db.Conteos.SingleAsync()).TotalUnidades);
        producto.ConfigurarMinimo(3);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(3, (await db.Productos.SingleAsync()).ExistenciaMinima);
    }

    [Fact]
    public void RechazaUnMinimoNegativo()
    {
        var producto = new Producto("Azúcar", "Marca A", "Bolsa de 1 libra", null);
        Assert.Throws<ArgumentOutOfRangeException>(() => producto.ConfigurarMinimo(-1));
    }
}
