import { EstadoSolicitud } from './modelos';

export type MarcaEstado = 'hueco' | 'lleno' | 'cuadrado' | 'glifo';

export interface EstiloEstado {
  fg: string;
  bg: string;
  bd: string;
  marca: MarcaEstado;
  glifo?: string;
  sentido: string;
}

export const ESTILO_ESTADO: Record<EstadoSolicitud, EstiloEstado> = {
  Registrada: {
    fg: '#4C5A68', bg: '#EDF1F5', bd: '#D6DDE5', marca: 'hueco',
    sentido: 'Recién ingresada. Aún no entró a validación.',
  },
  EnValidacion: {
    fg: '#4B3F8C', bg: '#EEEBF7', bd: '#CFC7E8', marca: 'hueco',
    sentido: 'En curso: se está verificando la disponibilidad presupuestaria.',
  },
  Validada: {
    fg: '#1F4E79', bg: '#E8EFF6', bd: '#C3D4E5', marca: 'lleno',
    sentido: 'Avance positivo. Aprobada, pendiente de ejecutarse.',
  },
  EnProceso: {
    fg: '#4B3F8C', bg: '#EEEBF7', bd: '#CFC7E8', marca: 'lleno',
    sentido: 'En curso: instrucción enviada al sistema externo.',
  },
  Ejecutada: {
    fg: '#1A6047', bg: '#E6F1EC', bd: '#BFDCCE', marca: 'lleno',
    sentido: 'Éxito final. El movimiento quedó acreditado.',
  },

  Rechazada: {
    fg: '#8C1D18', bg: '#FBEAE8', bd: '#EFC7C2', marca: 'cuadrado',
    sentido: 'Negativo definitivo. No admite reintento.',
  },
  Fallida: {
    fg: '#99530A', bg: '#FCF1E3', bd: '#EBD3AE', marca: 'glifo', glifo: '↻',
    sentido: 'Error recuperable. Se puede reprocesar hasta 3 veces.',
  },

  Anulada: {
    fg: '#6B7887', bg: '#F1F4F8', bd: '#DDE3EA', marca: 'glifo', glifo: '⊘',
    sentido: 'Cancelada por el usuario antes de ejecutarse.',
  },
};
