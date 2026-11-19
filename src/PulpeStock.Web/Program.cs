using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using PulpeStock.Web.Datos;

var builder = WebApplication.CreateBuilder(args);
var conexion = builder.Configuration.GetConnectionString("Inventario")
    ?? throw new InvalidOperationException("Falta la conexión de Inventario.");

builder.Services.AddControllersWithViews(opciones =>
{
    opciones.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor(
        (_, campo) => $"Revisá el valor de {campo}.");
});
builder.Services.AddDbContext<InventarioDbContext>(opciones => opciones.UseSqlite(conexion));
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Productos/Error");
}

var cultura = CultureInfo.GetCultureInfo("es-HN");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = [cultura],
    SupportedUICultures = [cultura]
});
app.UseStaticFiles();
app.MapControllerRoute("default", "{controller=Productos}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
