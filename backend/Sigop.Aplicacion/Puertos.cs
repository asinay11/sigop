using Sigop.Dominio;

namespace Sigop.Aplicacion;

public interface IRepositorioSolicitudes
{
    Task<Solicitud?> ObtenerAsync(Guid id, long entidadId, CancellationToken ct = default);

    Task<Solicitud?> ObtenerParaProcesoAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<Solicitud> Elementos, int Total)> BuscarAsync(FiltroSolicitudes filtro, CancellationToken ct = default);

    Task<string> SiguienteNumeroAsync(CancellationToken ct = default);

    void Agregar(Solicitud solicitud);
}

public interface IRepositorioCatalogos
{
    Task<IReadOnlyList<UnidadEjecutoraDto>> UnidadesAsync(long entidadId, CancellationToken ct = default);
}

public interface IUnidadDeTrabajo
{
    Task<int> ConfirmarAsync(CancellationToken ct = default);
}

public interface IContextoUsuario
{
    string Usuario { get; }
    long EntidadId { get; }
    string CorrelationId { get; }
}

public interface IProtectorDatos
{
    string? Cifrar(string? valor);
    string? Descifrar(string? valor);

    string? Enmascarar(string? valor);
}

public sealed record FiltroSolicitudes(
    long EntidadId,
    EstadoSolicitud? Estado = null,
    string? Numero = null,
    int Pagina = 1,
    int TamanoPagina = 20);

public sealed class ExcepcionNoEncontrado(string mensaje) : Exception(mensaje);

public sealed class ExcepcionAccesoDenegado(string mensaje) : Exception(mensaje);
