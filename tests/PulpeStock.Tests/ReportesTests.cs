using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic.FileIO;
using PulpeStock.Web.Datos;
using PulpeStock.Web.Inventario;

namespace PulpeStock.Tests;

public sealed class ReportesTests
{
    [Fact]
    public async Task MuestraElUltimoConteoYLosProductosPendientesDeContar()
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        var datos = await Sembrar(app);

        var pagina = await cliente.GetStringAsync("/Reportes");
        Assert.Contains($"data-producto=\"{datos.Azucar.Id}\"", pagina);
        Assert.Contains($"data-producto=\"{datos.Arroz.Id}\"", pagina);
        Assert.Contains($"data-producto=\"{datos.SinConteo.Id}\"", pagina);
        Assert.Contains("0 unidades", pagina);
        Assert.Contains("Sin conteo", pagina);
        Assert.Contains("19/11/2026 00:00", pagina);

        var filtrado = await cliente.GetStringAsync($"/Reportes?ProductoId={datos.Azucar.Id}");
        Assert.Contains($"data-producto=\"{datos.Azucar.Id}\"", filtrado);
        Assert.DoesNotContain($"data-producto=\"{datos.Arroz.Id}\"", filtrado);
        Assert.DoesNotContain($"data-producto=\"{datos.SinConteo.Id}\"", filtrado);
        Assert.Contains($"selected=\"selected\" value=\"{datos.Azucar.Id}\"", filtrado);
    }

    [Fact]
    public async Task FiltraElHistorialPorProductoYDiaCompletoEnHoraDeHonduras()
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        var datos = await Sembrar(app);

        var pagina = await cliente.GetStringAsync(
            $"/Reportes/Historial?ProductoId={datos.Azucar.Id}&Desde=2026-11-18&Hasta=2026-11-18");

        Assert.Equal(new[] { datos.Conteos[2].Id, datos.Conteos[1].Id }, LeerIdsConteo(pagina));
        Assert.Contains("18/11/2026 00:00", pagina);
        Assert.Contains("18/11/2026 23:59", pagina);
        Assert.Contains("value=\"2026-11-18\"", pagina);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        Assert.Equal(3, await db.Productos.CountAsync());
        Assert.Equal(5, await db.Conteos.CountAsync());
    }

    [Fact]
    public async Task MuestraTodosLosConteosEnOrdenDescendenteSinFiltros()
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        var datos = await Sembrar(app);

        var pagina = await cliente.GetStringAsync("/Reportes/Historial");

        var esperados = datos.Conteos.OrderByDescending(conteo => conteo.FechaUtc)
            .ThenByDescending(conteo => conteo.Id).Select(conteo => conteo.Id).ToArray();
        Assert.Equal(esperados, LeerIdsConteo(pagina));
    }

    [Theory]
    [InlineData("Desde=2026-11-19", 1)]
    [InlineData("Hasta=2026-11-17", 1)]
    [InlineData("Hasta=9999-12-31", 5)]
    public async Task PermiteConsultarConUnaSolaFecha(string filtro, int cantidadEsperada)
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        await Sembrar(app);

        var pagina = await cliente.GetStringAsync("/Reportes/Historial?" + filtro);

        Assert.Equal(cantidadEsperada, LeerIdsConteo(pagina).Length);
    }

    [Fact]
    public async Task InformaCuandoNoHayConteosEnElPeriodo()
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        var datos = await Sembrar(app);

        var pagina = await cliente.GetStringAsync($"/Reportes/Historial?ProductoId={datos.SinConteo.Id}");
        Assert.Contains("No hay conteos para los filtros seleccionados.", pagina);
        Assert.Empty(LeerIdsConteo(pagina));
    }

    [Theory]
    [InlineData("/Reportes?ProductoId=999")]
    [InlineData("/Reportes?ProductoId=abc")]
    [InlineData("/Reportes/Historial?ProductoId=-1")]
    [InlineData("/Reportes/Historial?Desde=2026-11-19&Hasta=2026-11-18")]
    [InlineData("/Reportes/Historial?Desde=2026-02-30")]
    [InlineData("/Reportes/Historial?Hasta=abc")]
    public async Task PideCorregirLosFiltrosInvalidos(string ruta)
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        await Sembrar(app);

        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("field-validation-error", pagina);
        Assert.DoesNotContain("data-producto=", pagina);
        Assert.Empty(LeerIdsConteo(pagina));
    }

    [Fact]
    public async Task PermiteAbrirAmbosReportesConLaBaseVacia()
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();

        var inventario = await cliente.GetStringAsync("/Reportes");
        var historial = await cliente.GetStringAsync("/Reportes/Historial");

        Assert.Contains("Todavía no hay productos registrados.", WebUtility.HtmlDecode(inventario));
        Assert.Contains("No hay conteos para los filtros seleccionados.", historial);
        Assert.Contains("href=\"/Reportes\"", await cliente.GetStringAsync("/"));
    }

    [Fact]
    public async Task ExportaInventarioConElProductoSeleccionadoYCantidadesDeCero()
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        var datos = await Sembrar(app);

        using var respuesta = await cliente.GetAsync($"/Reportes?ProductoId={datos.Azucar.Id}&descargar=true");
        var filas = await LeerCsv(respuesta, "inventario.csv");

        Assert.Equal(2, filas.Count);
        Assert.Equal(new[] { "Azúcar", "Marca A", "Bolsa de 1 libra", "0", "19/11/2026 00:00", "Contado" }, filas[1]);
        using var pendiente = await cliente.GetAsync($"/Reportes?ProductoId={datos.SinConteo.Id}&descargar=true");
        var sinConteo = await LeerCsv(pendiente, "inventario.csv");
        Assert.Equal("", sinConteo[1][3]);
        Assert.Equal("", sinConteo[1][4]);
        Assert.Equal("Sin conteo", sinConteo[1][5]);
    }

    [Fact]
    public async Task ExportaHistorialConLasMismasFechasYProductoQueLaPantalla()
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        var datos = await Sembrar(app);
        var ruta = $"/Reportes/Historial?ProductoId={datos.Azucar.Id}&Desde=2026-11-18&Hasta=2026-11-18";

        var pantalla = await cliente.GetStringAsync(ruta);
        using var respuesta = await cliente.GetAsync(ruta + "&descargar=true");
        var filas = await LeerCsv(respuesta, "historial-conteos.csv");

        Assert.Equal(LeerIdsConteo(pantalla).Length + 1, filas.Count);
        Assert.Equal(new[] { "18/11/2026 23:59", "Azúcar", "Marca A", "Bolsa de 1 libra", "5" }, filas[1]);
        Assert.Equal(new[] { "18/11/2026 00:00", "Azúcar", "Marca A", "Bolsa de 1 libra", "9" }, filas[2]);
    }

    [Fact]
    public async Task ReportaReposicionYPendientesConLaMismaReglaDelModulo()
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        var datos = await Sembrar(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
            var productos = await db.Productos.OrderBy(producto => producto.Id).ToListAsync();
            productos[0].ConfigurarMinimo(3);
            productos[1].ConfigurarMinimo(7);
            productos[2].ConfigurarMinimo(5);
            var conExistencias = new Producto("Aceite", "Marca D", "Botella", null);
            conExistencias.ConfigurarMinimo(1);
            db.Productos.Add(conExistencias);
            await db.SaveChangesAsync();
            db.Conteos.Add(new ConteoInventario(conExistencias.Id, 10, [], DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var reporte = await cliente.GetStringAsync("/Reportes/Reposicion");
        var modulo = await cliente.GetStringAsync("/Reposicion");
        Assert.Contains($"data-producto=\"{datos.Azucar.Id}\"", reporte);
        Assert.Contains($"data-producto=\"{datos.Arroz.Id}\"", reporte);
        Assert.Contains($"data-pendiente=\"{datos.SinConteo.Id}\"", reporte);
        Assert.Equal(Regex.Matches(modulo, "data-producto=").Count, Regex.Matches(reporte, "data-producto=").Count);

        using var respuesta = await cliente.GetAsync("/Reportes/Reposicion?descargar=true");
        var filas = await LeerCsv(respuesta, "reposicion.csv");
        Assert.Equal(4, filas.Count);
        Assert.Equal(new[] { "Azúcar", "Marca A", "Bolsa de 1 libra", "0", "3", "19/11/2026 00:00", "Por reponer" }, filas[2]);
        Assert.Equal(new[] { "Azúcar", "Marca C", "Bolsa de 1 libra", "", "5", "", "Pendiente de contar" }, filas[3]);

        using var filtrado = await cliente.GetAsync($"/Reportes/Reposicion?ProductoId={datos.Arroz.Id}&descargar=true");
        Assert.Equal(2, (await LeerCsv(filtrado, "reposicion.csv")).Count);
        var pantallaFiltrada = await cliente.GetStringAsync($"/Reportes/Reposicion?ProductoId={datos.Arroz.Id}");
        Assert.DoesNotContain($"data-producto=\"{datos.Azucar.Id}\"", pantallaFiltrada);
        Assert.DoesNotContain($"data-pendiente=\"{datos.SinConteo.Id}\"", pantallaFiltrada);
    }

    [Fact]
    public async Task ExportaTextoConComillasComasYSaltosDeLinea()
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
            db.Productos.Add(new Producto("Azúcar, especial", "Marca \"A\"", "Bolsa\r\nde 1 libra", null));
            await db.SaveChangesAsync();
        }

        using var respuesta = await cliente.GetAsync("/Reportes?descargar=true");
        var filas = await LeerCsv(respuesta, "inventario.csv");
        Assert.Equal(2, filas.Count);
        Assert.Equal(new[] { "Azúcar, especial", "Marca \"A\"", "Bolsa\nde 1 libra", "", "", "Sin conteo" },
            filas[1].Select(campo => campo.Replace("\r\n", "\n")).ToArray());
    }

    [Theory]
    [InlineData("/Reportes?descargar=true", "inventario.csv")]
    [InlineData("/Reportes/Historial?descargar=true", "historial-conteos.csv")]
    [InlineData("/Reportes/Reposicion?descargar=true", "reposicion.csv")]
    public async Task ExportaSoloEncabezadosSiLaConsultaEstaVacia(string ruta, string nombre)
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Single(await LeerCsv(respuesta, nombre));
    }

    [Theory]
    [InlineData("/Reportes?ProductoId=abc&descargar=true")]
    [InlineData("/Reportes/Historial?Desde=2026-11-20&Hasta=2026-11-19&descargar=true")]
    [InlineData("/Reportes/Reposicion?ProductoId=999&descargar=true")]
    public async Task ValidaFiltrosAntesDeExportar(string ruta)
    {
        using var app = new AplicacionReportes();
        using var cliente = app.CreateClient();
        await Sembrar(app);

        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal("text/html", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Null(respuesta.Content.Headers.ContentDisposition);
        Assert.Contains("field-validation-error", await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task<List<string[]>> LeerCsv(HttpResponseMessage respuesta, string nombre)
    {
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("text/csv", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", respuesta.Content.Headers.ContentType?.CharSet);
        Assert.Equal(nombre, respuesta.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var contenido = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetPreamble(), contenido.Take(3).ToArray());
        using var parser = new TextFieldParser(new StringReader(Encoding.UTF8.GetString(contenido, 3, contenido.Length - 3)));
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;
        var filas = new List<string[]>();
        while (!parser.EndOfData)
        {
            filas.Add(parser.ReadFields()!);
        }

        return filas;
    }

    private static int[] LeerIdsConteo(string pagina) =>
        Regex.Matches(pagina, "data-conteo=\"(\\d+)\"")
            .Select(coincidencia => int.Parse(coincidencia.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();

    private static async Task<DatosReporte> Sembrar(AplicacionReportes app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        var azucar = new Producto("Azúcar", "Marca A", "Bolsa de 1 libra", null);
        var arroz = new Producto("Arroz", "Marca B", "Bolsa de 1 libra", null);
        var sinConteo = new Producto("Azúcar", "Marca C", "Bolsa de 1 libra", null);
        db.Productos.AddRange(azucar, arroz, sinConteo);
        await db.SaveChangesAsync();

        ConteoInventario[] conteos =
        [
            new(azucar.Id, 10, [], new DateTime(2026, 11, 18, 5, 59, 59, DateTimeKind.Utc)),
            new(azucar.Id, 9, [], new DateTime(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc)),
            new(azucar.Id, 5, [], new DateTime(2026, 11, 19, 5, 59, 59, DateTimeKind.Utc)),
            new(azucar.Id, 0, [], new DateTime(2026, 11, 19, 6, 0, 0, DateTimeKind.Utc)),
            new(arroz.Id, 7, [], new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc))
        ];
        db.Conteos.AddRange(conteos);
        await db.SaveChangesAsync();
        return new DatosReporte(azucar, arroz, sinConteo, conteos);
    }

    private sealed record DatosReporte(
        Producto Azucar, Producto Arroz, Producto SinConteo, IReadOnlyList<ConteoInventario> Conteos);

    private sealed class AplicacionReportes : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _conexion = new(
            $"Data Source=Reportes-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False;Foreign Keys=True");

        public AplicacionReportes() => _conexion.Open();

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:Inventario", _conexion.ConnectionString)
                .ConfigureServices(servicios => servicios.AddDataProtection().UseEphemeralDataProtectionProvider());

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _conexion.Dispose();
            }
        }
    }
}
