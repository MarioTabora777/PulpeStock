using PulpeStock.Web.Inventario;

namespace PulpeStock.Web.Models;

public sealed record FilaProducto(
    int Id, string Nombre, string Marca, string Presentacion, int? UltimoTotal, DateTime? UltimaFechaUtc);

public sealed record DetalleProducto(Producto Producto, IReadOnlyList<ConteoInventario> Conteos);
