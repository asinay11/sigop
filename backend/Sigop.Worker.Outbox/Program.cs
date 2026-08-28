using Microsoft.EntityFrameworkCore;
using Sigop.Aplicacion;
using Sigop.Infraestructura;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

builder.Services.Configure<OpcionesCifrado>(config.GetSection("Cifrado"));
builder.Services.Configure<OpcionesRabbitMq>(config.GetSection("RabbitMq"));
builder.Services.Configure<OpcionesOutbox>(config.GetSection("Outbox"));

builder.Services.AddDbContext<SigopDbContext>(opciones =>
    opciones.UseNpgsql(config.GetConnectionString("Sigop")));

builder.Services.AddSingleton<IProtectorDatos, ProtectorDatosAes>();
builder.Services.AddSingleton<ConexionRabbit>();

builder.Services.AddHostedService<DespachadorOutbox>();

var host = builder.Build();
host.Run();
