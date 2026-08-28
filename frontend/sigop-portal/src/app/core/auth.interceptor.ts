import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const ALFABETO = '23456789ABCDEFGHJKLMNPQRSTUVWXYZ';

function nuevaCorrelacion(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(12));
  const letras = Array.from(bytes, (b) => ALFABETO[b % ALFABETO.length]);

  return [letras.slice(0, 4), letras.slice(4, 8), letras.slice(8, 12)]
    .map((g) => g.join(''))
    .join('-');
}

export const authInterceptor: HttpInterceptorFn = (peticion, siguiente) => {
  const auth = inject(AuthService);
  const token = auth.obtenerToken();

  const cabeceras: Record<string, string> = { 'X-Correlation-Id': nuevaCorrelacion() };

  if (token) {
    cabeceras['Authorization'] = `Bearer ${token}`;
  }

  return siguiente(peticion.clone({ setHeaders: cabeceras })).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        auth.logout();
      }

      return throwError(() => error);
    }),
  );
};
