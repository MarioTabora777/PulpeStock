using System.ComponentModel.DataAnnotations;

namespace PulpeStock.Web.Models;

public sealed class DatosProducto
{
    [Required(ErrorMessage = "Ingresá el nombre del producto.")]
    [StringLength(100, ErrorMessage = "Usá hasta 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá la marca.")]
    [StringLength(80, ErrorMessage = "Usá hasta 80 caracteres.")]
    public string Marca { get; set; } = string.Empty;

    [Display(Name = "Presentación")]
    [Required(ErrorMessage = "Ingresá la presentación.")]
    [StringLength(80, ErrorMessage = "Usá hasta 80 caracteres.")]
    public string Presentacion { get; set; } = string.Empty;

    [Display(Name = "Código de barras")]
    [StringLength(40, ErrorMessage = "Usá hasta 40 caracteres.")]
    public string? CodigoBarras { get; set; }
}
