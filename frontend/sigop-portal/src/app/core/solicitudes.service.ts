import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ActualizarSolicitud,
  EstadoSolicitud,
  Pagina,
  RegistrarSolicitud,
  ResultadoRegistro,
  SolicitudDetalle,
  SolicitudResumen,
  UnidadEjecutora,
} from './modelos';

@Injectable({ providedIn: 'root' })
export class SolicitudesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.urlApi}/api/solicitudes`;

  buscar(filtro: {
    estado?: EstadoSolicitud | null;
    numero?: string | null;
    pagina?: number;
    tamanoPagina?: number;
  }): Observable<Pagina<SolicitudResumen>> {
    let parametros = new HttpParams()
      .set('pagina', filtro.pagina ?? 1)
      .set('tamanoPagina', filtro.tamanoPagina ?? 20);

    if (filtro.estado) {
      parametros = parametros.set('estado', filtro.estado);
    }

    if (filtro.numero?.trim()) {
      parametros = parametros.set('numero', filtro.numero.trim());
    }

    return this.http.get<Pagina<SolicitudResumen>>(this.base, { params: parametros });
  }

  obtener(id: string): Observable<SolicitudDetalle> {
    return this.http.get<SolicitudDetalle>(`${this.base}/${id}`);
  }

  unidades(): Observable<UnidadEjecutora[]> {
    return this.http.get<UnidadEjecutora[]>(`${environment.urlApi}/api/unidades`);
  }

  registrar(datos: RegistrarSolicitud): Observable<ResultadoRegistro> {
    return this.http.post<ResultadoRegistro>(this.base, datos);
  }

  actualizar(id: string, datos: ActualizarSolicitud): Observable<SolicitudDetalle> {
    return this.http.put<SolicitudDetalle>(`${this.base}/${id}`, datos);
  }

  cambiarEstado(id: string, estadoDestino: EstadoSolicitud, motivo: string): Observable<SolicitudDetalle> {
    return this.http.post<SolicitudDetalle>(`${this.base}/${id}/estado`, { estadoDestino, motivo });
  }

  reprocesar(id: string, motivo: string): Observable<SolicitudDetalle> {
    return this.http.post<SolicitudDetalle>(`${this.base}/${id}/reproceso`, { motivo });
  }
}
