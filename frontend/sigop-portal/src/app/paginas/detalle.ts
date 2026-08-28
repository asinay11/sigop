import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { BadgeEstado } from '../core/badge';
import { BarraSuperior } from '../core/barra';
import { ESTILO_ESTADO } from '../core/estados';
import {
  ETIQUETA_ESTADO, ETIQUETA_ORIGEN, ETIQUETA_TIPO,
  ErrorApi, EstadoSolicitud, SolicitudDetalle,
} from '../core/modelos';
import { SolicitudesService } from '../core/solicitudes.service';

@Component({
  selector: 'sigop-detalle',
  imports: [RouterLink, CurrencyPipe, DatePipe, BadgeEstado, BarraSuperior],
  template: `
    <sigop-barra />

    <main>
      <a routerLink="/solicitudes" class="volver">← Solicitudes</a>

      @if (cargando()) {
        <p class="aviso">Cargando…</p>
      } @else if (solicitud(); as s) {
        <header class="cabecera">
          <div>
            <h1>{{ s.numero }}</h1>
            <div class="estado">
              <sigop-badge [estado]="s.estado" />
              <span class="sentido">{{ sentido(s.estado) }}</span>
            </div>
          </div>
          <div class="importe">
            <span class="rotulo">Monto</span>
            <strong>{{ s.monto | currency: s.moneda : 'symbol-narrow' }}</strong>
          </div>
        </header>

        @if (s.ultimoError) {
          <div class="fallo">
            <strong>Último error</strong>
            <span>{{ s.ultimoError }}</span>
            <span class="intentos">Reprocesos usados: {{ s.intentosReproceso }} de {{ s.maximoReprocesos }}</span>
          </div>
        }

        <div class="columnas">
          <section class="panel">
            <h2>Datos de la solicitud</h2>
            <dl>
              <dt>Tipo de operación</dt>
              <dd>{{ etiquetaTipo(s.tipoOperacion) }}</dd>

              <dt>Modo de ejecución</dt>
              <dd>{{ s.esSincrona ? 'Síncrono' : 'Asíncrono, por mensajería' }}</dd>

              <dt>Concepto</dt>
              <dd>{{ s.concepto }}</dd>

              @if (s.beneficiarioNombre) {
                <dt>Beneficiario</dt>
                <dd>{{ s.beneficiarioNombre }}</dd>

                <dt>NIT</dt>
                <dd class="protegido">{{ s.beneficiarioNitEnmascarado }} <i title="Almacenado cifrado">🔒</i></dd>

                <dt>Cuenta bancaria</dt>
                <dd class="protegido">{{ s.cuentaBancariaEnmascarada }} <i title="Almacenado cifrado">🔒</i></dd>
              }

              <dt>Registrada por</dt>
              <dd>{{ s.creadoPor }} · {{ s.creadoEn | date: 'dd/MM/yyyy HH:mm' }}</dd>

              <dt>Correlación</dt>
              <dd class="protegido">{{ s.correlationId }}</dd>
            </dl>

            <div class="acciones">
              @if (puedeReprocesar(s)) {
                <button type="button" class="reproceso" (click)="reprocesar(s)">
                  ↻ Reprocesar · intento {{ s.intentosReproceso + 1 }} de {{ s.maximoReprocesos }}
                </button>
              }

              @for (destino of accionesManuales(s); track destino) {
                <button type="button" (click)="cambiarEstado(s, destino)">{{ etiquetaEstado(destino) }}</button>
              }

              @if (accionesManuales(s).length === 0 && !puedeReprocesar(s)) {
                <p class="sin-acciones">Sin acciones disponibles en este estado.</p>
              }
            </div>

            @if (error(); as e) {
              <div class="error" role="alert">
                <strong>{{ e.detalle }}</strong>
                @if (e.correlationId) {
                  <span class="correlacion">Referencia: {{ e.correlationId }}</span>
                }
              </div>
            }
          </section>

          <section class="panel">
            <h2>Historial</h2>
            <ol class="linea">
              @for (h of s.historial; track h.ocurridoEn) {
                <li>
                  <span class="punto" [style.background]="color(h.estadoNuevo)"></span>
                  <div class="hito">
                    <strong [style.color]="color(h.estadoNuevo)">{{ etiquetaEstado(h.estadoNuevo) }}</strong>
                    <span class="origen">{{ etiquetaOrigen(h.origen) }}</span>
                    <span class="cuando">{{ h.ocurridoEn | date: 'dd/MM/yyyy HH:mm:ss' }}</span>
                  </div>
                  <p class="motivo">{{ h.motivo }}</p>
                  <p class="autor">{{ h.realizadoPor }}</p>
                </li>
              }
            </ol>
          </section>
        </div>
      } @else {
        <p class="aviso error">No se encontró la solicitud.</p>
      }
    </main>
  `,
  styles: `
    main { max-width: 64rem; margin: 0 auto; padding: 24px; }
    .volver { font-size: 12px; color: var(--texto-suave); }

    .cabecera {
      display: flex; justify-content: space-between; align-items: flex-start;
      background: var(--superficie); border: 1px solid var(--borde);
      border-radius: var(--radio); padding: 18px 20px; margin: 12px 0 14px;
    }
    h1 { font-family: var(--mono); font-size: 18px; font-weight: 500; margin: 0 0 8px; }
    .estado { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .sentido { font-size: 11.5px; color: var(--texto-debil); }
    .importe { text-align: right; }
    .importe .rotulo {
      display: block; font-size: 10.5px; letter-spacing: .06em;
      text-transform: uppercase; color: var(--texto-tenue); margin-bottom: 3px;
    }
    .importe strong { font-size: 26px; font-weight: 500; font-variant-numeric: tabular-nums; }

    .fallo {
      display: flex; flex-direction: column; gap: 3px;
      background: #fcf1e3; border: 1px solid #ebd3ae; border-left: 3px solid #99530a;
      border-radius: var(--radio); padding: 12px 14px; margin-bottom: 14px; font-size: 12.5px;
    }
    .fallo strong { color: #99530a; }
    .intentos { font-size: 11px; color: var(--texto-tenue); }

    .columnas { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; align-items: start; }
    @media (max-width: 60rem) { .columnas { grid-template-columns: 1fr; } }

    .panel {
      background: var(--superficie); border: 1px solid var(--borde);
      border-radius: var(--radio); padding: 18px 20px;
    }
    h2 {
      font-size: 10.5px; font-weight: 500; letter-spacing: .06em; text-transform: uppercase;
      color: var(--texto-tenue); margin: 0 0 14px; padding-bottom: 8px;
      border-bottom: 1px solid var(--borde-suave);
    }

    dl { display: grid; grid-template-columns: 9.5rem 1fr; gap: 9px 14px; margin: 0; font-size: 12.5px; }
    dt { color: var(--texto-tenue); }
    dd { margin: 0; }
    .protegido { font-family: var(--mono); font-size: 11.5px; }
    .protegido i { font-style: normal; font-size: 10px; opacity: .6; }

    .acciones { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 18px;
                padding-top: 14px; border-top: 1px solid var(--borde-suave); }
    .acciones button {
      padding: 6px 14px; cursor: pointer; font-size: 12.5px;
      background: var(--superficie); color: var(--texto);
      border: 1px solid var(--borde-fuerte); border-radius: var(--radio);
    }
    .acciones button:hover { background: var(--superficie-alt); }
    .reproceso { color: #99530a !important; border-color: #ebd3ae !important; background: #fcf1e3 !important; }
    .sin-acciones { font-size: 11.5px; color: var(--texto-debil); margin: 0; }

    .error {
      display: flex; flex-direction: column; gap: 3px; margin-top: 12px;
      padding: 10px 12px; font-size: 12.5px;
      color: var(--peligro); background: var(--peligro-fondo);
      border: 1px solid var(--peligro-borde); border-radius: var(--radio);
    }
    .correlacion { font-family: var(--mono); font-size: 10.5px; opacity: .75; }

    .linea { list-style: none; padding: 0; margin: 0; }
    .linea li { position: relative; padding: 0 0 18px 20px; border-left: 1px solid var(--borde); }
    .linea li:last-child { border-left-color: transparent; padding-bottom: 0; }
    .punto {
      position: absolute; left: -4.5px; top: 3px;
      width: 8px; height: 8px; border-radius: 50%;
      box-shadow: 0 0 0 3px var(--superficie);
    }
    .hito { display: flex; align-items: baseline; gap: 9px; flex-wrap: wrap; }
    .hito strong { font-size: 12.5px; font-weight: 600; }
    .origen {
      font-size: 10px; letter-spacing: .04em; text-transform: uppercase;
      color: var(--texto-tenue); background: var(--superficie-alt);
      border: 1px solid var(--borde-suave); border-radius: var(--radio); padding: 1px 5px;
    }
    .cuando { margin-left: auto; font-size: 11px; color: var(--texto-debil);
              font-variant-numeric: tabular-nums; }
    .motivo { margin: 4px 0 0; font-size: 12px; color: var(--texto-suave); line-height: 1.45; }
    .autor { margin: 2px 0 0; font-size: 11px; color: var(--texto-debil); }

    .aviso { padding: 40px; text-align: center; color: var(--texto-tenue); }
    .aviso.error { color: var(--peligro); }
  `,
})
export class DetallePagina {
  private readonly servicio = inject(SolicitudesService);
  private readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly solicitud = signal<SolicitudDetalle | null>(null);
  protected readonly cargando = signal(true);
  protected readonly error = signal<ErrorApi | null>(null);

