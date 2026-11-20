namespace PulpeStock.Web.Inventario;

public sealed class Producto
{
    private Producto() { }

    public Producto(string nombre, string marca, string presentacion, string? codigoBarras)
    {
        Nombre = nombre.Trim();
        Marca = marca.Trim();
        Presentacion = presentacion.Trim();
        CodigoBarras = string.IsNullOrWhiteSpace(codigoBarras) ? null : codigoBarras.Trim();
    }

    public int Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string Marca { get; private set; } = null!;
    public string Presentacion { get; private set; } = null!;
    public string? CodigoBarras { get; private set; }
    public ICollection<ConteoInventario> Conteos { get; } = new List<ConteoInventario>();
}
