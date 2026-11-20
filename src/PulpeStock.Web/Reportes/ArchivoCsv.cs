using System.Text;

namespace PulpeStock.Web.Reportes;

public static class ArchivoCsv
{
    public static byte[] Crear(string[] encabezados, IEnumerable<string?[]> filas)
    {
        var contenido = new StringBuilder();
        contenido.AppendJoin(',', encabezados.Select(Escapar)).Append("\r\n");
        foreach (var fila in filas)
        {
            contenido.AppendJoin(',', fila.Select(Escapar)).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(contenido.ToString())];
    }

    private static string Escapar(string? valor)
    {
        var texto = valor ?? string.Empty;
        var inicio = texto.TrimStart();
        if (inicio.Length > 0 && "=+-@".Contains(inicio[0]))
        {
            texto = "'" + texto;
        }

        return "\"" + texto.Replace("\"", "\"\"") + "\"";
    }
}
