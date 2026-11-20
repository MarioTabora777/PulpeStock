using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PulpeStock.Web.Models;

public sealed class ReporteInventario
{
    [Display(Name = "Producto")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un producto válido.")]
    public int? ProductoId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> Opciones { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<FilaProducto> Productos { get; set; } = [];
}

public sealed class ReporteHistorial
{
    [Display(Name = "Producto")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un producto válido.")]
    public int? ProductoId { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? Desde { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? Hasta { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> Opciones { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<FilaHistorial> Conteos { get; set; } = [];
}

public sealed record FilaHistorial(
    int Id, int ProductoId, string Nombre, string Marca, string Presentacion,
    int TotalUnidades, DateTime FechaUtc);
