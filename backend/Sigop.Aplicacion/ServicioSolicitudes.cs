using Microsoft.Extensions.Logging;
using Sigop.Dominio;

namespace Sigop.Aplicacion;

public sealed class ServicioSolicitudes(
    IRepositorioSolicitudes repositorio,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario usuario,
    IProtectorDatos protector,
    ILogger<ServicioSolicitudes> log)
{
    public async Task<ResultadoRegistroDto> RegistrarAsync(RegistrarSolicitudDto dto, CancellationToken ct = default)
    {
        var unidadEjecutoraId = ResolverUnidadEjecutora(dto.UnidadEjecutoraId);
        var numero = await repositorio.SiguienteNumeroAsync(ct);

        var solicitud = Solicitud.Registrar(
            numero, usuario.EntidadId, unidadEjecutoraId, dto.TipoOperacion,
            dto.Monto, dto.Moneda, dto.Concepto,
            dto.BeneficiarioNombre, dto.BeneficiarioNit, dto.CuentaBancaria,
            usuario.Usuario, usuario.CorrelationId);

        repositorio.Agregar(solicitud);

        await unidadDeTrabajo.ConfirmarAsync(ct);

        log.LogInformation("Solicitud {Numero} registrada por {Usuario}.", solicitud.Numero, usuario.Usuario);

        return new ResultadoRegistroDto(
            solicitud.Id, solicitud.Numero, solicitud.Estado.ToString(),
            solicitud.TipoOperacion.EsSincrona(), usuario.CorrelationId);
    }

    public async Task<PaginaDto<SolicitudResumenDto>> BuscarAsync(
        EstadoSolicitud? estado, string? numero, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        var filtro = new FiltroSolicitudes(
            usuario.EntidadId,
            estado,
            numero,
            pagina < 1 ? 1 : pagina,
            tamanoPagina is < 1 or > 100 ? 20 : tamanoPagina);

        var (elementos, total) = await repositorio.BuscarAsync(filtro, ct);

        return new PaginaDto<SolicitudResumenDto>(
            [.. elementos.Select(AResumen)], filtro.Pagina, filtro.TamanoPagina, total);
    }

    public async Task<SolicitudDetalleDto> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        ADetalle(await CargarAsync(id, ct));

    public async Task<SolicitudDetalleDto> ActualizarAsync(Guid id, ActualizarSolicitudDto dto, CancellationToken ct = default)
    {
        var solicitud = await CargarAsync(id, ct);

        solicitud.ActualizarDatos(
            dto.Monto, dto.Moneda, dto.Concepto,
            dto.BeneficiarioNombre, dto.BeneficiarioNit, dto.CuentaBancaria,
            usuario.Usuario, "Actualizacion de datos de la solicitud.", usuario.CorrelationId);

        await unidadDeTrabajo.ConfirmarAsync(ct);

        log.LogInformation("Solicitud {Numero} actualizada por {Usuario}.", solicitud.Numero, usuario.Usuario);

        return ADetalle(solicitud);
    }

    public async Task<SolicitudDetalleDto> CambiarEstadoAsync(Guid id, CambiarEstadoDto dto, CancellationToken ct = default)
    {
        var solicitud = await CargarAsync(id, ct);
        var estadoAnterior = solicitud.Estado;

        switch (dto.EstadoDestino)
        {
            case EstadoSolicitud.Anulada:
                solicitud.Anular(dto.Motivo, usuario.Usuario, usuario.CorrelationId);
                break;

            case EstadoSolicitud.EnValidacion:
                solicitud.IniciarValidacion(dto.Motivo, usuario.Usuario, usuario.CorrelationId);
                break;

            case EstadoSolicitud.Rechazada:
                solicitud.Rechazar(dto.Motivo, usuario.Usuario, usuario.CorrelationId);
                break;

            default:
                throw new ExcepcionAccesoDenegado(
                    $"La transicion a '{dto.EstadoDestino}' la produce el flujo automatico y no admite intervencion manual.");
        }

        await unidadDeTrabajo.ConfirmarAsync(ct);

        log.LogInformation(
            "Solicitud {Numero}: {Anterior} -> {Nuevo} por {Usuario}.",
            solicitud.Numero, estadoAnterior, solicitud.Estado, usuario.Usuario);

        return ADetalle(solicitud);
    }

    public async Task<SolicitudDetalleDto> ReprocesarAsync(Guid id, ReprocesarDto dto, CancellationToken ct = default)
    {
        var solicitud = await CargarAsync(id, ct);

        solicitud.Reprocesar(dto.Motivo, usuario.Usuario, usuario.CorrelationId);

        await unidadDeTrabajo.ConfirmarAsync(ct);

        log.LogWarning(
            "Reproceso {Intento} de {Maximo} sobre {Numero}, ordenado por {Usuario}.",
            solicitud.IntentosReproceso, Solicitud.MaximoReprocesos, solicitud.Numero, usuario.Usuario);

        return ADetalle(solicitud);
    }

    private async Task<Solicitud> CargarAsync(Guid id, CancellationToken ct) =>
        await repositorio.ObtenerAsync(id, usuario.EntidadId, ct)
            ?? throw new ExcepcionNoEncontrado($"No se encontro la solicitud '{id}'.");

    private static long ResolverUnidadEjecutora(long? solicitada) =>
        solicitada is { } elegida && elegida > 0
            ? elegida
            : throw new ExcepcionAccesoDenegado("Debe indicar la unidad ejecutora de la solicitud.");

    private static SolicitudResumenDto AResumen(Solicitud s) => new(
        s.Id, s.Numero, s.TipoOperacion.ToString(), s.Monto, s.Moneda,
        s.Concepto, s.Estado.ToString(), s.CreadoEn, s.CreadoPor);

    private SolicitudDetalleDto ADetalle(Solicitud s) => new(
        s.Id, s.Numero, s.EntidadId, s.UnidadEjecutoraId,
        s.TipoOperacion.ToString(), s.TipoOperacion.EsSincrona(),
        s.Monto, s.Moneda, s.Concepto,
        s.BeneficiarioNombre,
        protector.Enmascarar(s.BeneficiarioNit),
        protector.Enmascarar(s.CuentaBancaria),
        s.Estado.ToString(),
        [.. MaquinaEstados.Disponibles(s.Estado).Select(e => e.ToString())],
        s.IntentosReproceso, Solicitud.MaximoReprocesos, s.UltimoError,
        s.CorrelationId, s.CreadoEn, s.CreadoPor,
        [.. s.Historial.OrderByDescending(h => h.OcurridoEn).Select(h => new CambioEstadoDto(
            h.EstadoAnterior?.ToString(), h.EstadoNuevo.ToString(), h.Motivo,
            h.Origen.ToString(), h.RealizadoPor, h.CorrelationId, h.OcurridoEn))]);
}
