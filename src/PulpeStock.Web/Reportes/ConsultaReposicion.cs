using Microsoft.EntityFrameworkCore;
using PulpeStock.Web.Datos;
using PulpeStock.Web.Models;

namespace PulpeStock.Web.Reportes;

public static class ConsultaReposicion
{
    public static async Task<ListaReposicion> Obtener(
        InventarioDbContext db, CancellationToken cancellationToken, int? productoId = null)
    {
        var consulta = db.Productos.AsNoTracking()
            .Where(producto => producto.ExistenciaMinima.HasValue);
        if (productoId.HasValue)
        {
            consulta = consulta.Where(producto => producto.Id == productoId.Value);
        }

        var productos = await consulta
            .OrderBy(producto => producto.Nombre).ThenBy(producto => producto.Marca)
            .ThenBy(producto => producto.Presentacion).ThenBy(producto => producto.Id)
            .Select(producto => new FilaReposicion(
                producto.Id, producto.Nombre, producto.Marca, producto.Presentacion,
                producto.ExistenciaMinima!.Value,
                producto.Conteos.OrderByDescending(conteo => conteo.FechaUtc).ThenByDescending(conteo => conteo.Id)
                    .Select(conteo => (int?)conteo.TotalUnidades).FirstOrDefault(),
                producto.Conteos.OrderByDescending(conteo => conteo.FechaUtc).ThenByDescending(conteo => conteo.Id)
                    .Select(conteo => (DateTime?)conteo.FechaUtc).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new ListaReposicion(
            productos.Where(producto => producto.UltimoTotal <= producto.Minimo).ToList(),
            productos.Where(producto => producto.UltimoTotal is null).ToList());
    }
}
