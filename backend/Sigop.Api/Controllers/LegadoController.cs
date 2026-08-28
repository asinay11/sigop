using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigop.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("legado/presupuesto")]
public sealed class LegadoController(ILogger<LegadoController> log) : ControllerBase
{
    private const string Ns = "http://sicoin.minfin.gob.gt/presupuesto";

    private const decimal TechoDisponible = 1_000_000m;

    [HttpPost]
    [Consumes("text/xml", "application/soap+xml")]
    [Produces("text/xml")]
    public async Task<IActionResult> ValidarDisponibilidad()
    {
        XNamespace ns = Ns;

        using var lector = new StreamReader(Request.Body);
        var peticion = XDocument.Parse(await lector.ReadToEndAsync());

        var numero = peticion.Descendants(ns + "numeroSolicitud").FirstOrDefault()?.Value ?? "?";
        var textoMonto = peticion.Descendants(ns + "monto").FirstOrDefault()?.Value ?? "0";
        var correlacion = peticion.Descendants(ns + "correlacion").FirstOrDefault()?.Value ?? "?";

        var monto = decimal.Parse(textoMonto, CultureInfo.InvariantCulture);

        var (codigo, detalle) = monto > TechoDisponible
            ? ("SIN_DISPONIBILIDAD", $"El monto solicitado excede el saldo disponible de {TechoDisponible:N2}.")
            : ("OK", $"Disponibilidad confirmada. Saldo remanente {TechoDisponible - monto:N2}.");

        log.LogInformation(
            "[LEGADO SIMULADO] {Numero} por {Monto}: {Codigo}. Correlacion {Correlacion}.",
            numero, monto, codigo, correlacion);

        var respuesta = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/") + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap", "http://schemas.xmlsoap.org/soap/envelope/"),
                new XElement(XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/") + "Body",
                    new XElement(ns + "ValidarDisponibilidadResponse",
                        new XElement(ns + "ValidarDisponibilidadResult",
                            new XElement(ns + "codigo", codigo),
                            new XElement(ns + "detalle", detalle))))));

        return Content(respuesta.ToString(SaveOptions.DisableFormatting), "text/xml");
    }
}
