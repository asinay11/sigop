import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { ResultadoLogin } from './modelos';

const CLAVE_SESION = 'sigop.sesion';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly sesionActual = signal<ResultadoLogin | null>(this.leerSesion());

  readonly sesion = this.sesionActual.asReadonly();
  readonly autenticado = computed(() => this.sesionActual() !== null);
  readonly nombre = computed(() => this.sesionActual()?.nombre ?? '');
  readonly entidad = computed(() => this.sesionActual()?.entidad ?? '');

  login(usuario: string, password: string): Observable<ResultadoLogin> {
    return this.http
      .post<ResultadoLogin>(`${environment.urlApi}/api/auth/login`, { usuario, password })
      .pipe(tap((resultado) => this.guardarSesion(resultado)));
  }

  logout(): void {
    localStorage.removeItem(CLAVE_SESION);
    this.sesionActual.set(null);
    this.router.navigate(['/login']);
  }

  obtenerToken(): string | null {
    return this.sesionActual()?.token ?? null;
  }

  private leerSesion(): ResultadoLogin | null {
    const bruto = localStorage.getItem(CLAVE_SESION);

    if (!bruto) {
      return null;
    }

    try {
      const sesion = JSON.parse(bruto) as ResultadoLogin;
      return new Date(sesion.expira) > new Date() ? sesion : null;
    } catch {
      return null;
    }
  }

  private guardarSesion(sesion: ResultadoLogin): void {
    localStorage.setItem(CLAVE_SESION, JSON.stringify(sesion));
    this.sesionActual.set(sesion);
  }
}
