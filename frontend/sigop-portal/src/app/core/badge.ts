import { Component, computed, input } from '@angular/core';
import { ESTILO_ESTADO } from './estados';
import { ETIQUETA_ESTADO, EstadoSolicitud } from './modelos';

@Component({
  selector: 'sigop-badge',
  template: `
    <span class="badge" [style.color]="estilo().fg" [style.background]="estilo().bg"
          [style.border-color]="estilo().bd" [title]="estilo().sentido">
      @switch (estilo().marca) {
        @case ('hueco') {
          <i class="marca hueco" [style.border-color]="estilo().fg"></i>
        }
        @case ('lleno') {
          <i class="marca lleno" [style.background]="estilo().fg"></i>
        }
        @case ('cuadrado') {
          <i class="marca cuadrado" [style.background]="estilo().fg"></i>
        }
        @default {
          <i class="glifo">{{ estilo().glifo }}</i>
        }
      }
      {{ etiqueta() }}
    </span>
  `,
  styles: `
    .badge {
      display: inline-flex; align-items: center; gap: 5px;
      padding: 2px 8px 2px 6px;
      border: 1px solid; border-radius: var(--radio);
      font-size: 11.5px; font-weight: 500; white-space: nowrap;
    }
    .marca { width: 7px; height: 7px; flex: none; }
    .hueco { border: 1.5px solid; border-radius: 50%; }
    .lleno { border-radius: 50%; }
    .cuadrado { border-radius: 0; }
    .glifo { font-style: normal; font-size: 11px; line-height: 1; }
  `,
})
export class BadgeEstado {
  readonly estado = input.required<EstadoSolicitud>();

  protected readonly estilo = computed(() => ESTILO_ESTADO[this.estado()]);
  protected readonly etiqueta = computed(() => ETIQUETA_ESTADO[this.estado()]);
}
