using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PulpeStock.Web.Datos;
using PulpeStock.Web.Inventario;

namespace PulpeStock.Tests;

public sealed class MantenimientoProductosTests
{
    [Fact]
    public async Task EditaLosDatosSinCambiarElProductoSuMinimoOSusConteos()
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        var id = await RegistrarProducto(cliente, "Marca A", "001234");
        await AgregarConteo(app, id);
        var pagina = await cliente.GetStringAsync($"/Productos/Editar/{id}");
        Assert.Contains("value=\"001234\"", pagina);
        var campos = CamposProducto(" Marca corregida ", " 009876 ");
        campos["Nombre"] = " Azúcar refinada ";
        campos["Presentacion"] = " Bolsa de 1 libra ";
        campos["Activo"] = "false";
        campos["ExistenciaMinima"] = "999";

        using var respuesta = await EnviarFormulario(cliente, $"/Productos/Editar/{id}", campos);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/Productos/Detalle/{id}", respuesta.Headers.Location?.OriginalString);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        var producto = await db.Productos.Include(producto => producto.Conteos).SingleAsync();
        Assert.Equal(id, producto.Id);
        Assert.Equal("Azúcar refinada", producto.Nombre);
        Assert.Equal("Marca corregida", producto.Marca);
        Assert.Equal("Bolsa de 1 libra", producto.Presentacion);
        Assert.Equal("009876", producto.CodigoBarras);
        Assert.True(producto.Activo);
        Assert.Equal(3, producto.ExistenciaMinima);
        var conteo = Assert.Single(producto.Conteos);
        Assert.Equal(21, conteo.TotalUnidades);
        Assert.Equal(12, Assert.Single(conteo.Paquetes).UnidadesPorPaquete);
    }

    [Fact]
    public async Task AceptaElCodigoPropioYRechazaElDeOtroProductoInclusoInactivo()
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        var primero = await RegistrarProducto(cliente, "Marca A", "001");
        var segundo = await RegistrarProducto(cliente, "Marca B", "002");
        using var desactivacion = await EnviarFormulario(cliente, $"/Productos/Desactivar/{segundo}", [], $"/Productos/Detalle/{segundo}");
        Assert.Equal(HttpStatusCode.Redirect, desactivacion.StatusCode);

        using var propio = await EnviarFormulario(cliente, $"/Productos/Editar/{primero}", CamposProducto("Marca A", "001"));
        Assert.Equal(HttpStatusCode.Redirect, propio.StatusCode);

        using var repetido = await EnviarFormulario(cliente, $"/Productos/Editar/{primero}", CamposProducto("Cambio rechazado", "002"));
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Contains("Ese código de barras ya está registrado.", WebUtility.HtmlDecode(await repetido.Content.ReadAsStringAsync()));

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        var producto = await db.Productos.SingleAsync(producto => producto.Id == primero);
        Assert.Equal("001", producto.CodigoBarras);
        Assert.Equal("Marca A", producto.Marca);
        Assert.Equal(2, await db.Productos.CountAsync());
    }

    [Fact]
    public async Task PermiteQuitarElCodigoDeBarrasOpcional()
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        var id = await RegistrarProducto(cliente, "Marca A", "001");
        using var respuesta = await EnviarFormulario(cliente, $"/Productos/Editar/{id}", CamposProducto("Marca A", "  "));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        Assert.Null((await db.Productos.SingleAsync()).CodigoBarras);
    }

    [Theory]
    [InlineData("Nombre", "")]
    [InlineData("Marca", "   ")]
    [InlineData("Presentacion", "")]
    public async Task ValidaCamposObligatoriosEnAltaYEdicion(string campo, string valor)
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        var id = await RegistrarProducto(cliente, "Marca A", null);
        var campos = CamposProducto("Marca A", "");
        campos[campo] = valor;

        using var edicion = await EnviarFormulario(cliente, $"/Productos/Editar/{id}", campos);
        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);
        Assert.Contains("field-validation-error", await edicion.Content.ReadAsStringAsync());
        using var alta = await EnviarFormulario(cliente, "/Productos/Nuevo", campos);
        Assert.Equal(HttpStatusCode.OK, alta.StatusCode);
        Assert.Contains("field-validation-error", await alta.Content.ReadAsStringAsync());

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        var producto = await db.Productos.SingleAsync();
        Assert.Equal("Azúcar", producto.Nombre);
        Assert.Equal("Marca A", producto.Marca);
        Assert.Equal("Bolsa de 1 libra", producto.Presentacion);
    }

    [Theory]
    [InlineData("Nombre", 101)]
    [InlineData("Marca", 81)]
    [InlineData("Presentacion", 81)]
    [InlineData("CodigoBarras", 41)]
    public async Task RechazaDatosQueSuperanLaLongitudPermitida(string campo, int longitud)
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        var id = await RegistrarProducto(cliente, "Marca A", null);
        var campos = CamposProducto("Marca A", "");
        campos[campo] = new string('x', longitud);

        using var respuesta = await EnviarFormulario(cliente, $"/Productos/Editar/{id}", campos);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("field-validation-error", await respuesta.Content.ReadAsStringAsync());

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        Assert.Equal("Marca A", (await db.Productos.SingleAsync()).Marca);
    }

    [Fact]
    public async Task DesactivaYReactivaConservandoHistorialYMinimo()
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        var id = await RegistrarProducto(cliente, "Marca A", null);
        var otro = await RegistrarProducto(cliente, "Marca B", null);
        await AgregarConteo(app, id);
        using var conteoBajo = await EnviarFormulario(cliente, $"/Productos/Contar/{id}", new() { ["UnidadesSueltas"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, conteoBajo.StatusCode);
        Assert.Contains($"data-producto=\"{id}\"", await cliente.GetStringAsync("/Reposicion"));
        var conteoPendiente = await CamposConToken(cliente, $"/Productos/Contar/{id}", new() { ["UnidadesSueltas"] = "1" });

        for (var intento = 0; intento < 2; intento++)
        {
            using var desactivar = await EnviarFormulario(cliente, $"/Productos/Desactivar/{id}", [], $"/Productos/Detalle/{id}");
            Assert.Equal(HttpStatusCode.Redirect, desactivar.StatusCode);
        }

        var edicionInactivo = CamposProducto("Marca A", null);
        edicionInactivo["Activo"] = "true";
        using var edicion = await EnviarFormulario(cliente, $"/Productos/Editar/{id}", edicionInactivo);
        Assert.Equal(HttpStatusCode.Redirect, edicion.StatusCode);
        var activos = await cliente.GetStringAsync("/Productos");
        Assert.DoesNotContain($"data-producto=\"{id}\"", activos);
        Assert.Contains($"data-producto=\"{otro}\"", activos);
        var inactivos = await cliente.GetStringAsync("/Productos?Estado=Inactivos");
        Assert.Contains($"data-producto=\"{id}\"", inactivos);
        Assert.DoesNotContain($"data-producto=\"{otro}\"", inactivos);
        var todos = await cliente.GetStringAsync("/Productos?Estado=Todos");
        Assert.Contains($"data-producto=\"{id}\"", todos);
        Assert.Contains($"data-producto=\"{otro}\"", todos);

        Assert.Contains("<td>21</td>", await cliente.GetStringAsync($"/Productos/Detalle/{id}"));
        Assert.Contains("data-conteo=", await cliente.GetStringAsync($"/Reportes/Historial?ProductoId={id}"));
        Assert.Contains("Inactivo", await cliente.GetStringAsync($"/Reportes?ProductoId={id}"));
        Assert.DoesNotContain($"data-producto=\"{id}\"", await cliente.GetStringAsync("/Reposicion"));

        using var formularioInactivo = await cliente.GetAsync($"/Productos/Contar/{id}");
        Assert.Equal(HttpStatusCode.Conflict, formularioInactivo.StatusCode);
        using var contenido = new FormUrlEncodedContent(conteoPendiente);
        using var envioAntiguo = await cliente.PostAsync($"/Productos/Contar/{id}", contenido);
        Assert.Equal(HttpStatusCode.Conflict, envioAntiguo.StatusCode);

        for (var intento = 0; intento < 2; intento++)
        {
            using var reactivar = await EnviarFormulario(cliente, $"/Productos/Reactivar/{id}", [], $"/Productos/Detalle/{id}");
            Assert.Equal(HttpStatusCode.Redirect, reactivar.StatusCode);
        }

        using var nuevoConteo = await EnviarFormulario(cliente, $"/Productos/Contar/{id}", new() { ["UnidadesSueltas"] = "2" });
        Assert.Equal(HttpStatusCode.Redirect, nuevoConteo.StatusCode);
        Assert.Contains($"data-producto=\"{id}\"", await cliente.GetStringAsync("/Reposicion"));
        Assert.Contains($"data-producto=\"{id}\"", await cliente.GetStringAsync("/Reportes/Reposicion"));

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        var producto = await db.Productos.SingleAsync(producto => producto.Id == id);
        Assert.True(producto.Activo);
        Assert.Equal(3, producto.ExistenciaMinima);
        Assert.Equal(3, await db.Conteos.CountAsync(conteo => conteo.ProductoId == id));
    }

    [Theory]
    [InlineData("Editar")]
    [InlineData("Desactivar")]
    [InlineData("Reactivar")]
    public async Task ExigeTokenParaLasOperacionesDeMantenimiento(string accion)
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        var id = await RegistrarProducto(cliente, "Marca A", null);
        using var campos = new FormUrlEncodedContent(CamposProducto("Marca modificada", ""));
        using var respuesta = await cliente.PostAsync($"/Productos/{accion}/{id}", campos);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("Editar")]
    [InlineData("Desactivar")]
    [InlineData("Reactivar")]
    public async Task DevuelveNoEncontradoParaUnProductoInexistente(string accion)
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        using var respuesta = await EnviarFormulario(cliente, $"/Productos/{accion}/999",
            CamposProducto("Marca A", null), "/Productos/Nuevo");
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        using var formulario = await cliente.GetAsync("/Productos/Editar/999");
        Assert.Equal(HttpStatusCode.NotFound, formulario.StatusCode);
    }

    [Theory]
    [InlineData("Desactivar")]
    [InlineData("Reactivar")]
    public async Task CambiaElEstadoSoloMediantePost(string accion)
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        var id = await RegistrarProducto(cliente, "Marca A", null);
        using var respuesta = await cliente.GetAsync($"/Productos/{accion}/{id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("abc")]
    public async Task SolicitaCorregirUnFiltroDeEstadoInvalido(string estado)
    {
        using var app = new AplicacionProductos();
        using var cliente = app.CrearCliente();
        var pagina = await cliente.GetStringAsync("/Productos?Estado=" + estado);
        Assert.Contains("field-validation-error", pagina);
        Assert.DoesNotContain("data-producto=", pagina);
    }

    [Fact]
    public async Task ActualizaUnaBaseExistenteYConservaLosProductosActivos()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<InventarioDbContext>().UseSqlite(conexion).Options;
        await using var db = new InventarioDbContext(opciones);
        var anterior = db.Database.GetMigrations().Single(migracion => migracion.EndsWith("_AgregarMinimoReposicion", StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(anterior);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Productos (Id, Nombre, Marca, Presentacion, ExistenciaMinima)
                VALUES (1, 'Azúcar', 'Marca A', 'Bolsa de 1 libra', 3);
            INSERT INTO Conteos (ProductoId, UnidadesSueltas, TotalUnidades, FechaUtc)
                VALUES (1, 2, 2, '2026-11-23 12:00:00');
            """);

        await db.Database.MigrateAsync();
        var producto = await db.Productos.SingleAsync();
        Assert.True(producto.Activo);
        Assert.Equal(3, producto.ExistenciaMinima);
        Assert.Equal(2, (await db.Conteos.SingleAsync()).TotalUnidades);
        producto.EstablecerEstado(false);
        var inactivo = new Producto("Arroz", "Marca B", "Bolsa de 1 libra", null);
        inactivo.EstablecerEstado(false);
        db.Productos.Add(inactivo);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.All(await db.Productos.ToListAsync(), guardado => Assert.False(guardado.Activo));
    }

    private static Dictionary<string, string> CamposProducto(string marca, string? codigo) => new()
    {
        ["Nombre"] = "Azúcar",
        ["Marca"] = marca,
        ["Presentacion"] = "Bolsa de 1 libra",
        ["CodigoBarras"] = codigo ?? string.Empty
    };

    private static async Task<int> RegistrarProducto(HttpClient cliente, string marca, string? codigo)
    {
        using var respuesta = await EnviarFormulario(cliente, "/Productos/Nuevo", CamposProducto(marca, codigo));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return int.Parse(respuesta.Headers.Location!.OriginalString.Split('/').Last(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task AgregarConteo(AplicacionProductos app, int id)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
        (await db.Productos.SingleAsync(producto => producto.Id == id)).ConfigurarMinimo(3);
        db.Conteos.Add(new ConteoInventario(id, 9, [new PaqueteContado(1, 12)], DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, string>> CamposConToken(
        HttpClient cliente, string ruta, Dictionary<string, string> campos)
    {
        var pagina = await cliente.GetStringAsync(ruta);
        var token = Regex.Match(pagina, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        campos["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return campos;
    }

    private static async Task<HttpResponseMessage> EnviarFormulario(
        HttpClient cliente, string ruta, Dictionary<string, string> campos, string? paginaFormulario = null)
    {
        await CamposConToken(cliente, paginaFormulario ?? ruta, campos);
        using var contenido = new FormUrlEncodedContent(campos);
        return await cliente.PostAsync(ruta, contenido);
    }

    private sealed class AplicacionProductos : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _conexion = new(
            $"Data Source=Productos-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False;Foreign Keys=True");

        public AplicacionProductos() => _conexion.Open();

        public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

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
