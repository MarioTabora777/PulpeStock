using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PulpeStock.Web.Datos;
using PulpeStock.Web.Models;
using PulpeStock.Web.Reportes;

namespace PulpeStock.Web.Controllers;

public sealed class ReportesController(InventarioDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [Bind(nameof(ReporteInventario.ProductoId))] ReporteInventario reporte,
        CancellationToken cancellationToken, bool descargar = false)
    {
        reporte.Opciones = await ObtenerOpciones(cancellationToken);
        ValidarProducto(reporte.ProductoId, reporte.Opciones);
        if (!ModelState.IsValid)
        {
            return View(reporte);
        }

        var productos = db.Productos.AsNoTracking();
        if (reporte.ProductoId.HasValue)
        {
            productos = productos.Where(producto => producto.Id == reporte.ProductoId.Value);
        }

        reporte.Productos = await productos
            .OrderBy(producto => producto.Nombre).ThenBy(producto => producto.Marca)
            .ThenBy(producto => producto.Presentacion).ThenBy(producto => producto.Id)
            .Select(producto => new FilaProducto(
                producto.Id, producto.Nombre, producto.Marca, producto.Presentacion,
                producto.Conteos.OrderByDescending(conteo => conteo.FechaUtc).ThenByDescending(conteo => conteo.Id)
                    .Select(conteo => (int?)conteo.TotalUnidades).FirstOrDefault(),
                producto.Conteos.OrderByDescending(conteo => conteo.FechaUtc).ThenByDescending(conteo => conteo.Id)
                    .Select(conteo => (DateTime?)conteo.FechaUtc).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        if (descargar)
        {
            var filas = reporte.Productos.Select(producto => new string?[]
            {
                producto.Nombre, producto.Marca, producto.Presentacion,
                producto.UltimoTotal?.ToString(CultureInfo.InvariantCulture),
                producto.UltimaFechaUtc.HasValue ? FechaConteo.EnHoraLocal(producto.UltimaFechaUtc.Value) : null,
                producto.UltimoTotal.HasValue ? "Contado" : "Sin conteo"
            });
            return File(ArchivoCsv.Crear(
                ["Producto", "Marca", "Presentación", "Último conteo", "Fecha del conteo (Honduras)", "Estado"], filas),
                "text/csv; charset=utf-8", "inventario.csv");
        }

        return View(reporte);
    }

    [HttpGet]
    public async Task<IActionResult> Historial(
        [Bind(nameof(ReporteHistorial.ProductoId), nameof(ReporteHistorial.Desde), nameof(ReporteHistorial.Hasta))]
        ReporteHistorial reporte, CancellationToken cancellationToken, bool descargar = false)
    {
        reporte.Opciones = await ObtenerOpciones(cancellationToken);
        ValidarProducto(reporte.ProductoId, reporte.Opciones);
        if (reporte.Desde > reporte.Hasta)
        {
            ModelState.AddModelError(nameof(reporte.Hasta), "La fecha final debe ser igual o posterior a la inicial.");
        }

        if (!ModelState.IsValid)
        {
            return View(reporte);
        }

        var conteos = db.Conteos.AsNoTracking();
        if (reporte.ProductoId.HasValue)
        {
            conteos = conteos.Where(conteo => conteo.ProductoId == reporte.ProductoId.Value);
        }

        if (reporte.Desde.HasValue)
        {
            var inicioUtc = FechaConteo.InicioDelDiaUtc(reporte.Desde.Value);
            conteos = conteos.Where(conteo => conteo.FechaUtc >= inicioUtc);
        }

        if (reporte.Hasta.HasValue && reporte.Hasta.Value < DateOnly.MaxValue)
        {
            var finUtc = FechaConteo.InicioDelDiaUtc(reporte.Hasta.Value.AddDays(1));
            conteos = conteos.Where(conteo => conteo.FechaUtc < finUtc);
        }

        reporte.Conteos = await conteos
            .Join(db.Productos, conteo => conteo.ProductoId, producto => producto.Id,
                (conteo, producto) => new { Conteo = conteo, Producto = producto })
            .OrderByDescending(fila => fila.Conteo.FechaUtc).ThenByDescending(fila => fila.Conteo.Id)
            .Select(fila => new FilaHistorial(
                fila.Conteo.Id, fila.Producto.Id, fila.Producto.Nombre, fila.Producto.Marca,
                fila.Producto.Presentacion, fila.Conteo.TotalUnidades, fila.Conteo.FechaUtc))
            .ToListAsync(cancellationToken);

        if (descargar)
        {
            var filas = reporte.Conteos.Select(conteo => new string?[]
            {
                FechaConteo.EnHoraLocal(conteo.FechaUtc), conteo.Nombre, conteo.Marca, conteo.Presentacion,
                conteo.TotalUnidades.ToString(CultureInfo.InvariantCulture)
            });
            return File(ArchivoCsv.Crear(
                ["Fecha del conteo (Honduras)", "Producto", "Marca", "Presentación", "Cantidad contada"], filas),
                "text/csv; charset=utf-8", "historial-conteos.csv");
        }

        return View(reporte);
    }

    [HttpGet]
    public async Task<IActionResult> Reposicion(
        [Bind(nameof(ReporteReposicion.ProductoId))] ReporteReposicion reporte,
        CancellationToken cancellationToken, bool descargar = false)
    {
        reporte.Opciones = await ObtenerOpciones(cancellationToken);
        ValidarProducto(reporte.ProductoId, reporte.Opciones);
        if (!ModelState.IsValid)
        {
            return View(reporte);
        }

        reporte.Datos = await ConsultaReposicion.Obtener(db, cancellationToken, reporte.ProductoId);
        if (descargar)
        {
            var filas = reporte.Datos.PorReponer.Concat(reporte.Datos.PendientesDeConteo)
                .Select(producto => new string?[]
                {
                    producto.Nombre, producto.Marca, producto.Presentacion,
                    producto.UltimoTotal?.ToString(CultureInfo.InvariantCulture),
                    producto.Minimo.ToString(CultureInfo.InvariantCulture),
                    producto.UltimaFechaUtc.HasValue ? FechaConteo.EnHoraLocal(producto.UltimaFechaUtc.Value) : null,
                    producto.UltimoTotal.HasValue ? "Por reponer" : "Pendiente de contar"
                });
            return File(ArchivoCsv.Crear(
                ["Producto", "Marca", "Presentación", "Último conteo", "Mínimo", "Fecha del conteo (Honduras)", "Estado"], filas),
                "text/csv; charset=utf-8", "reposicion.csv");
        }

        return View(reporte);
    }

    private async Task<List<SelectListItem>> ObtenerOpciones(CancellationToken cancellationToken) =>
        await db.Productos.AsNoTracking()
            .OrderBy(producto => producto.Nombre).ThenBy(producto => producto.Marca)
            .ThenBy(producto => producto.Presentacion).ThenBy(producto => producto.Id)
            .Select(producto => new SelectListItem(
                producto.Nombre + " · " + producto.Marca + " · " + producto.Presentacion, producto.Id.ToString()))
            .ToListAsync(cancellationToken);

    private void ValidarProducto(int? productoId, IReadOnlyList<SelectListItem> opciones)
    {
        if (productoId.HasValue && !opciones.Any(opcion => opcion.Value == productoId.Value.ToString()))
        {
            ModelState.TryAddModelError("ProductoId", "Seleccioná un producto registrado.");
        }
    }
}
