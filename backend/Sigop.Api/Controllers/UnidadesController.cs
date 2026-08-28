using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigop.Aplicacion;

namespace Sigop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/unidades")]
public sealed class UnidadesController(IRepositorioCatalogos catalogos, IContextoUsuario usuario) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UnidadEjecutoraDto>>> Listar(CancellationToken ct) =>
        Ok(await catalogos.UnidadesAsync(usuario.EntidadId, ct));
}
