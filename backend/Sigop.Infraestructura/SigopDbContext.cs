using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sigop.Aplicacion;
using Sigop.Dominio;

namespace Sigop.Infraestructura;

public sealed class Entidad
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public sealed class UnidadEjecutora
{
    public long Id { get; set; }
    public long EntidadId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public sealed class Usuario
{
    public long Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string HashPassword { get; set; } = string.Empty;
    public long EntidadId { get; set; }
    public string Estado { get; set; } = "ACTIVO";
    public short IntentosFallidos { get; set; }
    public DateTimeOffset? BloqueadoHasta { get; set; }
    public DateTimeOffset? UltimoAcceso { get; set; }
}

public sealed class MensajeSaliente
{
    public Guid Id { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public string TipoMensaje { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public long EntidadId { get; set; }
    public string? SolicitudNumero { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Estado { get; set; } = "PENDIENTE";
    public short Intentos { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? PublicadoEn { get; set; }
}

public sealed class MensajeProcesado
{
    public Guid MessageId { get; set; }
    public DateTimeOffset ProcesadoEn { get; set; }
}
public sealed class SigopDbContext(DbContextOptions<SigopDbContext> opciones, IProtectorDatos protector)
    : DbContext(opciones)
{
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<CambioEstado> HistorialEstados => Set<CambioEstado>();
    public DbSet<Entidad> Entidades => Set<Entidad>();
    public DbSet<UnidadEjecutora> UnidadesEjecutoras => Set<UnidadEjecutora>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<MensajeSaliente> Outbox => Set<MensajeSaliente>();
    public DbSet<MensajeProcesado> MensajesProcesados => Set<MensajeProcesado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        var cifrado = new ValueConverter<string?, string?>(
            v => protector.Cifrar(v),
            v => protector.Descifrar(v));

        modelBuilder.Entity<Solicitud>(e =>
        {
            e.ToTable("solicitudes");
            e.HasKey(s => s.Id);
            e.Property(s => s.Estado).HasConversion(ConversorEnum<EstadoSolicitud>());
            e.Property(s => s.TipoOperacion).HasConversion(ConversorEnum<TipoOperacion>());
            e.Property(s => s.BeneficiarioNit).HasConversion(cifrado);
            e.Property(s => s.CuentaBancaria).HasConversion(cifrado);

            e.Metadata.FindNavigation(nameof(Solicitud.Historial))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(s => s.Historial).WithOne().HasForeignKey(h => h.SolicitudId);

            e.Ignore(s => s.Eventos);
        });

        modelBuilder.Entity<CambioEstado>(e =>
        {
            e.ToTable("historial_estados");
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).ValueGeneratedOnAdd();
            e.Property(h => h.EstadoAnterior).HasConversion(ConversorEnumNullable<EstadoSolicitud>());
            e.Property(h => h.EstadoNuevo).HasConversion(ConversorEnum<EstadoSolicitud>());
            e.Property(h => h.Origen).HasConversion(ConversorEnum<OrigenCambio>());
        });

        modelBuilder.Entity<Entidad>(e => { e.ToTable("entidades"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<UnidadEjecutora>(e => { e.ToTable("unidades_ejecutoras"); e.HasKey(x => x.Id); });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.Id);
            e.Property(x => x.NombreUsuario).HasColumnName("usuario");
        });

        modelBuilder.Entity<MensajeSaliente>(e =>
        {
            e.ToTable("outbox_mensajes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Contenido).HasColumnType("jsonb");
        });

        modelBuilder.Entity<MensajeProcesado>(e =>
        {
            e.ToTable("mensajes_procesados");
            e.HasKey(x => x.MessageId);
        });

        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var prop in tipo.GetProperties().Where(p => p.GetColumnName() == p.Name))
            {
                prop.SetColumnName(ASnake(prop.Name).ToLowerInvariant());
            }
        }
    }

    private static ValueConverter<T, string> ConversorEnum<T>() where T : struct, Enum =>
        new(v => ASnake(v.ToString()!), v => Enum.Parse<T>(DeSnake(v)));

    private static ValueConverter<T?, string?> ConversorEnumNullable<T>() where T : struct, Enum =>
        new(v => v.HasValue ? ASnake(v.Value.ToString()!) : null,
            v => v == null ? null : Enum.Parse<T>(DeSnake(v)));

    private static string ASnake(string pascal) =>
        string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToUpperInvariant();

    private static string DeSnake(string snake) =>
        string.Concat(snake.Split('_').Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
}
