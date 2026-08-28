using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sigop.Infraestructura;

namespace Sigop.Api.Controllers;

public sealed record LoginDto
{
    [Required]
    public string Usuario { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("login")]
public sealed class AuthController(ServicioTokens tokens) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken ct)
    {
        var resultado = await tokens.AutenticarAsync(dto.Usuario, dto.Password, ct);

        return resultado is null
            ? Unauthorized(new { codigo = "CREDENCIALES_INVALIDAS", detalle = "Usuario o contrasena incorrectos." })
            : Ok(resultado);
    }
}
