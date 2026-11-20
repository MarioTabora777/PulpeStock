using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PulpeStock.Web.Inventario;

namespace PulpeStock.Web.Models;

public sealed class ConfiguracionMinimo
{
    [Display(Name = "Mínimo en unidades")]
    [Range(0, int.MaxValue, ErrorMessage = "Ingresá cero o una cantidad positiva.")]
    public int? ExistenciaMinima { get; set; }

    [ValidateNever]
    public Producto Producto { get; set; } = null!;
}
