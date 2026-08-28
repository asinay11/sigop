namespace Sigop.Dominio;

public static class MaquinaEstados
{
    private static readonly Dictionary<EstadoSolicitud, EstadoSolicitud[]> Transiciones = new()
    {
        [EstadoSolicitud.Registrada]   = [EstadoSolicitud.EnValidacion, EstadoSolicitud.Fallida, EstadoSolicitud.Anulada],
        [EstadoSolicitud.EnValidacion] = [EstadoSolicitud.Validada, EstadoSolicitud.Rechazada, EstadoSolicitud.Fallida],
        [EstadoSolicitud.Validada]     = [EstadoSolicitud.EnProceso, EstadoSolicitud.Anulada],
        [EstadoSolicitud.EnProceso]    = [EstadoSolicitud.Ejecutada, EstadoSolicitud.Fallida],

        [EstadoSolicitud.Fallida]      = [EstadoSolicitud.EnValidacion, EstadoSolicitud.Anulada],

        [EstadoSolicitud.Rechazada]    = [],
        [EstadoSolicitud.Ejecutada]    = [],
        [EstadoSolicitud.Anulada]      = [],
    };

    public static bool EsValida(EstadoSolicitud origen, EstadoSolicitud destino) =>
        Transiciones.TryGetValue(origen, out var destinos) && destinos.Contains(destino);

    public static IReadOnlyList<EstadoSolicitud> Disponibles(EstadoSolicitud origen) =>
        Transiciones.TryGetValue(origen, out var destinos) ? destinos : [];

    public static bool EsTerminal(EstadoSolicitud estado) => Disponibles(estado).Count == 0;

    public static void Exigir(EstadoSolicitud origen, EstadoSolicitud destino)
    {
        if (EsValida(origen, destino))
        {
            return;
        }

        var detalle = EsTerminal(origen)
            ? $"'{origen}' es un estado terminal."
            : $"Desde '{origen}' solo se admite: {string.Join(", ", Disponibles(origen))}.";

        throw new ExcepcionReglaNegocio(
            "TRANSICION_INVALIDA",
            $"No se puede pasar de '{origen}' a '{destino}'. {detalle}");
    }
}
