using System.ComponentModel.DataAnnotations;
using Sigop.Dominio;

namespace Sigop.Aplicacion;

public sealed record RegistrarSolicitudDto
{
    public long? UnidadEjecutoraId { get; init; }

    [Required]
    public TipoOperacion TipoOperacion { get; init; }

    [Range(0.01, 999999999999.99)]
    public decimal Monto { get; init; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Moneda { get; init; } = "GTQ";

    [Required, StringLength(500)]
    public string Concepto { get; init; } = string.Empty;

    [StringLength(200)]
    public string? BeneficiarioNombre { get; init; }

    [StringLength(20)]
    public string? BeneficiarioNit { get; init; }

    [StringLength(30)]
    public string? CuentaBancaria { get; init; }
}

public sealed record ActualizarSolicitudDto
{
    [Range(0.01, 999999999999.99)]
    public decimal Monto { get; init; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Moneda { get; init; } = "GTQ";

    [Required, StringLength(500)]
    public string Concepto { get; init; } = string.Empty;

    [StringLength(200)]
    public string? BeneficiarioNombre { get; init; }

    [StringLength(20)]
    public string? BeneficiarioNit { get; init; }

    [StringLength(30)]
    public string? CuentaBancaria { get; init; }
}

public sealed record CambiarEstadoDto
{
    [Required]
    public EstadoSolicitud EstadoDestino { get; init; }

    [Required, StringLength(1000)]
    public string Motivo { get; init; } = string.Empty;
}

public sealed record ReprocesarDto
{
    [Required, StringLength(1000)]
    public string Motivo { get; init; } = string.Empty;
}

public sealed record SolicitudResumenDto(
    Guid Id, string Numero, string TipoOperacion, decimal Monto, string Moneda,
    string Concepto, string Estado, DateTimeOffset CreadoEn, string CreadoPor);

public sealed record SolicitudDetalleDto(
    Guid Id, string Numero, long EntidadId, long UnidadEjecutoraId,
    string TipoOperacion, bool EsSincrona,
    decimal Monto, string Moneda, string Concepto,
    string? BeneficiarioNombre, string? BeneficiarioNitEnmascarado, string? CuentaBancariaEnmascarada,
    string Estado, IReadOnlyList<string> TransicionesDisponibles,
    int IntentosReproceso, int MaximoReprocesos, string? UltimoError,
    string CorrelationId, DateTimeOffset CreadoEn, string CreadoPor,
    IReadOnlyList<CambioEstadoDto> Historial);

public sealed record CambioEstadoDto(
    string? EstadoAnterior, string EstadoNuevo, string Motivo,
    string Origen, string RealizadoPor, string CorrelationId, DateTimeOffset OcurridoEn);

public sealed record PaginaDto<T>(IReadOnlyList<T> Elementos, int Pagina, int TamanoPagina, int Total)
{
    public int TotalPaginas => TamanoPagina == 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanoPagina);
}

public sealed record ResultadoRegistroDto(
    Guid Id, string Numero, string Estado, bool EsSincrona, string CorrelationId);

public sealed record UnidadEjecutoraDto(long Id, string Codigo, string Nombre);
