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

public sealed class OpcionesRabbitMq
{
    public string Host { get; init; } = "localhost";
    public int Puerto { get; init; } = 5672;
    public string VirtualHost { get; init; } = "/";
    public string Usuario { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public ushort Prefetch { get; init; } = 10;

    public string NombreConexion { get; init; } = "sigop";
}

public sealed class OpcionesOutbox
{
    public int IntervaloSegundos { get; init; } = 2;
    public int TamanoLote { get; init; } = 50;
    public int MaximoIntentos { get; init; } = 5;
}

public sealed class ConexionRabbit(IOptions<OpcionesRabbitMq> opciones, ILogger<ConexionRabbit> log) : IAsyncDisposable
{
    private readonly OpcionesRabbitMq _opciones = opciones.Value;
    private readonly SemaphoreSlim _cerrojo = new(1, 1);

    private IConnection? _conexion;

    public async Task<IConnection> ObtenerAsync(CancellationToken ct = default)
    {
        if (_conexion is { IsOpen: true } viva)
        {
            return viva;
        }

        await _cerrojo.WaitAsync(ct);

        try
        {
            if (_conexion is { IsOpen: true } yaCreada)
            {
                return yaCreada;
            }

            var fabrica = new ConnectionFactory
            {
                HostName = _opciones.Host,
                Port = _opciones.Puerto,
                VirtualHost = _opciones.VirtualHost,
                UserName = _opciones.Usuario,
                Password = _opciones.Password,
                ClientProvidedName = _opciones.NombreConexion,
                AutomaticRecoveryEnabled = true,
            };

            _conexion = await fabrica.CreateConnectionAsync(ct);
            log.LogInformation("Conectado a RabbitMQ en {Host}:{Puerto}.", _opciones.Host, _opciones.Puerto);

            return _conexion;
        }
        finally
        {
            _cerrojo.Release();
        }
    }

    public async Task<IChannel> CrearCanalAsync(bool conConfirmaciones, CancellationToken ct = default)
    {
        var conexion = await ObtenerAsync(ct);

        return await conexion.CreateChannelAsync(
            new CreateChannelOptions(conConfirmaciones, conConfirmaciones), ct);
    }

