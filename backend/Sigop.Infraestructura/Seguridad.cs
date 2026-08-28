using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Sigop.Aplicacion;

namespace Sigop.Infraestructura;

public sealed class OpcionesJwt
{
    public string Emisor { get; init; } = "sigop-api";
    public string Audiencia { get; init; } = "sigop-portal";
    public string ClaveFirma { get; init; } = string.Empty;
    public int MinutosVigencia { get; init; } = 480;
}

public sealed class OpcionesCifrado
{
    public string Clave { get; init; } = string.Empty;
}

public static class Correlacion
{
    private const string Alfabeto = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public static string Nueva()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);
        var caracteres = new char[14];
        var destino = 0;

        for (var i = 0; i < 12; i++)
        {
            if (i == 4 || i == 8)
            {
                caracteres[destino++] = (char)45;
            }

            caracteres[destino++] = Alfabeto[bytes[i] % Alfabeto.Length];
        }

        return new string(caracteres);
    }

    public static bool EsValida(string? valor) =>
        !string.IsNullOrWhiteSpace(valor)
        && valor.Length <= 50
        && valor.All(c => char.IsAsciiLetterOrDigit(c) || c == (char)45);
}

public static class ClaimsSigop
{
    public const string EntidadId = "entidad";
}

public sealed class ProtectorDatosAes : IProtectorDatos
{
    private const string Prefijo = "v1:";
    private const int LongitudNonce = 12;
    private const int LongitudEtiqueta = 16;

    private readonly byte[] _clave;

    public ProtectorDatosAes(IOptions<OpcionesCifrado> opciones)
    {
        _clave = Convert.FromBase64String(opciones.Value.Clave);

        if (_clave.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException($"La clave de cifrado mide {_clave.Length} bytes. AES admite 16, 24 o 32.");
        }
    }

    public string? Cifrar(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || valor.StartsWith(Prefijo, StringComparison.Ordinal))
        {
            return valor;
        }

        var enClaro = Encoding.UTF8.GetBytes(valor);
        var nonce = RandomNumberGenerator.GetBytes(LongitudNonce);
        var cifrado = new byte[enClaro.Length];
        var etiqueta = new byte[LongitudEtiqueta];

        using (var aes = new AesGcm(_clave, LongitudEtiqueta))
        {
            aes.Encrypt(nonce, enClaro, cifrado, etiqueta);
        }

        return Prefijo + Convert.ToBase64String([.. nonce, .. etiqueta, .. cifrado]);
    }

    public string? Descifrar(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || !valor.StartsWith(Prefijo, StringComparison.Ordinal))
        {
            return valor;
        }

        var bruto = Convert.FromBase64String(valor[Prefijo.Length..]);
        var nonce = bruto.AsSpan(0, LongitudNonce);
        var etiqueta = bruto.AsSpan(LongitudNonce, LongitudEtiqueta);
        var cifrado = bruto.AsSpan(LongitudNonce + LongitudEtiqueta);
        var enClaro = new byte[cifrado.Length];

        using (var aes = new AesGcm(_clave, LongitudEtiqueta))
        {
            aes.Decrypt(nonce, cifrado, etiqueta, enClaro);
        }

        return Encoding.UTF8.GetString(enClaro);
    }

    public string? Enmascarar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return valor;
        }

        var visible = valor.Length <= 4 ? valor : valor[^4..];
        return new string('*', Math.Max(valor.Length - visible.Length, 0)) + visible;
    }
}

public sealed class ContextoUsuario : IContextoUsuario
{
    public string Usuario { get; private set; } = string.Empty;
    public long EntidadId { get; private set; }
    public string CorrelationId { get; private set; } = Correlacion.Nueva();

    public void Establecer(string usuario, long entidadId)
    {
        Usuario = usuario;
        EntidadId = entidadId;
    }

    public void EstablecerCorrelacion(string correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            CorrelationId = correlationId;
        }
    }
}

public sealed record ResultadoLogin(string Token, DateTimeOffset Expira, string Usuario, string Nombre, string Entidad);

public sealed class ServicioTokens(
    SigopDbContext contexto,
    IPasswordHasher<Usuario> hasher,
    IOptions<OpcionesJwt> opciones)
{
    private const int IntentosAntesDeBloquear = 5;
    private const int MinutosBloqueo = 15;

    private readonly OpcionesJwt _jwt = opciones.Value;

    public async Task<ResultadoLogin?> AutenticarAsync(string usuario, string password, CancellationToken ct = default)
    {
        var normalizado = usuario.Trim().ToLowerInvariant();
        var registro = await contexto.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == normalizado, ct);

        if (registro is null || registro.Estado != "ACTIVO")
        {
            return null;
        }

        var ahora = DateTimeOffset.UtcNow;

        if (registro.BloqueadoHasta is { } hasta && hasta > ahora)
        {
            return null;
        }

        if (hasher.VerifyHashedPassword(registro, registro.HashPassword, password) == PasswordVerificationResult.Failed)
        {
            registro.IntentosFallidos++;

            if (registro.IntentosFallidos >= IntentosAntesDeBloquear)
            {
                registro.BloqueadoHasta = ahora.AddMinutes(MinutosBloqueo);
            }

            await contexto.SaveChangesAsync(ct);
            return null;
        }

        registro.IntentosFallidos = 0;
        registro.BloqueadoHasta = null;
        registro.UltimoAcceso = ahora;
        await contexto.SaveChangesAsync(ct);

        var entidad = await contexto.Entidades
            .Where(e => e.Id == registro.EntidadId)
            .Select(e => e.Nombre)
            .FirstAsync(ct);

        return EmitirToken(registro, entidad, ahora);
    }

    private ResultadoLogin EmitirToken(Usuario usuario, string entidad, DateTimeOffset ahora)
    {
        var expira = ahora.AddMinutes(_jwt.MinutosVigencia);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, usuario.NombreUsuario),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),

            new(ClaimsSigop.EntidadId, usuario.EntidadId.ToString()),
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Emisor,
            Audience = _jwt.Audiencia,
            Subject = new ClaimsIdentity(claims),
            Expires = expira.UtcDateTime,
            IssuedAt = ahora.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(_jwt.ClaveFirma)),
                SecurityAlgorithms.HmacSha256),
        };

        return new ResultadoLogin(
            new JsonWebTokenHandler().CreateToken(descriptor),
            expira,
            usuario.NombreUsuario,
            usuario.Nombre,
            entidad);
    }
}

public static class SembradorUsuarios
{
    public const string PasswordDemo = "12345";

    private const long EntidadMinfin = 11130007;
    private const long EntidadMinGob = 11130008;

    public static async Task SembrarAsync(SigopDbContext contexto, IPasswordHasher<Usuario> hasher, CancellationToken ct = default)
    {
        if (await contexto.Usuarios.AnyAsync(ct))
        {
            return;
        }

        var usuarios = new[]
        {
            Crear("jperez", "Juan Perez", EntidadMinfin),
            Crear("rmorales", "Rosa Morales", EntidadMinGob),
        };

        foreach (var u in usuarios)
        {
            u.HashPassword = hasher.HashPassword(u, PasswordDemo);
        }

        contexto.Usuarios.AddRange(usuarios);
        await contexto.SaveChangesAsync(ct);
    }

    private static Usuario Crear(string usuario, string nombre, long entidadId) => new()
    {
        NombreUsuario = usuario,
        Nombre = nombre,
        EntidadId = entidadId,
        Estado = "ACTIVO",
    };
}
