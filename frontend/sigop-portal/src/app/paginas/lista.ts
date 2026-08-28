import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BadgeEstado } from '../core/badge';
import { BarraSuperior } from '../core/barra';
import { ETIQUETA_ESTADO, ETIQUETA_TIPO, EstadoSolicitud, Pagina, SolicitudResumen } from '../core/modelos';
import { SolicitudesService } from '../core/solicitudes.service';

@Component({
  selector: 'sigop-lista',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, BadgeEstado, BarraSuperior],
  template: `
    <sigop-barra />

    <main>
      <div class="titulo">
        <div>
          <h1>Solicitudes</h1>
          @if (pagina(); as p) {
            <p class="conteo">{{ p.total }} {{ p.total === 1 ? 'solicitud' : 'solicitudes' }}</p>
          }
        </div>
        <a routerLink="/solicitudes/nueva" class="primario">Nueva solicitud</a>
      </div>

      <div class="filtros">
        <label>
          <span class="rotulo">Estado</span>
          <select [(ngModel)]="estado" (ngModelChange)="buscar(1)">
            <option [ngValue]="null">Todos</option>
            @for (e of estados; track e) {
              <option [ngValue]="e">{{ etiquetaEstado(e) }}</option>
            }
          </select>
        </label>

        <label class="crecer">
          <span class="rotulo">Número de solicitud</span>
          <input type="search" placeholder="SOL-2026-…" [(ngModel)]="numero" (keyup.enter)="buscar(1)" />
        </label>
      </div>

      <section class="panel">
        @if (cargando()) {
          <p class="aviso">Cargando…</p>
        } @else if (error()) {
          <p class="aviso error">{{ error() }}</p>
        } @else if (pagina(); as p) {
          @if (p.elementos.length === 0) {
            <div class="vacio">
              <p>No hay solicitudes con esos filtros.</p>
              <span>Pruebe con otro estado o revise el número ingresado.</span>
            </div>
          } @else {
            <table>
              <thead>
                <tr>
                  <th>Número</th>
                  <th>Tipo de operación</th>
                  <th class="der">Monto</th>
                  <th>Concepto</th>
                  <th>Estado</th>
                  <th>Registrada</th>
                </tr>
              </thead>
              <tbody>
                @for (s of p.elementos; track s.id) {
                  <tr [routerLink]="['/solicitudes', s.id]" tabindex="0">
                    <td class="numero">{{ s.numero }}</td>
                    <td>{{ etiquetaTipo(s.tipoOperacion) }}</td>
                    <td class="der monto">{{ s.monto | currency: s.moneda : 'symbol-narrow' }}</td>
                    <td class="concepto">{{ s.concepto }}</td>
                    <td><sigop-badge [estado]="s.estado" /></td>
                    <td class="fecha">{{ s.creadoEn | date: 'dd/MM/yyyy HH:mm' }}</td>
                  </tr>
                }
              </tbody>
            </table>

            @if (p.totalPaginas > 1) {
              <div class="paginacion">
                <button type="button" [disabled]="p.pagina <= 1" (click)="buscar(p.pagina - 1)">Anterior</button>
                <span>Página {{ p.pagina }} de {{ p.totalPaginas }}</span>
                <button type="button" [disabled]="p.pagina >= p.totalPaginas" (click)="buscar(p.pagina + 1)">Siguiente</button>
              </div>
            }
          }
        }
      </section>
    </main>
  `,
  styles: `
    main { max-width: 72rem; margin: 0 auto; padding: 24px; }
    .titulo { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 18px; }
    h1 { font-size: 19px; font-weight: 600; margin: 0; }
    .conteo { font-size: 12px; color: var(--texto-tenue); margin: 3px 0 0; }
    .primario {
      padding: 8px 16px; background: var(--primario); color: #fff;
      border-radius: var(--radio); font-size: 12.5px; font-weight: 500;
    }
    .primario:hover { background: var(--primario-hover); color: #fff; text-decoration: none; }

    .filtros { display: flex; gap: 14px; margin-bottom: 14px; }
    .filtros label { display: flex; flex-direction: column; gap: 4px; }
    .crecer { flex: 1; max-width: 20rem; }
    .rotulo {
      font-size: 10.5px; font-weight: 500; letter-spacing: .06em;
      text-transform: uppercase; color: var(--texto-tenue);
    }
    select, input {
      padding: 6px 9px; border: 1px solid var(--borde-fuerte);
      border-radius: var(--radio); background: var(--superficie); color: var(--texto);
    }

    .panel { background: var(--superficie); border: 1px solid var(--borde); border-radius: var(--radio); }
    table { width: 100%; border-collapse: collapse; }
    th {
      text-align: left; padding: 9px 12px;
      font-size: 10.5px; font-weight: 500; letter-spacing: .06em; text-transform: uppercase;
      color: var(--texto-tenue); background: var(--superficie-alt);
      border-bottom: 1px solid var(--borde);
    }
    td { padding: 10px 12px; border-bottom: 1px solid var(--borde-suave); font-size: 12.5px; }
    tbody tr { cursor: pointer; }
    tbody tr:hover { background: var(--superficie-alt); }
    tbody tr:last-child td { border-bottom: none; }
    .der { text-align: right; }
    .numero { font-family: var(--mono); font-size: 12px; color: var(--primario); }
    .monto { font-variant-numeric: tabular-nums; font-weight: 500; }
    .concepto { color: var(--texto-suave); max-width: 20rem; overflow: hidden;
                text-overflow: ellipsis; white-space: nowrap; }
    .fecha { color: var(--texto-tenue); font-size: 12px; white-space: nowrap; }

    .paginacion {
      display: flex; gap: 12px; align-items: center; justify-content: flex-end;
      padding: 10px 12px; border-top: 1px solid var(--borde-suave); font-size: 12px;
    }
    .paginacion button {
      padding: 5px 12px; background: var(--superficie);
      border: 1px solid var(--borde-fuerte); border-radius: var(--radio); cursor: pointer;
    }
    .paginacion button:disabled { opacity: .45; cursor: default; }

    .aviso { padding: 24px; text-align: center; color: var(--texto-tenue); margin: 0; }
    .error { color: var(--peligro); }
    .vacio { padding: 44px 24px; text-align: center; }
    .vacio p { margin: 0 0 4px; color: var(--texto-suave); }
    .vacio span { font-size: 12px; color: var(--texto-debil); }
  `,
})
export class ListaPagina {
  private readonly servicio = inject(SolicitudesService);

  protected readonly estados: EstadoSolicitud[] = [
    'Registrada', 'EnValidacion', 'Validada', 'Rechazada',
    'EnProceso', 'Ejecutada', 'Fallida', 'Anulada',
  ];

  protected estado: EstadoSolicitud | null = null;
  protected numero = '';

  protected readonly pagina = signal<Pagina<SolicitudResumen> | null>(null);
  protected readonly cargando = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.buscar(1);
  }

  protected buscar(pagina: number): void {
    this.cargando.set(true);
    this.error.set(null);

    this.servicio.buscar({ estado: this.estado, numero: this.numero, pagina }).subscribe({
      next: (resultado) => {
        this.pagina.set(resultado);
        this.cargando.set(false);
      },
      error: () => {
        this.error.set('No se pudo cargar el listado.');
        this.cargando.set(false);
      },
    });
  }

  protected etiquetaEstado(estado: EstadoSolicitud): string {
    return ETIQUETA_ESTADO[estado];
  }

  protected etiquetaTipo(tipo: SolicitudResumen['tipoOperacion']): string {
    return ETIQUETA_TIPO[tipo];
  }
}
