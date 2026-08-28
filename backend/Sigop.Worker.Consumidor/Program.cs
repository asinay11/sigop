using Microsoft.EntityFrameworkCore;
using Sigop.Aplicacion;
using Sigop.Infraestructura;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

builder.Services.Configure<OpcionesCifrado>(config.GetSection("Cifrado"));
builder.Services.Configure<OpcionesRabbitMq>(config.GetSection("RabbitMq"));
builder.Services.Configure<OpcionesLegado>(config.GetSection("Legado"));
builder.Services.Configure<OpcionesBanco>(config.GetSection("Banco"));

builder.Services.AddDbContext<SigopDbContext>(opciones =>
    opciones.UseNpgsql(config.GetConnectionString("Sigop")));

builder.Services.AddScoped<IRepositorioSolicitudes, RepositorioSolicitudes>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
builder.Services.AddSingleton<IProtectorDatos, ProtectorDatosAes>();

builder.Services.AddScoped<ContextoUsuario>();
builder.Services.AddScoped<IContextoUsuario>(s => s.GetRequiredService<ContextoUsuario>());

var opcionesLegado = config.GetSection("Legado").Get<OpcionesLegado>() ?? new OpcionesLegado();

builder.Services.AddHttpClient<IServicioLegado, ClienteSoapLegado>(cliente =>
{
    cliente.BaseAddress = new Uri(opcionesLegado.UrlServicio);
    cliente.Timeout = TimeSpan.FromSeconds(opcionesLegado.TiempoEsperaSegundos);
});

builder.Services.AddSingleton<IServicioBanco, ServicioBancoSimulado>();
builder.Services.AddSingleton<ConexionRabbit>();

builder.Services.AddHostedService<ConsumidorValidacion>();

var host = builder.Build();
host.Run();
