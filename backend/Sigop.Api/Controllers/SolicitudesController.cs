using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigop.Aplicacion;
using Sigop.Dominio;

namespace Sigop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/solicitudes")]
public sealed class SolicitudesController(ServicioSolicitudes servicio) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Registrar(RegistrarSolicitudDto dto, CancellationToken ct)
    {
        var resultado = await servicio.RegistrarAsync(dto, ct);

        return resultado.EsSincrona
            ? CreatedAtAction(nameof(Obtener), new { id = resultado.Id }, resultado)
            : Accepted($"/api/solicitudes/{resultado.Id}", resultado);
    }

    [HttpGet]
    public async Task<ActionResult<PaginaDto<SolicitudResumenDto>>> Buscar(
        [FromQuery] EstadoSolicitud? estado,
        [FromQuery] string? numero,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20,
        CancellationToken ct = default) =>
        Ok(await servicio.BuscarAsync(estado, numero, pagina, tamanoPagina, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SolicitudDetalleDto>> Obtener(Guid id, CancellationToken ct) =>
        Ok(await servicio.ObtenerAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SolicitudDetalleDto>> Actualizar(
        Guid id, ActualizarSolicitudDto dto, CancellationToken ct) =>
        Ok(await servicio.ActualizarAsync(id, dto, ct));

    [HttpPost("{id:guid}/estado")]
    public async Task<ActionResult<SolicitudDetalleDto>> CambiarEstado(
        Guid id, CambiarEstadoDto dto, CancellationToken ct) =>
        Ok(await servicio.CambiarEstadoAsync(id, dto, ct));

    [HttpPost("{id:guid}/reproceso")]
    public async Task<ActionResult<SolicitudDetalleDto>> Reprocesar(
        Guid id, ReprocesarDto dto, CancellationToken ct) =>
        Ok(await servicio.ReprocesarAsync(id, dto, ct));
}
