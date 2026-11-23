using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PulpeStock.Web.Datos;
using PulpeStock.Web.Models;
using PulpeStock.Web.Reportes;

namespace PulpeStock.Web.Controllers;

public sealed class ReposicionController(InventarioDbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await ConsultaReposicion.Obtener(db, cancellationToken));
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
