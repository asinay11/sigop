using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Sigop.Aplicacion;
using Sigop.Dominio;
using Sigop.Infraestructura;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.Configure<OpcionesJwt>(config.GetSection("Jwt"));
builder.Services.Configure<OpcionesCifrado>(config.GetSection("Cifrado"));

builder.Services.AddDbContext<SigopDbContext>(opciones =>
    opciones.UseNpgsql(config.GetConnectionString("Sigop")));

builder.Services.AddScoped<IRepositorioSolicitudes, RepositorioSolicitudes>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
builder.Services.AddScoped<IRepositorioCatalogos, RepositorioCatalogos>();
builder.Services.AddScoped<ServicioSolicitudes>();

builder.Services.AddHealthChecks().AddCheck<ComprobacionBaseDatos>("base-datos");

// Ventana global por IP, mas una estrecha para el login contra fuerza bruta.
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opciones.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));

    opciones.AddPolicy("login", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddSingleton<IProtectorDatos, ProtectorDatosAes>();
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<ServicioTokens>();

builder.Services.AddScoped<ContextoUsuario>();
builder.Services.AddScoped<IContextoUsuario>(s => s.GetRequiredService<ContextoUsuario>());

var jwt = config.GetSection("Jwt").Get<OpcionesJwt>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Emisor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.ClaveFirma)),

            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

var origenes = config.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];

builder.Services.AddCors(opciones => opciones.AddDefaultPolicy(politica => politica
    .WithOrigins(origenes)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("X-Correlation-Id")));

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(opciones =>
{
    opciones.SwaggerDoc("v1", new OpenApiInfo { Title = "SIGOP API", Version = "v1" });

    opciones.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });

    opciones.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", documento)] = [],
    });
});

var app = builder.Build();

app.UseExceptionHandler(manejador => manejador.Run(async contexto =>
{
    var excepcion = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;

    var (estado, codigo, mensaje) = excepcion switch
    {
        ExcepcionNoEncontrado e => (StatusCodes.Status404NotFound, "NO_ENCONTRADO", e.Message),
        ExcepcionAccesoDenegado e => (StatusCodes.Status403Forbidden, "ACCESO_DENEGADO", e.Message),

        ExcepcionReglaNegocio e when e.Codigo == "TRANSICION_INVALIDA"
            => (StatusCodes.Status409Conflict, e.Codigo, e.Message),

        ExcepcionReglaNegocio e => (StatusCodes.Status422UnprocessableEntity, e.Codigo, e.Message),

        _ => (StatusCodes.Status500InternalServerError, "ERROR_INTERNO",
              "Ocurrio un error inesperado. Cite el identificador de correlacion al reportarlo."),
    };

    if (estado == StatusCodes.Status500InternalServerError)
    {
        app.Logger.LogError(excepcion, "Error no controlado en {Ruta}.", contexto.Request.Path);
    }

    contexto.Response.StatusCode = estado;
    contexto.Response.ContentType = "application/problem+json";

    await contexto.Response.WriteAsJsonAsync(new
    {
        codigo,
        detalle = mensaje,
        correlationId = contexto.Response.Headers["X-Correlation-Id"].FirstOrDefault(),
    });
}));

app.Use(async (contexto, siguiente) =>
{
    var correlationId = contexto.Request.Headers["X-Correlation-Id"].FirstOrDefault();

    if (!Correlacion.EsValida(correlationId))
    {
        correlationId = Correlacion.Nueva();
    }

    contexto.RequestServices.GetRequiredService<ContextoUsuario>().EstablecerCorrelacion(correlationId!);
    contexto.Response.Headers["X-Correlation-Id"] = correlationId;

    await siguiente();
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();

app.Use(async (contexto, siguiente) =>
{
    if (contexto.User.Identity?.IsAuthenticated == true &&
        long.TryParse(contexto.User.FindFirstValue(ClaimsSigop.EntidadId), out var entidadId))
    {
        contexto.RequestServices.GetRequiredService<ContextoUsuario>().Establecer(
            contexto.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            entidadId);
    }

    await siguiente();
});

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

using (var alcance = app.Services.CreateScope())
{
    var contexto = alcance.ServiceProvider.GetRequiredService<SigopDbContext>();
    var hasher = alcance.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
    await SembradorUsuarios.SembrarAsync(contexto, hasher);
}

app.Run();

// Comprobacion de salud: verifica que la base responda, no solo que el proceso viva.
public sealed class ComprobacionBaseDatos(SigopDbContext contexto) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext contexto_, CancellationToken ct = default) =>
        await contexto.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy("La base de datos responde.")
            : HealthCheckResult.Unhealthy("La base de datos no responde.");
}
