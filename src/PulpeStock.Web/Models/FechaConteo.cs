using System.Globalization;

namespace PulpeStock.Web.Models;

public static class FechaConteo
{
    private static readonly TimeZoneInfo ZonaPulperia = TimeZoneInfo.FindSystemTimeZoneById("America/Tegucigalpa");

    public static string EnHoraLocal(DateTime fechaUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, ZonaPulperia);
        return local.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-HN"));
    }

    public static string EnIsoUtc(DateTime fechaUtc) =>
        DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
}
