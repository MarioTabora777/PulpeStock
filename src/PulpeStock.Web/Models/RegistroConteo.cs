using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PulpeStock.Web.Inventario;

namespace PulpeStock.Web.Models;

public sealed class RegistroConteo
{
    [Display(Name = "Unidades sueltas o total contado")]
    [Range(0, int.MaxValue, ErrorMessage = "Ingresá una cantidad de unidades válida.")]
    public int? UnidadesSueltas { get; set; }

    public List<FilaPaquete> Paquetes { get; set; } = [];
    [ValidateNever]
    public Producto Producto { get; set; } = null!;
}

public sealed class FilaPaquete
{
    [Display(Name = "Cantidad de paquetes")]
    [Range(1, int.MaxValue, ErrorMessage = "Ingresá al menos un paquete.")]
    public int? Cantidad { get; set; }

    [Display(Name = "Unidades por paquete")]
    [Range(1, int.MaxValue, ErrorMessage = "Ingresá al menos una unidad por paquete.")]
    public int? UnidadesPorPaquete { get; set; }
}
