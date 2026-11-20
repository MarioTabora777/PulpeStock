namespace PulpeStock.Web.Models;

public sealed record FilaReposicion(
    int Id, string Nombre, string Marca, string Presentacion, int Minimo,
    int? UltimoTotal, DateTime? UltimaFechaUtc);

public sealed record ListaReposicion(
    IReadOnlyList<FilaReposicion> PorReponer, IReadOnlyList<FilaReposicion> PendientesDeConteo);
