using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sigop.Aplicacion;
using Sigop.Dominio;

namespace Sigop.Infraestructura;

public sealed class ConsumidorValidacion(
    IServiceScopeFactory fabricaAlcances,
    ConexionRabbit conexion,
    IOptions<OpcionesRabbitMq> opciones,
    ILogger<ConsumidorValidacion> log) : BackgroundService
{
    private const string Actor = "orquestador";

    private IChannel? _canal;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && _canal is null)
        {
            try
            {
                var codigos = await CodigosEntidadAsync(stoppingToken);

                await conexion.DeclararTopologiaAsync(codigos, stoppingToken);

                _canal = await conexion.CrearCanalAsync(conConfirmaciones: false, stoppingToken);

                await _canal.BasicQosAsync(0, opciones.Value.Prefetch, global: false, stoppingToken);

                foreach (var codigo in codigos)
                {
                    var deEsteCodigo = codigo;
                    var consumidor = new AsyncEventingBasicConsumer(_canal);

                    consumidor.ReceivedAsync += (_, entrega) => RecibirAsync(deEsteCodigo, entrega, stoppingToken);

                    await _canal.BasicConsumeAsync(
                        Topologia.Cola(codigo), autoAck: false, consumidor, stoppingToken);
                }

                log.LogInformation("Consumidor escuchando {Colas} colas de entidad.", codigos.Count);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogWarning(ex, "No se pudo iniciar el consumidor. Se reintenta en 5 s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task<IReadOnlyCollection<string>> CodigosEntidadAsync(CancellationToken ct)
    {
        using var alcance = fabricaAlcances.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SigopDbContext>();

        return await contexto.Entidades.AsNoTracking().Select(e => e.Id.ToString()).ToListAsync(ct);
    }

    private async Task RecibirAsync(string codigoEntidad, BasicDeliverEventArgs entrega, CancellationToken ct)
    {
        var canal = _canal!;
        var messageId = Guid.TryParse(entrega.BasicProperties.MessageId, out var id) ? id : Guid.CreateVersion7();
        var correlationId = entrega.BasicProperties.CorrelationId ?? messageId.ToString();
        var cuerpo = Encoding.UTF8.GetString(entrega.Body.Span);

        try
        {
            using var alcance = fabricaAlcances.CreateScope();
            var servicios = alcance.ServiceProvider;
            var contexto = servicios.GetRequiredService<SigopDbContext>();

            servicios.GetRequiredService<ContextoUsuario>().EstablecerCorrelacion(correlationId);

            if (await contexto.MensajesProcesados.AnyAsync(m => m.MessageId == messageId, ct))
            {
                log.LogDebug("Mensaje {MessageId} ya procesado. Se descarta.", messageId);
                await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
                return;
            }

            contexto.MensajesProcesados.Add(new MensajeProcesado
            {
                MessageId = messageId,
                ProcesadoEn = DateTimeOffset.UtcNow,
            });

            await AvanzarAsync(servicios, cuerpo, correlationId, ct);

            await servicios.GetRequiredService<IUnidadDeTrabajo>().ConfirmarAsync(ct);

            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex)
        {
            await ReprogramarAsync(canal, codigoEntidad, entrega, messageId, cuerpo, correlationId, ex, ct);
            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
        }
    }

    private async Task ReprogramarAsync(
        IChannel canal, string codigoEntidad, BasicDeliverEventArgs entrega,
        Guid messageId, string cuerpo, string correlationId, Exception ex, CancellationToken ct)
    {
        var intentos = LeerIntentos(entrega) + 1;
        var propiedades = ClonarPropiedades(entrega, intentos, ex.Message);

        if (intentos > Topologia.MaximoReintentos)
        {
            log.LogError(ex, "Mensaje {MessageId} agoto los {Maximo} reintentos. Se marca la solicitud como fallida.",
                messageId, Topologia.MaximoReintentos);

            await MarcarFallidaAsync(cuerpo, correlationId, ex.Message, ct);

            await canal.BasicPublishAsync(
                Topologia.ExchangeDlx, Topologia.Dlq(codigoEntidad), mandatory: false,
                basicProperties: propiedades, body: entrega.Body.ToArray(), cancellationToken: ct);

            return;
        }

        log.LogWarning(ex, "Fallo el mensaje {MessageId} de {Entidad}. Reintento {Intento} de {Maximo} en {Segundos} s.",
            messageId, codigoEntidad, intentos, Topologia.MaximoReintentos, Topologia.SegundosReintento);

        await canal.BasicPublishAsync(
            Topologia.ExchangeReintentos, Topologia.ColaReintento(codigoEntidad), mandatory: true,
            basicProperties: propiedades, body: entrega.Body.ToArray(), cancellationToken: ct);
    }

    private async Task MarcarFallidaAsync(string cuerpo, string correlationId, string error, CancellationToken ct)
    {
        try
        {
            using var documento = JsonDocument.Parse(cuerpo);

            if (!documento.RootElement.TryGetProperty("solicitudId", out var nodo) ||
                !nodo.TryGetGuid(out var solicitudId))
            {
                return;
            }

            using var alcance = fabricaAlcances.CreateScope();
            var servicios = alcance.ServiceProvider;

            var solicitud = await servicios.GetRequiredService<IRepositorioSolicitudes>()
                .ObtenerParaProcesoAsync(solicitudId, ct);

            if (solicitud is null || !MaquinaEstados.EsValida(solicitud.Estado, EstadoSolicitud.Fallida))
            {
                return;
            }

            var contexto = servicios.GetRequiredService<ContextoUsuario>();
            contexto.EstablecerCorrelacion(correlationId);
            contexto.Establecer(Actor, solicitud.EntidadId);

            solicitud.MarcarFallida(
                $"Agotados {Topologia.MaximoReintentos} reintentos. {error}", Actor, correlationId);

            await servicios.GetRequiredService<IUnidadDeTrabajo>().ConfirmarAsync(ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "No se pudo marcar la solicitud como fallida tras agotar los reintentos.");
        }
    }

    private async Task AvanzarAsync(
        IServiceProvider servicios, string cuerpo, string correlationId, CancellationToken ct)
    {
        using var documento = JsonDocument.Parse(cuerpo);

        if (!documento.RootElement.TryGetProperty("solicitudId", out var nodo) ||
            !nodo.TryGetGuid(out var solicitudId))
        {
            return;
        }

        var repositorio = servicios.GetRequiredService<IRepositorioSolicitudes>();
        var solicitud = await repositorio.ObtenerParaProcesoAsync(solicitudId, ct);

        if (solicitud is null)
        {
            return;
        }

        servicios.GetRequiredService<ContextoUsuario>()
            .Establecer(Actor, solicitud.EntidadId);

        switch (solicitud.Estado)
        {
            case EstadoSolicitud.Registrada:
                solicitud.IniciarValidacion("Inicio de validacion contra el sistema legado.", Actor, correlationId);
                await ValidarAsync(servicios, solicitud, correlationId, ct);
                break;

            case EstadoSolicitud.EnValidacion:
                await ValidarAsync(servicios, solicitud, correlationId, ct);
                break;

            case EstadoSolicitud.Validada:
                solicitud.IniciarEjecucion("Instruccion enviada al sistema bancario.", Actor, correlationId);
                await ConfirmarAsync(servicios, solicitud, correlationId, ct);
                break;

            case EstadoSolicitud.EnProceso:
                await ConfirmarAsync(servicios, solicitud, correlationId, ct);
                break;

            default:
                log.LogInformation("La solicitud {Numero} esta en {Estado}; no hay paso siguiente.",
                    solicitud.Numero, solicitud.Estado);
                break;
        }
    }

    private static async Task ValidarAsync(
        IServiceProvider servicios, Solicitud solicitud, string correlationId, CancellationToken ct)
    {
        var contexto = servicios.GetRequiredService<SigopDbContext>();

        var codigoEntidad = solicitud.EntidadId.ToString();

        var codigoUnidad = await contexto.UnidadesEjecutoras
            .Where(u => u.Id == solicitud.UnidadEjecutoraId).Select(u => u.Codigo).FirstAsync(ct);

        var legado = servicios.GetRequiredService<IServicioLegado>();

        var resultado = await legado.ValidarDisponibilidadAsync(
            codigoEntidad, codigoUnidad, solicitud.Numero,
            solicitud.Monto, solicitud.Moneda, correlationId, ct);

        if (resultado.EsValida)
        {
            solicitud.MarcarValidada($"{resultado.Codigo}: {resultado.Detalle}", Actor, correlationId);
        }
        else
        {
            solicitud.Rechazar($"{resultado.Codigo}: {resultado.Detalle}", Actor, correlationId);
        }
    }

    private static async Task ConfirmarAsync(
        IServiceProvider servicios, Solicitud solicitud, string correlationId, CancellationToken ct)
    {
        var banco = servicios.GetRequiredService<IServicioBanco>();

        var resultado = await banco.ConfirmarAsync(
            solicitud.Numero, solicitud.Monto, solicitud.Moneda,
            $"{solicitud.Id}:{solicitud.IntentosReproceso}", ct);

        if (resultado.Confirmada)
        {
            solicitud.MarcarEjecutada($"{resultado.Codigo}: {resultado.Detalle}", Actor, correlationId);
        }
        else
        {
            solicitud.MarcarFallida($"{resultado.Codigo}: {resultado.Detalle}", Actor, correlationId);
        }
    }

    private static int LeerIntentos(BasicDeliverEventArgs entrega)
    {
        if (entrega.BasicProperties.Headers is { } cabeceras &&
            cabeceras.TryGetValue(Topologia.CabeceraIntentos, out var valor) && valor is not null)
        {
            var texto = valor switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string s => s,
                _ => valor.ToString(),
            };

            if (int.TryParse(texto, out var intentos))
            {
                return intentos;
            }
        }

        return 0;
    }

    private static BasicProperties ClonarPropiedades(BasicDeliverEventArgs entrega, int intentos, string error)
    {
        var cabeceras = new Dictionary<string, object?>();

        if (entrega.BasicProperties.Headers is { } originales)
        {
            foreach (var (clave, valor) in originales)
            {
                cabeceras[clave] = valor;
            }
        }

        cabeceras[Topologia.CabeceraIntentos] = intentos.ToString(CultureInfo.InvariantCulture);
        cabeceras[Topologia.CabeceraUltimoError] = error.Length > 400 ? error[..400] : error;

        return new BasicProperties
        {
            MessageId = entrega.BasicProperties.MessageId,
            CorrelationId = entrega.BasicProperties.CorrelationId,
            ContentType = entrega.BasicProperties.ContentType,
            Type = entrega.BasicProperties.Type,
            DeliveryMode = DeliveryModes.Persistent,
            Headers = cabeceras,
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_canal is not null)
        {
            await _canal.DisposeAsync();
            _canal = null;
        }

        await base.StopAsync(cancellationToken);
    }
}
