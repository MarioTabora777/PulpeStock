namespace PulpeStock.Web.Inventario;

public sealed class ConteoInventario
{
    private readonly List<PaqueteContado> _paquetes = [];

    private ConteoInventario() { }

    public ConteoInventario(
        int productoId, int unidadesSueltas, IEnumerable<PaqueteContado> paquetes, DateTime fechaUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unidadesSueltas);

        ProductoId = productoId;
        UnidadesSueltas = unidadesSueltas;
        FechaUtc = fechaUtc;
        _paquetes.AddRange(paquetes);

        var total = unidadesSueltas;
        foreach (var paquete in _paquetes)
        {
            total = checked(total + (int)paquete.TotalUnidades);
        }

        TotalUnidades = total;
    }

    public int Id { get; private set; }
    public int ProductoId { get; private set; }
    public int UnidadesSueltas { get; private set; }
    public int TotalUnidades { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public IReadOnlyList<PaqueteContado> Paquetes => _paquetes.AsReadOnly();
}