  constructor() {
    queueMicrotask(() => this.cargar());
  }

  protected cargar(): void {
    this.cargando.set(true);

    this.servicio.obtener(this.id()).subscribe({
      next: (detalle) => {
        this.solicitud.set(detalle);
        this.cargando.set(false);
      },
      error: () => {
        this.solicitud.set(null);
        this.cargando.set(false);
      },
    });
  }

  protected accionesManuales(s: SolicitudDetalle): EstadoSolicitud[] {
    const manuales: EstadoSolicitud[] = ['Anulada', 'EnValidacion', 'Rechazada'];
    return s.transicionesDisponibles.filter((destino) => manuales.includes(destino));
  }

  protected puedeReprocesar(s: SolicitudDetalle): boolean {
    return s.estado === 'Fallida' && s.intentosReproceso < s.maximoReprocesos;
  }

  protected cambiarEstado(s: SolicitudDetalle, destino: EstadoSolicitud): void {
    const motivo = prompt(`Motivo del cambio a "${ETIQUETA_ESTADO[destino]}":`);

    if (!motivo?.trim()) {
      return;
    }

    this.error.set(null);

    this.servicio.cambiarEstado(s.id, destino, motivo).subscribe({
      next: (detalle) => this.solicitud.set(detalle),
      error: (respuesta: HttpErrorResponse) => this.error.set(respuesta.error),
    });
  }

  protected reprocesar(s: SolicitudDetalle): void {
    const motivo = prompt('Motivo del reproceso:');

    if (!motivo?.trim()) {
      return;
    }

    this.error.set(null);

    this.servicio.reprocesar(s.id, motivo).subscribe({
      next: (detalle) => this.solicitud.set(detalle),
      error: (respuesta: HttpErrorResponse) => this.error.set(respuesta.error),
    });
  }

  protected color(estado: EstadoSolicitud): string {
    return ESTILO_ESTADO[estado].fg;
  }

  protected sentido(estado: EstadoSolicitud): string {
    return ESTILO_ESTADO[estado].sentido;
  }

  protected etiquetaEstado(estado: EstadoSolicitud): string {
    return ETIQUETA_ESTADO[estado];
  }

  protected etiquetaTipo(tipo: SolicitudDetalle['tipoOperacion']): string {
    return ETIQUETA_TIPO[tipo];
  }

  protected etiquetaOrigen(origen: SolicitudDetalle['historial'][number]['origen']): string {
    return ETIQUETA_ORIGEN[origen];
  }
}
