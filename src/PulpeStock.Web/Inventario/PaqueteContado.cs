namespace PulpeStock.Web.Inventario;

public sealed class PaqueteContado
{
    private PaqueteContado() { }

    public PaqueteContado(int cantidad, int unidadesPorPaquete)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidad);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unidadesPorPaquete);

        Cantidad = cantidad;
        UnidadesPorPaquete = unidadesPorPaquete;
    }

    public int Cantidad { get; private set; }
    public int UnidadesPorPaquete { get; private set; }

    internal long TotalUnidades => (long)Cantidad * UnidadesPorPaquete;
}
