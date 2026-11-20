using System.Text;
using PulpeStock.Web.Reportes;

namespace PulpeStock.Tests;

public sealed class ArchivoCsvTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1+1")]
    [InlineData("-1+1")]
    [InlineData("@SUM(1,1)")]
    [InlineData(" \t=1+1")]
    [InlineData("\r\n=1+1")]
    public void ExportaComoTextoLosValoresQuePuedenInterpretarseComoFormulas(string valor)
    {
        var bytes = ArchivoCsv.Crear(["Producto"], [new[] { valor }]);
        var csv = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        Assert.Equal("\"Producto\"\r\n\"'" + valor + "\"\r\n", csv);
    }

    [Fact]
    public void ConservaCeroYEscapaComillasDentroDeCadaCampo()
    {
        var bytes = ArchivoCsv.Crear(["Nombre", "Cantidad"], [new[] { "Marca \"A\"", "0" }]);
        var csv = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        Assert.Equal("\"Nombre\",\"Cantidad\"\r\n\"Marca \"\"A\"\"\",\"0\"\r\n", csv);
    }
}
