import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'sigop-barra',
  imports: [RouterLink],
  template: `
    <header>
      <a routerLink="/solicitudes" class="marca">SIGOP</a>
      <span class="sistema">Sistema de Gestión de Operaciones Presupuestarias</span>

      <div class="identidad">
        <span class="nombre">{{ auth.nombre() }}</span>
        <span class="entidad">{{ auth.entidad() }}</span>
      </div>

      <button type="button" (click)="auth.logout()">Salir</button>
    </header>
  `,
  styles: `
    header {
      display: flex; align-items: center; gap: 18px;
      padding: 10px 24px;
      background: var(--barra); color: var(--barra-texto);
      font-size: 12.5px;
    }
    .marca { color: #fff; font-weight: 600; letter-spacing: .02em; text-decoration: none; }
    .marca:hover { color: #fff; text-decoration: none; }
    .sistema { opacity: .55; }
    .identidad { margin-left: auto; text-align: right; line-height: 1.35; }
    .nombre { display: block; color: #fff; }
    .entidad { display: block; opacity: .5; font-size: 11px; }
    button {
      background: var(--barra-alt); color: var(--barra-texto);
      border: 1px solid #33465a; border-radius: var(--radio);
      padding: 4px 12px; cursor: pointer;
    }
    button:hover { color: #fff; }
  `,
})
export class BarraSuperior {
  protected readonly auth = inject(AuthService);
}
