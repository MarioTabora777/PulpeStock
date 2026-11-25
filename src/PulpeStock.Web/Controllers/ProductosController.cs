using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PulpeStock.Web.Datos;
using PulpeStock.Web.Inventario;
using PulpeStock.Web.Models;

namespace PulpeStock.Web.Controllers;

public sealed class ProductosController(InventarioDbContext db, TimeProvider reloj) : Controller
{
    public async Task<IActionResult> Index(
        [Bind(nameof(ListadoProductos.Estado))] ListadoProductos listado, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(listado);
        }

        var consulta = db.Productos.AsNoTracking();
        if (listado.Estado != EstadoProductos.Todos)
        {
            var activo = listado.Estado == EstadoProductos.Activos;
            consulta = consulta.Where(producto => producto.Activo == activo);
        }

        listado.Productos = await consulta
            .OrderBy(p => p.Nombre).ThenBy(p => p.Marca).ThenBy(p => p.Presentacion)
            .Select(p => new FilaProducto(
                p.Id, p.Nombre, p.Marca, p.Presentacion,
                p.Conteos.OrderByDescending(c => c.FechaUtc).ThenByDescending(c => c.Id)
                    .Select(c => (int?)c.TotalUnidades).FirstOrDefault(),
                p.Conteos.OrderByDescending(c => c.FechaUtc).ThenByDescending(c => c.Id)
                    .Select(c => (DateTime?)c.FechaUtc).FirstOrDefault(), p.Activo))
            .ToListAsync(cancellationToken);

        return View(listado);
    }

    [HttpGet]
    public IActionResult Nuevo() => View(new DatosProducto());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(DatosProducto entrada, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(entrada);
        }

        var producto = new Producto(entrada.Nombre, entrada.Marca, entrada.Presentacion, entrada.CodigoBarras);
        db.Productos.Add(producto);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            ModelState.AddModelError(nameof(DatosProducto.CodigoBarras), "Ese código de barras ya está registrado.");
            return View(entrada);
        }

        return RedirectToAction(nameof(Detalle), new { id = producto.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken cancellationToken)
    {
        var producto = await BuscarProducto(id, cancellationToken);
        if (producto is null)
        {
            return NotFound();
        }

        return View(new DatosProducto
        {
            Nombre = producto.Nombre,
            Marca = producto.Marca,
            Presentacion = producto.Presentacion,
            CodigoBarras = producto.CodigoBarras
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, DatosProducto entrada, CancellationToken cancellationToken)
    {
        var producto = await db.Productos.SingleOrDefaultAsync(producto => producto.Id == id, cancellationToken);
        if (producto is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(entrada);
        }

        producto.ActualizarDatos(entrada.Nombre, entrada.Marca, entrada.Presentacion, entrada.CodigoBarras);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            ModelState.AddModelError(nameof(DatosProducto.CodigoBarras), "Ese código de barras ya está registrado.");
            return View(entrada);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Desactivar(int id, CancellationToken cancellationToken) =>
        CambiarEstado(id, false, cancellationToken);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Reactivar(int id, CancellationToken cancellationToken) =>
        CambiarEstado(id, true, cancellationToken);

    public async Task<IActionResult> Detalle(int id, CancellationToken cancellationToken)
    {
        var producto = await BuscarProducto(id, cancellationToken);
        if (producto is null)
        {
            return NotFound();
        }

        var conteos = await db.Conteos.AsNoTracking().Where(c => c.ProductoId == id)
            .OrderByDescending(c => c.FechaUtc).ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);

        return View(new DetalleProducto(producto, conteos));
    }

    [HttpGet]
    public async Task<IActionResult> Contar(int id, CancellationToken cancellationToken)
    {
        var producto = await BuscarProducto(id, cancellationToken);
        if (producto is null)
        {
            return NotFound();
        }

        if (!producto.Activo)
        {
            return MostrarProductoInactivo(producto);
        }

        return View(new RegistroConteo { Producto = producto });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Contar(
        int id, [Bind(nameof(RegistroConteo.UnidadesSueltas), nameof(RegistroConteo.Paquetes))] RegistroConteo entrada,
        CancellationToken cancellationToken)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        var producto = await BuscarProducto(id, cancellationToken);
        if (producto is null)
        {
            return NotFound();
        }

        if (!producto.Activo)
        {
            return MostrarProductoInactivo(producto);
        }

        entrada.Producto = producto;
        ValidarPaquetes(entrada.Paquetes, ModelState);
        if (!ModelState.IsValid)
        {
            return View(entrada);
        }

        var paquetes = entrada.Paquetes
            .Where(p => p.Cantidad.HasValue && p.UnidadesPorPaquete.HasValue)
            .Select(p => new PaqueteContado(p.Cantidad!.Value, p.UnidadesPorPaquete!.Value))
            .ToArray();

        if (!entrada.UnidadesSueltas.HasValue && paquetes.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Ingresá un conteo. Si el producto se agotó, ingresá 0 unidades.");
            return View(entrada);
        }

        ConteoInventario conteo;
        try
        {
            conteo = new ConteoInventario(id, entrada.UnidadesSueltas ?? 0, paquetes, reloj.GetUtcNow().UtcDateTime);
        }
        catch (OverflowException)
        {
            ModelState.AddModelError(string.Empty, "El total supera la cantidad máxima permitida.");
            return View(entrada);
        }

        db.Conteos.Add(conteo);
        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    private async Task<IActionResult> CambiarEstado(int id, bool activo, CancellationToken cancellationToken)
    {
        var producto = await db.Productos.SingleOrDefaultAsync(producto => producto.Id == id, cancellationToken);
        if (producto is null)
        {
            return NotFound();
        }

        producto.EstablecerEstado(activo);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private IActionResult MostrarProductoInactivo(Producto producto)
    {
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View("Inactivo", producto);
    }

    private Task<Producto?> BuscarProducto(int id, CancellationToken cancellationToken) =>
        db.Productos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    private static void ValidarPaquetes(IReadOnlyList<FilaPaquete> paquetes, ModelStateDictionary estado)
    {
        for (var i = 0; i < paquetes.Count; i++)
        {
            var paquete = paquetes[i];

            if (paquete.Cantidad.HasValue && !paquete.UnidadesPorPaquete.HasValue)
            {
                estado.TryAddModelError($"Paquetes[{i}].UnidadesPorPaquete", "Ingresá las unidades por paquete.");
            }
            else if (!paquete.Cantidad.HasValue && paquete.UnidadesPorPaquete.HasValue)
            {
                estado.TryAddModelError($"Paquetes[{i}].Cantidad", "Ingresá la cantidad de paquetes.");
            }
        }
    }
}
