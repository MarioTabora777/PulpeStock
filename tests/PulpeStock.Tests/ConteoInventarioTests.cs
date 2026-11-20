using PulpeStock.Web.Inventario;

namespace PulpeStock.Tests;

public sealed class ConteoInventarioTests
{
    [Fact]
    public void SumaUnidadesSueltasYPaquetesDeDistintoTamano()
    {
        var conteo = new ConteoInventario(1, 3,
            [new PaqueteContado(1, 12), new PaqueteContado(1, 6)], DateTime.UnixEpoch);

        Assert.Equal(21, conteo.TotalUnidades);
        Assert.Equal(3, conteo.UnidadesSueltas);
        Assert.Equal(2, conteo.Paquetes.Count);
    }

    [Fact]
    public void AceptaCeroCuandoElProductoSeAgoto()
    {
        var conteo = new ConteoInventario(1, 0, [], DateTime.UnixEpoch);

        Assert.Equal(0, conteo.TotalUnidades);
    }

    [Fact]
    public void RechazaUnidadesSueltasNegativas() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConteoInventario(1, -1, [], DateTime.UnixEpoch));

    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 0)]
    [InlineData(-1, 12)]
    public void RechazaPaquetesConCantidadesNoPositivas(int cantidad, int unidadesPorPaquete) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PaqueteContado(cantidad, unidadesPorPaquete));

    [Fact]
    public void RechazaTotalesQueNoSePuedenGuardar() =>
        Assert.Throws<OverflowException>(
            () => new ConteoInventario(1, 0, [new PaqueteContado(int.MaxValue, 2)], DateTime.UnixEpoch));
}
