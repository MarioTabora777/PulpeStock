using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulpeStock.Web.Datos;

namespace PulpeStock.Tests;

public sealed class FlujoConteoTests
{
    [Fact]
    public async Task GuardaElDesgloseYConservaHistorialPorMarca()
    {
        using var app = new AplicacionPruebas();
        using var cliente = app.CrearCliente();
        var marcaA = await RegistrarProducto(cliente, "Marca A");
        var marcaB = await RegistrarProducto(cliente, "Marca B");

        Assert.Contains("Sin conteo", await cliente.GetStringAsync(marcaA));

        using var segundoProducto = await EnviarFormulario(cliente, RutaConteo(marcaB),
            new Dictionary<string, string> { ["UnidadesSueltas"] = "5" });
        Assert.Equal(HttpStatusCode.Redirect, segundoProducto.StatusCode);

        using var primerConteo = await EnviarFormulario(cliente, RutaConteo(marcaA),
            new Dictionary<string, string>
            {
                ["UnidadesSueltas"] = "3",
                ["Paquetes[0].Cantidad"] = "1",
                ["Paquetes[0].UnidadesPorPaquete"] = "12",
                ["Paquetes[1].Cantidad"] = "1",
                ["Paquetes[1].UnidadesPorPaquete"] = "6"
            });
        Assert.Equal(HttpStatusCode.Redirect, primerConteo.StatusCode);
        var detalle = await cliente.GetStringAsync(marcaA);
        Assert.Contains("21 <span>unidades</span>", detalle);
        Assert.Contains("1 × 12", detalle);
        Assert.Contains("1 × 6", detalle);
        Assert.Contains("<time datetime=", detalle);

        using var conteoEnCero = await EnviarFormulario(cliente, RutaConteo(marcaA),
            new Dictionary<string, string> { ["UnidadesSueltas"] = "0" });
        Assert.Equal(HttpStatusCode.Redirect, conteoEnCero.StatusCode);

        var historial = await cliente.GetStringAsync(marcaA);
        Assert.Contains("0 <span>unidades</span>", historial);
        Assert.Contains("<td>21</td>", historial);
        Assert.Contains("5 <span>unidades</span>", await cliente.GetStringAsync(marcaB));

        var catalogo = await cliente.GetStringAsync("/");
        Assert.Contains("0 unidades", catalogo);
        Assert.Contains("5 unidades", catalogo);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        var conteo = await db.Conteos.SingleAsync(c => c.TotalUnidades == 21);
        Assert.Equal(3, conteo.UnidadesSueltas);
        Assert.Equal(new[] { 6, 12 },
            conteo.Paquetes.Select(paquete => paquete.UnidadesPorPaquete).Order().ToArray());
    }

    [Fact]
    public async Task RechazaUnPaqueteIncompletoYPermiteContarSoloPaquetes()
    {
        using var app = new AplicacionPruebas();
        using var cliente = app.CrearCliente();
        var producto = await RegistrarProducto(cliente, "Marca A");

        using var incompleto = await EnviarFormulario(cliente, RutaConteo(producto),
            new Dictionary<string, string> { ["Paquetes[0].Cantidad"] = "2" });
        Assert.Equal(HttpStatusCode.OK, incompleto.StatusCode);
        var formulario = WebUtility.HtmlDecode(await incompleto.Content.ReadAsStringAsync());
        Assert.Contains("Ingresá las unidades por paquete", formulario);
        Assert.Contains("Sin conteo", await cliente.GetStringAsync(producto));

        using var completo = await EnviarFormulario(cliente, RutaConteo(producto),
            new Dictionary<string, string>
            {
                ["Paquetes[0].Cantidad"] = "2",
                ["Paquetes[0].UnidadesPorPaquete"] = "12"
            });
        Assert.Equal(HttpStatusCode.Redirect, completo.StatusCode);
        Assert.Contains("24 <span>unidades</span>", await cliente.GetStringAsync(producto));
    }

