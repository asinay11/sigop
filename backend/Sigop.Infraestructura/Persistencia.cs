using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Sigop.Aplicacion;
using Sigop.Dominio;

namespace Sigop.Infraestructura;

public static class Topologia
{
    public const string ExchangeEventos = "sigop.eventos";
    public const string ExchangeReintentos = "sigop.reintentos";
    public const string ExchangeDlx = "sigop.dlx";

    public const string RkSolicitudRegistrada = "solicitud.registrada.v1";
    public const string RkCambioEstado = "solicitud.estado-cambiado.v1";

    public const string CabeceraIntentos = "x-intentos";
    public const string CabeceraUltimoError = "x-ultimo-error";

    public const int SegundosReintento = 30;

    public const int MaximoReintentos = 3;

    public static string Cola(string codigoEntidad) => $"sigop.q.validacion.{codigoEntidad}";

    public static string ColaReintento(string codigoEntidad) =>
        $"{Cola(codigoEntidad)}.retry.{SegundosReintento}s";

    public static string Dlq(string codigoEntidad) => $"{Cola(codigoEntidad)}.dlq";

    public static string PatronEntidad(string codigoEntidad) => $"{codigoEntidad}.#";

    public static string RoutingKey(string codigoEntidad, string evento) => $"{codigoEntidad}.{evento}";
}

public static class Json
{
    public static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class RepositorioSolicitudes(SigopDbContext contexto) : IRepositorioSolicitudes
{
    public Task<Solicitud?> ObtenerAsync(Guid id, long entidadId, CancellationToken ct = default) =>
        contexto.Solicitudes
            .Include(s => s.Historial)
            .FirstOrDefaultAsync(s => s.Id == id && s.EntidadId == entidadId, ct);

    public Task<Solicitud?> ObtenerParaProcesoAsync(Guid id, CancellationToken ct = default) =>
        contexto.Solicitudes
            .Include(s => s.Historial)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<(IReadOnlyList<Solicitud> Elementos, int Total)> BuscarAsync(
        FiltroSolicitudes filtro, CancellationToken ct = default)
    {
        var consulta = contexto.Solicitudes
            .AsNoTracking()
            .Where(s => s.EntidadId == filtro.EntidadId);

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(s => s.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Numero))
        {
            consulta = consulta.Where(s => EF.Functions.ILike(s.Numero, $"%{filtro.Numero.Trim()}%"));
        }

        var total = await consulta.CountAsync(ct);

        var elementos = await consulta
            .OrderByDescending(s => s.CreadoEn)
            .Skip((filtro.Pagina - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .ToListAsync(ct);

        return (elementos, total);
    }

    public async Task<string> SiguienteNumeroAsync(CancellationToken ct = default)
    {
        var correlativo = await contexto.Database
            .SqlQuery<long>($"SELECT nextval('seq_solicitud') AS \"Value\"")
            .SingleAsync(ct);

        return $"SOL-{DateTimeOffset.UtcNow.Year}-{correlativo:D6}";
    }

    public void Agregar(Solicitud solicitud) => contexto.Solicitudes.Add(solicitud);
}

public sealed class UnidadDeTrabajo(SigopDbContext contexto, IContextoUsuario usuario) : IUnidadDeTrabajo
{
    public async Task<int> ConfirmarAsync(CancellationToken ct = default)
    {
        await VolcarEventosAlOutboxAsync(ct);
        return await contexto.SaveChangesAsync(ct);
    }

    private async Task VolcarEventosAlOutboxAsync(CancellationToken ct)
    {
        var agregados = contexto.ChangeTracker
            .Entries<Solicitud>()
            .Select(e => e.Entity)
            .Where(s => s.Eventos.Count > 0)
            .ToList();

        foreach (var solicitud in agregados)
        {
            foreach (var evento in solicitud.Eventos)
            {
                contexto.Outbox.Add(new MensajeSaliente
                {
                    Id = evento.EventoId,
                    Exchange = Topologia.ExchangeEventos,
                    RoutingKey = evento.RoutingKey,
                    TipoMensaje = evento.Tipo,
                    Contenido = JsonSerializer.Serialize(evento.Contenido, Json.Opciones),
                    EntidadId = solicitud.EntidadId,
                    SolicitudNumero = solicitud.Numero,
                    CorrelationId = usuario.CorrelationId,
                    Estado = "PENDIENTE",
                    CreadoEn = DateTimeOffset.UtcNow,
                });
            }

            solicitud.LimpiarEventos();
        }
    }
}

public sealed class RepositorioCatalogos(SigopDbContext contexto) : IRepositorioCatalogos
{
    public async Task<IReadOnlyList<UnidadEjecutoraDto>> UnidadesAsync(long entidadId, CancellationToken ct = default) =>
        await contexto.UnidadesEjecutoras
            .AsNoTracking()
            .Where(u => u.EntidadId == entidadId && u.Estado == "ACTIVA")
            .OrderBy(u => u.Codigo)
            .Select(u => new UnidadEjecutoraDto(u.Id, u.Codigo, u.Nombre))
            .ToListAsync(ct);
}
