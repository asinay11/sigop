namespace Sigop.Dominio;

public sealed class ExcepcionReglaNegocio(string codigo, string mensaje) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
}

public sealed record EventoDominio(string Tipo, string RoutingKey, object Contenido)
{
    public Guid EventoId { get; } = Guid.CreateVersion7();
}
