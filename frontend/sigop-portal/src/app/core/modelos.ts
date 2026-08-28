
export type EstadoSolicitud =
  | 'Registrada'
  | 'EnValidacion'
  | 'Validada'
  | 'Rechazada'
  | 'EnProceso'
  | 'Ejecutada'
  | 'Fallida'
  | 'Anulada';

export type TipoOperacion =
  | 'PagoProveedor'
  | 'TransferenciaPresupuestaria'
  | 'ModificacionPresupuestaria'
  | 'ConsultaDisponibilidad';

export type OrigenCambio = 'Usuario' | 'Validacion' | 'Saga' | 'Reproceso';

export interface ResultadoLogin {
  token: string;
  expira: string;
  usuario: string;
  nombre: string;
  entidad: string;
}

export interface SolicitudResumen {
  id: string;
  numero: string;
  tipoOperacion: TipoOperacion;
  monto: number;
  moneda: string;
  concepto: string;
  estado: EstadoSolicitud;
  creadoEn: string;
  creadoPor: string;
}

export interface CambioEstado {
  estadoAnterior: EstadoSolicitud | null;
  estadoNuevo: EstadoSolicitud;
  motivo: string;
  origen: OrigenCambio;
  realizadoPor: string;
  correlationId: string;
  ocurridoEn: string;
}

export interface SolicitudDetalle {
  id: string;
  numero: string;
  entidadId: number;
  unidadEjecutoraId: number;
  tipoOperacion: TipoOperacion;
  esSincrona: boolean;
  monto: number;
  moneda: string;
  concepto: string;
  beneficiarioNombre: string | null;

  beneficiarioNitEnmascarado: string | null;
  cuentaBancariaEnmascarada: string | null;

  estado: EstadoSolicitud;

  transicionesDisponibles: EstadoSolicitud[];

  intentosReproceso: number;
  maximoReprocesos: number;
  ultimoError: string | null;
  correlationId: string;
  creadoEn: string;
  creadoPor: string;
  historial: CambioEstado[];
}

export interface Pagina<T> {
  elementos: T[];
  pagina: number;
  tamanoPagina: number;
  total: number;
  totalPaginas: number;
}

export interface ResultadoRegistro {
  id: string;
  numero: string;
  estado: EstadoSolicitud;
  esSincrona: boolean;
  correlationId: string;
}

export interface RegistrarSolicitud {
  unidadEjecutoraId?: number | null;
  tipoOperacion: TipoOperacion;
  monto: number;
  moneda: string;
  concepto: string;
  beneficiarioNombre?: string | null;
  beneficiarioNit?: string | null;
  cuentaBancaria?: string | null;
}

export interface ActualizarSolicitud {
  monto: number;
  moneda: string;
  concepto: string;
  beneficiarioNombre?: string | null;
  beneficiarioNit?: string | null;
  cuentaBancaria?: string | null;
}

export interface ErrorApi {
  codigo: string;
  detalle: string;
  correlationId: string | null;
}

export const ETIQUETA_ESTADO: Record<EstadoSolicitud, string> = {
  Registrada: 'Registrada',
  EnValidacion: 'En validación',
  Validada: 'Validada',
  Rechazada: 'Rechazada',
  EnProceso: 'En proceso',
  Ejecutada: 'Ejecutada',
  Fallida: 'Fallida',
  Anulada: 'Anulada',
};

export const ETIQUETA_TIPO: Record<TipoOperacion, string> = {
  PagoProveedor: 'Pago a proveedor',
  TransferenciaPresupuestaria: 'Transferencia presupuestaria',
  ModificacionPresupuestaria: 'Modificación presupuestaria',
  ConsultaDisponibilidad: 'Consulta de disponibilidad',
};

export const ETIQUETA_ORIGEN: Record<OrigenCambio, string> = {
  Usuario: 'Usuario',
  Validacion: 'Validación',
  Saga: 'Proceso automático',
  Reproceso: 'Reproceso',
};

export interface UnidadEjecutora {
  id: number;
  codigo: string;
  nombre: string;
}
