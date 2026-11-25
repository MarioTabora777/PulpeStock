using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PulpeStock.Web.Models;

public enum EstadoProductos
{
    Activos,
    Inactivos,
    Todos
}

public sealed class ListadoProductos
{
    [EnumDataType(typeof(EstadoProductos), ErrorMessage = "Seleccioná un estado válido.")]
    public EstadoProductos Estado { get; set; } = EstadoProductos.Activos;

    [ValidateNever]
    public IReadOnlyList<FilaProducto> Productos { get; set; } = [];
}