    [Fact]
    public async Task RechazaUnConteoVacioSinConfundirloConCero()
    {
        using var app = new AplicacionPruebas();
        using var cliente = app.CrearCliente();
        var producto = await RegistrarProducto(cliente, "Marca A");

        using var respuesta = await EnviarFormulario(cliente, RutaConteo(producto), []);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var formulario = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("Ingresá un conteo", formulario);
        Assert.Contains("Sin conteo", await cliente.GetStringAsync(producto));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task ComparaElUltimoConteoConElMinimoPorMarca(int cantidad, bool requiereReposicion)
    {
        using var app = new AplicacionPruebas();
        using var cliente = app.CrearCliente();
        var marcaA = await RegistrarProducto(cliente, "Marca A");
        var marcaB = await RegistrarProducto(cliente, "Marca B");
        var rutaMinimo = new Uri(marcaA.OriginalString.Replace("/Productos/Detalle/", "/Reposicion/Minimo/"), UriKind.Relative);

        using var minimo = await EnviarFormulario(cliente, rutaMinimo,
            new Dictionary<string, string> { ["ExistenciaMinima"] = "3" });
        Assert.Equal(HttpStatusCode.Redirect, minimo.StatusCode);

        using var conteo = await EnviarFormulario(cliente, RutaConteo(marcaA),
            new Dictionary<string, string> { ["UnidadesSueltas"] = cantidad.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, conteo.StatusCode);

        var reposicion = await cliente.GetStringAsync("/Reposicion");
        var id = marcaA.OriginalString.Split('/').Last();
        Assert.Equal(requiereReposicion, reposicion.Contains($"data-producto=\"{id}\""));
        Assert.DoesNotContain("Marca B", reposicion);
        if (requiereReposicion)
        {
            Assert.Contains("<time datetime=", reposicion);
        }

        using var nuevoConteo = await EnviarFormulario(cliente, RutaConteo(marcaA),
            new Dictionary<string, string> { ["UnidadesSueltas"] = "10" });
        Assert.Equal(HttpStatusCode.Redirect, nuevoConteo.StatusCode);
        Assert.DoesNotContain($"data-producto=\"{id}\"", await cliente.GetStringAsync("/Reposicion"));

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        Assert.Equal(2, await db.Conteos.CountAsync());
        Assert.Null((await db.Productos.SingleAsync(producto => producto.Marca == "Marca B")).ExistenciaMinima);
    }

    [Fact]
    public async Task PermiteDesactivarElMinimoYConservaLosConteos()
    {
        using var app = new AplicacionPruebas();
        using var cliente = app.CrearCliente();
        var producto = await RegistrarProducto(cliente, "Marca A");
        var rutaMinimo = new Uri(producto.OriginalString.Replace("/Productos/Detalle/", "/Reposicion/Minimo/"), UriKind.Relative);
        using var minimo = await EnviarFormulario(cliente, rutaMinimo,
            new Dictionary<string, string> { ["ExistenciaMinima"] = "0" });
        Assert.Equal(HttpStatusCode.Redirect, minimo.StatusCode);

        var pendientes = await cliente.GetStringAsync("/Reposicion");
        Assert.Contains("data-pendiente=", pendientes);
        Assert.DoesNotContain("data-producto=", pendientes);

        using var conteo = await EnviarFormulario(cliente, RutaConteo(producto),
            new Dictionary<string, string> { ["UnidadesSueltas"] = "0" });
        Assert.Equal(HttpStatusCode.Redirect, conteo.StatusCode);
        Assert.Contains("data-producto=", await cliente.GetStringAsync("/Reposicion"));

        using var desactivar = await EnviarFormulario(cliente, rutaMinimo,
            new Dictionary<string, string> { ["ExistenciaMinima"] = "" });
        Assert.Equal(HttpStatusCode.Redirect, desactivar.StatusCode);
        var reposicion = await cliente.GetStringAsync("/Reposicion");
        Assert.DoesNotContain("data-producto=", reposicion);
        Assert.DoesNotContain("data-pendiente=", reposicion);
        Assert.Contains("0 <span>unidades</span>", await cliente.GetStringAsync(producto));

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        Assert.Null((await db.Productos.SingleAsync()).ExistenciaMinima);
        Assert.Equal(1, await db.Conteos.CountAsync());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    public async Task RechazaMinimosInvalidosSinReemplazarElValorGuardado(string cantidad)
    {
        using var app = new AplicacionPruebas();
        using var cliente = app.CrearCliente();
        var producto = await RegistrarProducto(cliente, "Marca A");
        var rutaMinimo = new Uri(producto.OriginalString.Replace("/Productos/Detalle/", "/Reposicion/Minimo/"), UriKind.Relative);
        using var inicial = await EnviarFormulario(cliente, rutaMinimo,
            new Dictionary<string, string> { ["ExistenciaMinima"] = "3" });
        Assert.Equal(HttpStatusCode.Redirect, inicial.StatusCode);

        using var invalido = await EnviarFormulario(cliente, rutaMinimo,
            new Dictionary<string, string> { ["ExistenciaMinima"] = cantidad });
        Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
        Assert.Contains("field-validation-error", await invalido.Content.ReadAsStringAsync());

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        Assert.Equal(3, (await db.Productos.SingleAsync()).ExistenciaMinima);
    }

    [Fact]
    public async Task ExigeTokenAlGuardarUnMinimo()
    {
        using var app = new AplicacionPruebas();
        using var cliente = app.CrearCliente();
        using var campos = new FormUrlEncodedContent(new Dictionary<string, string> { ["ExistenciaMinima"] = "3" });
        using var respuesta = await cliente.PostAsync("/Reposicion/Minimo/1", campos);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var desconocido = await cliente.GetAsync("/Reposicion/Minimo/999");
        Assert.Equal(HttpStatusCode.NotFound, desconocido.StatusCode);
    }

    private static async Task<Uri> RegistrarProducto(HttpClient cliente, string marca)
    {
        using var respuesta = await EnviarFormulario(cliente, new Uri("/Productos/Nuevo", UriKind.Relative),
            new Dictionary<string, string>
            {
                ["Nombre"] = "Azúcar",
                ["Marca"] = marca,
                ["Presentacion"] = "Bolsa de 1 libra"
            });

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return Assert.IsType<Uri>(respuesta.Headers.Location);
    }

    private static Uri RutaConteo(Uri producto) =>
        new(producto.OriginalString.Replace("/Detalle/", "/Contar/"), UriKind.Relative);

    private static async Task<HttpResponseMessage> EnviarFormulario(
        HttpClient cliente, Uri ruta, Dictionary<string, string> campos)
    {
        var pagina = await cliente.GetStringAsync(ruta);
        var token = Regex.Match(pagina, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "El formulario debe incluir un token antifalsificación.");
        campos["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        using var contenido = new FormUrlEncodedContent(campos);
        return await cliente.PostAsync(ruta, contenido);
    }

    private sealed class AplicacionPruebas : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _ancla = new(
            $"Data Source=Pruebas-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False;Foreign Keys=True");

        public AplicacionPruebas() => _ancla.Open();

        public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:Inventario", _ancla.ConnectionString)
                .ConfigureServices(servicios => servicios.AddDataProtection().UseEphemeralDataProtectionProvider());

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _ancla.Dispose();
            }
        }
    }
}
