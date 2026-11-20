using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulpeStock.Web.Datos;
using PulpeStock.Web.Models;

namespace PulpeStock.Web.Controllers;

public sealed class ReposicionController(InventarioDbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var productos = await db.Productos.AsNoTracking()
            .Where(producto => producto.ExistenciaMinima.HasValue)
            .OrderBy(producto => producto.Nombre).ThenBy(producto => producto.Marca)
            .ThenBy(producto => producto.Presentacion)
            .Select(producto => new FilaReposicion(
                producto.Id, producto.Nombre, producto.Marca, producto.Presentacion,
                producto.ExistenciaMinima!.Value,
                producto.Conteos.OrderByDescending(conteo => conteo.FechaUtc).ThenByDescending(conteo => conteo.Id)
                    .Select(conteo => (int?)conteo.TotalUnidades).FirstOrDefault(),
                producto.Conteos.OrderByDescending(conteo => conteo.FechaUtc).ThenByDescending(conteo => conteo.Id)
                    .Select(conteo => (DateTime?)conteo.FechaUtc).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return View(new ListaReposicion(
            productos.Where(producto => producto.UltimoTotal <= producto.Minimo).ToList(),
            productos.Where(producto => producto.UltimoTotal is null).ToList()));
    }

    [HttpGet]
    public async Task<IActionResult> Minimo(int id, CancellationToken cancellationToken)
    {
        var producto = await db.Productos.AsNoTracking().SingleOrDefaultAsync(
            producto => producto.Id == id, cancellationToken);
        if (producto is null)
        {
            return NotFound();
        }

        return View(new ConfiguracionMinimo
        {
            Producto = producto,
            ExistenciaMinima = producto.ExistenciaMinima
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Minimo(
        int id, [Bind(nameof(ConfiguracionMinimo.ExistenciaMinima))] ConfiguracionMinimo entrada,
        CancellationToken cancellationToken)
    {
        var producto = await db.Productos.SingleOrDefaultAsync(
            producto => producto.Id == id, cancellationToken);
        if (producto is null)
        {
            return NotFound();
        }

        entrada.Producto = producto;
        if (!ModelState.IsValid)
        {
            return View(entrada);
        }

        producto.ConfigurarMinimo(entrada.ExistenciaMinima);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction("Detalle", "Productos", new { id });
    }
}