    public async Task DeclararTopologiaAsync(IReadOnlyCollection<string> codigosEntidad, CancellationToken ct = default)
    {
        await using var canal = await CrearCanalAsync(conConfirmaciones: false, ct);

        await canal.ExchangeDeclareAsync(Topologia.ExchangeEventos, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(Topologia.ExchangeReintentos, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(Topologia.ExchangeDlx, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        foreach (var codigo in codigosEntidad)
        {
            await DeclararEntidadAsync(canal, codigo, ct);
        }

        log.LogInformation(
            "Topologia declarada para {Entidades} entidades: {Colas} colas en total.",
            codigosEntidad.Count,
            codigosEntidad.Count * 3);
    }

    private static async Task DeclararEntidadAsync(IChannel canal, string codigo, CancellationToken ct)
    {
        var cola = Topologia.Cola(codigo);
        var dlq = Topologia.Dlq(codigo);

        var argumentos = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = Topologia.ExchangeDlx,
            ["x-dead-letter-routing-key"] = dlq,
        };

        await canal.QueueDeclareAsync(cola, durable: true, exclusive: false, autoDelete: false, arguments: argumentos, cancellationToken: ct);
        await canal.QueueBindAsync(cola, Topologia.ExchangeEventos, Topologia.PatronEntidad(codigo), cancellationToken: ct);
        await canal.QueueBindAsync(cola, Topologia.ExchangeReintentos, cola, cancellationToken: ct);

        var espera = Topologia.ColaReintento(codigo);

        var argumentosEspera = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = Topologia.SegundosReintento * 1000,
            ["x-dead-letter-exchange"] = Topologia.ExchangeReintentos,
            ["x-dead-letter-routing-key"] = cola,
        };

        await canal.QueueDeclareAsync(espera, durable: true, exclusive: false, autoDelete: false, arguments: argumentosEspera, cancellationToken: ct);
        await canal.QueueBindAsync(espera, Topologia.ExchangeReintentos, espera, cancellationToken: ct);

        await canal.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await canal.QueueBindAsync(dlq, Topologia.ExchangeDlx, dlq, cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null)
        {
            await _conexion.DisposeAsync();
        }

        _cerrojo.Dispose();
    }
}

public sealed class DespachadorOutbox(
    IServiceScopeFactory fabricaAlcances,
    ConexionRabbit conexion,
    IOptions<OpcionesOutbox> opciones,
    ILogger<DespachadorOutbox> log) : BackgroundService
{
    private readonly OpcionesOutbox _opciones = opciones.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EsperarBrokerAsync(stoppingToken);

        await using var canal = await conexion.CrearCanalAsync(conConfirmaciones: true, stoppingToken);

        canal.BasicReturnAsync += (_, devuelto) =>
        {
            log.LogError(
                "Mensaje {MessageId} NO ENRUTABLE: {Motivo}. Exchange {Exchange}, clave {Clave}. Revise la topologia.",
                devuelto.BasicProperties.MessageId, devuelto.ReplyText, devuelto.Exchange, devuelto.RoutingKey);

            return Task.CompletedTask;
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var publicados = await DespacharLoteAsync(canal, stoppingToken);

                if (publicados < _opciones.TamanoLote)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_opciones.IntervaloSegundos), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Fallo un ciclo del despachador del outbox.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> DespacharLoteAsync(IChannel canal, CancellationToken ct)
    {
        using var alcance = fabricaAlcances.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SigopDbContext>();

        var pendientes = await contexto.Outbox
            .FromSql($"""
                SELECT * FROM outbox_mensajes
                WHERE estado = 'PENDIENTE'
                ORDER BY creado_en
                LIMIT {_opciones.TamanoLote}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        if (pendientes.Count == 0)
        {
            return 0;
        }

        var publicados = 0;

        foreach (var mensaje in pendientes)
        {
            try
            {
                await PublicarAsync(canal, mensaje, ct);
                mensaje.Estado = "PUBLICADO";
                mensaje.PublicadoEn = DateTimeOffset.UtcNow;
                publicados++;
            }
            catch (Exception ex)
            {
                mensaje.Intentos++;

                if (mensaje.Intentos >= _opciones.MaximoIntentos)
                {
                    mensaje.Estado = "FALLIDO";
                }

                log.LogError(ex, "No se pudo publicar el mensaje {Id} ({Tipo}).", mensaje.Id, mensaje.TipoMensaje);
            }
        }

        await contexto.SaveChangesAsync(ct);

        return publicados;
    }

    private static async Task PublicarAsync(IChannel canal, MensajeSaliente mensaje, CancellationToken ct)
    {
        var propiedades = new BasicProperties
        {
            MessageId = mensaje.Id.ToString(),
            CorrelationId = mensaje.CorrelationId,
            Type = mensaje.TipoMensaje,
            ContentType = "application/json",

            DeliveryMode = DeliveryModes.Persistent,

            Headers = new Dictionary<string, object?>
            {
                ["x-entidad-id"] = mensaje.EntidadId.ToString(),
            },
        };

        await canal.BasicPublishAsync(
            mensaje.Exchange, Topologia.RoutingKey(mensaje.EntidadId.ToString(), mensaje.RoutingKey), mandatory: true,
            basicProperties: propiedades,
            body: Encoding.UTF8.GetBytes(mensaje.Contenido),
            cancellationToken: ct);
    }

    private async Task<IReadOnlyCollection<string>> CodigosEntidadAsync(CancellationToken ct)
    {
        using var alcance = fabricaAlcances.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SigopDbContext>();

        return await contexto.Entidades.AsNoTracking().Select(e => e.Id.ToString()).ToListAsync(ct);
    }

    private async Task EsperarBrokerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await conexion.DeclararTopologiaAsync(await CodigosEntidadAsync(ct), ct);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "RabbitMQ no esta disponible todavia. Se reintenta en 5 s.");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}

