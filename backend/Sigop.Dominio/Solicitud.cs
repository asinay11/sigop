namespace Sigop.Dominio;

public sealed record SolicitudRegistradaV1(
    Guid SolicitudId, string Numero, long EntidadId, long UnidadEjecutoraId,
    string TipoOperacion, decimal Monto, string Moneda, string RegistradaPor)
{
    public const string Tipo = "SolicitudRegistradaV1";
    public const string RoutingKey = "solicitud.registrada.v1";
}

public sealed record SolicitudCambioEstadoV1(
    Guid SolicitudId, string Numero, long EntidadId,
    string? EstadoAnterior, string EstadoNuevo, string Motivo, string Origen, string RealizadoPor)
{
    public const string Tipo = "SolicitudCambioEstadoV1";
    public const string RoutingKey = "solicitud.estado-cambiado.v1";
}

public sealed class CambioEstado
{
    private CambioEstado()
    {
        Motivo = string.Empty;
        RealizadoPor = string.Empty;
        CorrelationId = string.Empty;
    }

    public CambioEstado(
        Guid solicitudId, EstadoSolicitud? estadoAnterior, EstadoSolicitud estadoNuevo,
        string motivo, OrigenCambio origen, string realizadoPor, string correlationId)
    {
        SolicitudId = solicitudId;
        EstadoAnterior = estadoAnterior;
        EstadoNuevo = estadoNuevo;
        Motivo = motivo;
        Origen = origen;
        RealizadoPor = realizadoPor;
        CorrelationId = correlationId;
        OcurridoEn = DateTimeOffset.UtcNow;
    }

    public long Id { get; private set; }
    public Guid SolicitudId { get; private set; }
    public EstadoSolicitud? EstadoAnterior { get; private set; }
    public EstadoSolicitud EstadoNuevo { get; private set; }
    public string Motivo { get; private set; }
    public OrigenCambio Origen { get; private set; }
    public string RealizadoPor { get; private set; }
    public string CorrelationId { get; private set; }
    public DateTimeOffset OcurridoEn { get; private set; }
}

public sealed class Solicitud
{
    public const int MaximoReprocesos = 3;
    public const int LongitudMaximaConcepto = 500;

    private static readonly string[] MonedasAdmitidas = ["GTQ", "USD"];

    private readonly List<CambioEstado> _historial = [];
    private readonly List<EventoDominio> _eventos = [];

