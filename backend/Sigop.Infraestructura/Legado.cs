using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Sigop.Infraestructura;

public sealed record ResultadoValidacion(bool EsValida, string Codigo, string Detalle);

public interface IServicioLegado
{
    Task<ResultadoValidacion> ValidarDisponibilidadAsync(
        string codigoEntidad, string codigoUnidad, string numeroSolicitud,
        decimal monto, string moneda, string correlationId, CancellationToken ct = default);
}

public sealed class OpcionesLegado
{
    public string UrlServicio { get; init; } = string.Empty;
    public int TiempoEsperaSegundos { get; init; } = 10;
}

public sealed class ClienteSoapLegado(HttpClient cliente, ILogger<ClienteSoapLegado> log) : IServicioLegado
{
    private const string NsSoap = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string NsLegado = "http://sicoin.minfin.gob.gt/presupuesto";

    public async Task<ResultadoValidacion> ValidarDisponibilidadAsync(
        string codigoEntidad, string codigoUnidad, string numeroSolicitud,
        decimal monto, string moneda, string correlationId, CancellationToken ct = default)
    {
        var sobre = ConstruirSobre(codigoEntidad, codigoUnidad, numeroSolicitud, monto, moneda, correlationId);

        using var contenido = new StringContent(sobre, Encoding.UTF8, "text/xml");
        contenido.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        using var peticion = new HttpRequestMessage(HttpMethod.Post, string.Empty) { Content = contenido };

        peticion.Headers.Add("SOAPAction", $"\"{NsLegado}/ValidarDisponibilidad\"");

        peticion.Headers.Add("X-Correlation-Id", correlationId);

        using var respuesta = await cliente.SendAsync(peticion, ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"El sistema legado respondio {(int)respuesta.StatusCode}.");
        }

        var resultado = Interpretar(cuerpo);

        log.LogInformation(
            "Validacion del legado para {Numero}: {Codigo}.", numeroSolicitud, resultado.Codigo);

        return resultado;
    }

    private static string ConstruirSobre(
        string codigoEntidad, string codigoUnidad, string numeroSolicitud,
        decimal monto, string moneda, string correlationId)
    {
        XNamespace soap = NsSoap;
        XNamespace pre = NsLegado;

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", NsSoap),
                new XAttribute(XNamespace.Xmlns + "pre", NsLegado),
                new XElement(soap + "Body",
                    new XElement(pre + "ValidarDisponibilidad",
                        new XElement(pre + "codigoEntidad", codigoEntidad),
                        new XElement(pre + "codigoUnidad", codigoUnidad),
                        new XElement(pre + "numeroSolicitud", numeroSolicitud),
                        new XElement(pre + "monto", monto.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(pre + "moneda", moneda),
                        new XElement(pre + "correlacion", correlationId)))))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static ResultadoValidacion Interpretar(string cuerpo)
    {
        XNamespace pre = NsLegado;
        var documento = XDocument.Parse(cuerpo);

        var resultado = documento.Descendants(pre + "ValidarDisponibilidadResult").FirstOrDefault()
            ?? throw new InvalidOperationException("La respuesta del legado no trae ValidarDisponibilidadResult.");

        var codigo = resultado.Element(pre + "codigo")?.Value ?? "DESCONOCIDO";
        var detalle = resultado.Element(pre + "detalle")?.Value ?? string.Empty;

        return new ResultadoValidacion(codigo == "OK", codigo, detalle);
    }
}

public sealed record ResultadoBanco(bool Confirmada, string Codigo, string Detalle);

public interface IServicioBanco
{
    Task<ResultadoBanco> ConfirmarAsync(
        string numeroSolicitud, decimal monto, string moneda,
        string claveIdempotencia, CancellationToken ct = default);
}

public sealed class OpcionesBanco
{
    public decimal MontoQueFalla { get; init; } = 500_000m;
}

public sealed class ServicioBancoSimulado(
    Microsoft.Extensions.Options.IOptions<OpcionesBanco> opciones,
    ILogger<ServicioBancoSimulado> log) : IServicioBanco
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ResultadoBanco> _emitidas = new(StringComparer.Ordinal);

    public Task<ResultadoBanco> ConfirmarAsync(
        string numeroSolicitud, decimal monto, string moneda,
        string claveIdempotencia, CancellationToken ct = default)
    {
        if (_emitidas.TryGetValue(claveIdempotencia, out var previa))
        {
            log.LogInformation("[BANCO SIMULADO] {Numero}: confirmacion ya emitida, no se ordena de nuevo.", numeroSolicitud);
            return Task.FromResult(previa);
        }

        ResultadoBanco resultado;

        if (monto >= opciones.Value.MontoQueFalla)
        {
            resultado = new ResultadoBanco(false, "TIMEOUT_BANCO",
                $"Tiempo de espera agotado al confirmar {monto:N2} {moneda} con el sistema bancario.");
        }
        else
        {
            var comprobante = $"CNF-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.CreateVersion7().ToString()[..8].ToUpperInvariant()}";
            resultado = new ResultadoBanco(true, comprobante, $"Operacion acreditada. Comprobante {comprobante}.");
            _emitidas[claveIdempotencia] = resultado;
        }

        log.LogInformation("[BANCO SIMULADO] {Numero} por {Monto}: {Codigo}.", numeroSolicitud, monto, resultado.Codigo);

        return Task.FromResult(resultado);
    }
}
