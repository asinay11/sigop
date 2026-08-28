namespace Sigop.Dominio;

public enum EstadoSolicitud
{
    Registrada,
    EnValidacion,
    Validada,
    Rechazada,
    EnProceso,
    Ejecutada,
    Fallida,
    Anulada,
}

public enum TipoOperacion
{
    PagoProveedor,
    TransferenciaPresupuestaria,
    ModificacionPresupuestaria,
    ConsultaDisponibilidad,
}

public enum OrigenCambio
{
    Usuario,
    Validacion,
    Saga,
    Reproceso,
}

public static class TipoOperacionExtensiones
{
    public static bool EsSincrona(this TipoOperacion tipo) =>
        tipo == TipoOperacion.ConsultaDisponibilidad;

    public static bool RequiereReservaPresupuestaria(this TipoOperacion tipo) =>
        tipo is TipoOperacion.PagoProveedor
             or TipoOperacion.TransferenciaPresupuestaria
             or TipoOperacion.ModificacionPresupuestaria;
}