    private Solicitud()
    {
        Numero = string.Empty;
        Moneda = string.Empty;
        Concepto = string.Empty;
        CorrelationId = string.Empty;
        CreadoPor = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Numero { get; private set; }
    public long EntidadId { get; private set; }
    public long UnidadEjecutoraId { get; private set; }
    public TipoOperacion TipoOperacion { get; private set; }
    public decimal Monto { get; private set; }
    public string Moneda { get; private set; }
    public string Concepto { get; private set; }
    public string? BeneficiarioNombre { get; private set; }
    public string? BeneficiarioNit { get; private set; }
    public string? CuentaBancaria { get; private set; }
    public EstadoSolicitud Estado { get; private set; }
    public int IntentosReproceso { get; private set; }
    public string? UltimoError { get; private set; }
    public string CorrelationId { get; private set; }
    public string CreadoPor { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public string? ActualizadoPor { get; private set; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    public IReadOnlyList<CambioEstado> Historial => _historial;
    public IReadOnlyList<EventoDominio> Eventos => _eventos;

    public static Solicitud Registrar(
        string numero, long entidadId, long unidadEjecutoraId, TipoOperacion tipoOperacion,
        decimal monto, string moneda, string concepto,
        string? beneficiarioNombre, string? beneficiarioNit, string? cuentaBancaria,
        string creadoPor, string correlationId)
    {
        ValidarDatos(tipoOperacion, monto, moneda, concepto, beneficiarioNombre, beneficiarioNit, cuentaBancaria);

        var ahora = DateTimeOffset.UtcNow;

        var solicitud = new Solicitud
        {
            Id = Guid.CreateVersion7(),
            Numero = numero,
            EntidadId = entidadId,
            UnidadEjecutoraId = unidadEjecutoraId,
            TipoOperacion = tipoOperacion,
            Monto = decimal.Round(monto, 2, MidpointRounding.ToEven),
            Moneda = moneda.Trim().ToUpperInvariant(),
            Concepto = concepto.Trim(),
            BeneficiarioNombre = beneficiarioNombre?.Trim(),
            BeneficiarioNit = beneficiarioNit?.Trim(),
            CuentaBancaria = cuentaBancaria?.Trim(),
            Estado = EstadoSolicitud.Registrada,
            CorrelationId = correlationId,
            CreadoPor = creadoPor,
            CreadoEn = ahora,
            ActualizadoEn = ahora,
        };

        solicitud._historial.Add(new CambioEstado(
            solicitud.Id, null, EstadoSolicitud.Registrada,
            "Solicitud registrada.", OrigenCambio.Usuario, creadoPor, correlationId));

        solicitud._eventos.Add(new EventoDominio(
            SolicitudRegistradaV1.Tipo,
            SolicitudRegistradaV1.RoutingKey,
            new SolicitudRegistradaV1(
                solicitud.Id, solicitud.Numero, solicitud.EntidadId, solicitud.UnidadEjecutoraId,
                solicitud.TipoOperacion.ToString(), solicitud.Monto, solicitud.Moneda, creadoPor)));

        return solicitud;
    }

    public void ActualizarDatos(
        decimal monto, string moneda, string concepto,
        string? beneficiarioNombre, string? beneficiarioNit, string? cuentaBancaria,
        string actualizadoPor, string motivo, string correlationId)
    {
        if (Estado != EstadoSolicitud.Registrada)
        {
            throw new ExcepcionReglaNegocio(
                "SOLICITUD_NO_EDITABLE",
                $"Una solicitud en estado '{Estado}' ya no admite cambios. Anulela y registre una nueva.");
        }

        ValidarDatos(TipoOperacion, monto, moneda, concepto, beneficiarioNombre, beneficiarioNit, cuentaBancaria);

        Monto = decimal.Round(monto, 2, MidpointRounding.ToEven);
        Moneda = moneda.Trim().ToUpperInvariant();
        Concepto = concepto.Trim();
        BeneficiarioNombre = beneficiarioNombre?.Trim();
        BeneficiarioNit = beneficiarioNit?.Trim();
        CuentaBancaria = cuentaBancaria?.Trim();
        ActualizadoPor = actualizadoPor;
        ActualizadoEn = DateTimeOffset.UtcNow;

        _historial.Add(new CambioEstado(
            Id, Estado, Estado, motivo, OrigenCambio.Usuario, actualizadoPor, correlationId));
    }

    public void IniciarValidacion(string motivo, string por, string correlationId) =>
        Transicionar(EstadoSolicitud.EnValidacion, motivo, OrigenCambio.Validacion, por, correlationId);

    public void MarcarValidada(string detalle, string por, string correlationId)
    {
        Transicionar(EstadoSolicitud.Validada, detalle, OrigenCambio.Validacion, por, correlationId);
        UltimoError = null;
    }

    public void Rechazar(string motivo, string por, string correlationId)
    {
        ExigirNoVacio(motivo, "SOLICITUD_MOTIVO_REQUERIDO", "El rechazo exige un motivo.");
        Transicionar(EstadoSolicitud.Rechazada, motivo, OrigenCambio.Validacion, por, correlationId);
    }

    public void IniciarEjecucion(string detalle, string por, string correlationId) =>
        Transicionar(EstadoSolicitud.EnProceso, detalle, OrigenCambio.Saga, por, correlationId);

    public void MarcarEjecutada(string detalle, string por, string correlationId)
    {
        Transicionar(EstadoSolicitud.Ejecutada, detalle, OrigenCambio.Saga, por, correlationId);
        UltimoError = null;
    }

    public void MarcarFallida(string error, string por, string correlationId)
    {
        ExigirNoVacio(error, "SOLICITUD_ERROR_REQUERIDO", "El fallo exige una descripcion del error.");
        Transicionar(EstadoSolicitud.Fallida, error, OrigenCambio.Saga, por, correlationId);
        UltimoError = error;
    }

    public void Anular(string motivo, string por, string correlationId)
    {
        ExigirNoVacio(motivo, "SOLICITUD_MOTIVO_REQUERIDO", "La anulacion exige un motivo.");
        Transicionar(EstadoSolicitud.Anulada, motivo, OrigenCambio.Usuario, por, correlationId);
    }

    public void Reprocesar(string motivo, string por, string correlationId)
    {
        if (Estado != EstadoSolicitud.Fallida)
        {
            throw new ExcepcionReglaNegocio(
                "REPROCESO_NO_APLICABLE",
                $"Solo se reprocesan solicitudes fallidas. Estado actual: '{Estado}'.");
        }

        if (IntentosReproceso >= MaximoReprocesos)
        {
            throw new ExcepcionReglaNegocio(
                "REPROCESO_AGOTADO",
                $"La solicitud agoto los {MaximoReprocesos} reprocesos admitidos. Requiere intervencion manual.");
        }

        IntentosReproceso++;

        Transicionar(
            EstadoSolicitud.EnValidacion,
            $"Reproceso {IntentosReproceso} de {MaximoReprocesos}. {motivo}",
            OrigenCambio.Reproceso, por, correlationId);
    }

    public void LimpiarEventos() => _eventos.Clear();

    private void Transicionar(
        EstadoSolicitud destino, string motivo, OrigenCambio origen, string por, string correlationId)
    {
        ExigirNoVacio(por, "USUARIO_REQUERIDO", "El responsable del cambio es obligatorio.");
        ExigirNoVacio(correlationId, "CORRELACION_REQUERIDA", "El identificador de correlacion es obligatorio.");

        MaquinaEstados.Exigir(Estado, destino);

        var anterior = Estado;
        Estado = destino;
        ActualizadoPor = por;
        ActualizadoEn = DateTimeOffset.UtcNow;

        _historial.Add(new CambioEstado(Id, anterior, destino, motivo, origen, por, correlationId));

        _eventos.Add(new EventoDominio(
            SolicitudCambioEstadoV1.Tipo,
            SolicitudCambioEstadoV1.RoutingKey,
            new SolicitudCambioEstadoV1(
                Id, Numero, EntidadId, anterior.ToString(), destino.ToString(),
                motivo, origen.ToString(), por)));
    }

    private static void ValidarDatos(
        TipoOperacion tipoOperacion, decimal monto, string moneda, string concepto,
        string? beneficiarioNombre, string? beneficiarioNit, string? cuentaBancaria)
    {
        if (monto <= 0m)
        {
            throw new ExcepcionReglaNegocio("MONTO_INVALIDO", "El monto debe ser mayor que cero.");
        }

        if (!MonedasAdmitidas.Contains(moneda?.Trim().ToUpperInvariant()))
        {
            throw new ExcepcionReglaNegocio(
                "MONEDA_NO_ADMITIDA",
                $"La moneda debe ser una de: {string.Join(", ", MonedasAdmitidas)}.");
        }

        ExigirNoVacio(concepto, "CONCEPTO_REQUERIDO", "El concepto es obligatorio.");

        if (concepto.Trim().Length > LongitudMaximaConcepto)
        {
            throw new ExcepcionReglaNegocio(
                "CONCEPTO_MUY_LARGO",
                $"El concepto no puede exceder {LongitudMaximaConcepto} caracteres.");
        }

        if (tipoOperacion == TipoOperacion.PagoProveedor)
        {
            ExigirNoVacio(beneficiarioNombre, "BENEFICIARIO_REQUERIDO", "El pago a proveedor exige beneficiario.");
            ExigirNoVacio(beneficiarioNit, "BENEFICIARIO_NIT_REQUERIDO", "El pago a proveedor exige el NIT del beneficiario.");
            ExigirNoVacio(cuentaBancaria, "CUENTA_REQUERIDA", "El pago a proveedor exige la cuenta bancaria de destino.");
        }
    }

    private static void ExigirNoVacio(string? valor, string codigo, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ExcepcionReglaNegocio(codigo, mensaje);
        }
    }
}
