import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { BarraSuperior } from '../core/barra';
import { ETIQUETA_TIPO, ErrorApi, TipoOperacion, UnidadEjecutora } from '../core/modelos';
import { SolicitudesService } from '../core/solicitudes.service';

@Component({
  selector: 'sigop-nueva',
  imports: [ReactiveFormsModule, RouterLink, BarraSuperior],
  template: `
    <sigop-barra />

    <main>
      <a routerLink="/solicitudes" class="volver">← Solicitudes</a>
      <h1>Nueva solicitud</h1>
      <p class="nota">Los campos marcados con * son obligatorios.</p>

      <form [formGroup]="formulario" (ngSubmit)="guardar()">
        <section class="panel">
          <label>
            <span class="rotulo">Unidad ejecutora *</span>
            <select formControlName="unidadEjecutoraId">
              @for (u of unidades(); track u.id) {
                <option [value]="u.id">{{ u.codigo }} — {{ u.nombre }}</option>
              }
            </select>
          </label>

          <label>
            <span class="rotulo">Tipo de operación *</span>
            <select formControlName="tipoOperacion">
              @for (t of tipos; track t) {
                <option [value]="t">{{ etiquetaTipo(t) }}</option>
              }
            </select>
          </label>

          <div class="fila">
            <label class="crecer">
              <span class="rotulo">Monto *</span>
              <input type="number" step="0.01" min="0.01" formControlName="monto" class="monto" />
            </label>

            <label class="moneda">
              <span class="rotulo">Moneda *</span>
              <select formControlName="moneda">
                <option value="GTQ">GTQ</option>
                <option value="USD">USD</option>
              </select>
            </label>
          </div>

          <label>
            <span class="rotulo">Concepto *</span>
            <textarea formControlName="concepto" rows="3" maxlength="500"></textarea>
            <small class="contador">{{ largoConcepto() }} / 500</small>
          </label>
        </section>

        @if (esPago()) {
          <section class="panel beneficiario">
            <header>
              <h2>Datos del beneficiario</h2>
              <span>Requeridos únicamente para pagos a proveedor.</span>
            </header>

            <label>
              <span class="rotulo">Nombre o razón social *</span>
              <input type="text" formControlName="beneficiarioNombre" />
            </label>

            <div class="fila">
              <label class="crecer">
                <span class="rotulo">NIT *</span>
                <input type="text" formControlName="beneficiarioNit" />
              </label>

              <label class="crecer">
                <span class="rotulo">Cuenta bancaria *</span>
                <input type="text" formControlName="cuentaBancaria" />
              </label>
            </div>

            <p class="cifrado">NIT y cuenta bancaria se almacenan cifrados y se muestran enmascarados.</p>
          </section>
        }

        @if (error(); as e) {
          <div class="error" role="alert">
            <strong>{{ e.detalle }}</strong>
            @if (e.correlationId) {
              <span class="correlacion">Referencia: {{ e.correlationId }}</span>
            }
          </div>
        }

        <div class="acciones">
          <a routerLink="/solicitudes" class="secundario">Cancelar</a>
          <button type="submit" [disabled]="formulario.invalid || guardando()">
            {{ guardando() ? 'Registrando…' : 'Registrar solicitud' }}
          </button>
        </div>
      </form>
    </main>
  `,
  styles: `
    main { max-width: 40rem; margin: 0 auto; padding: 24px; }
    .volver { font-size: 12px; color: var(--texto-suave); }
    h1 { font-size: 19px; font-weight: 600; margin: 12px 0 3px; }
    .nota { font-size: 11.5px; color: var(--texto-debil); margin: 0 0 18px; }

    form { display: flex; flex-direction: column; gap: 14px; }
    .panel {
      background: var(--superficie); border: 1px solid var(--borde);
      border-radius: var(--radio); padding: 20px;
      display: flex; flex-direction: column; gap: 16px;
    }
    .beneficiario { border-left: 2px solid var(--primario); }
    .beneficiario header h2 { font-size: 13px; font-weight: 600; margin: 0 0 2px; }
    .beneficiario header span { font-size: 11.5px; color: var(--texto-debil); }

    label { display: flex; flex-direction: column; gap: 5px; }
    .rotulo {
      font-size: 10.5px; font-weight: 500; letter-spacing: .06em;
      text-transform: uppercase; color: var(--texto-tenue);
    }
    input, select, textarea {
      padding: 8px 10px; border: 1px solid var(--borde-fuerte);
      border-radius: var(--radio); background: var(--superficie-tenue); color: var(--texto);
    }
    input:focus, select:focus, textarea:focus { background: var(--superficie); }
    textarea { resize: vertical; }
    .monto { font-family: var(--mono); text-align: right; }
    .fila { display: flex; gap: 14px; }
    .crecer { flex: 1; }
    .moneda { width: 7rem; }
    .contador { font-size: 11px; color: var(--texto-debil); text-align: right; }
    .cifrado { font-size: 11.5px; color: var(--primario); margin: 0; }

    .error {
      display: flex; flex-direction: column; gap: 3px;
      padding: 10px 12px; font-size: 12.5px;
      color: var(--peligro); background: var(--peligro-fondo);
      border: 1px solid var(--peligro-borde); border-radius: var(--radio);
    }
    .correlacion { font-family: var(--mono); font-size: 10.5px; opacity: .75; }

    .acciones { display: flex; gap: 10px; justify-content: flex-end; align-items: center; }
    .secundario { font-size: 12.5px; color: var(--texto-suave); padding: 8px 12px; }
    button {
      padding: 8px 18px; cursor: pointer; font-weight: 500;
      background: var(--primario); color: #fff; border: none; border-radius: var(--radio);
    }
    button:hover:not(:disabled) { background: var(--primario-hover); }
    button:disabled { opacity: .55; cursor: default; }
  `,
})
export class NuevaPagina {
  private readonly servicio = inject(SolicitudesService);
  private readonly router = inject(Router);

  protected readonly tipos: TipoOperacion[] = [
    'TransferenciaPresupuestaria',
    'PagoProveedor',
    'ModificacionPresupuestaria',
    'ConsultaDisponibilidad',
  ];

  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);
  protected readonly esPago = signal(false);
  protected readonly largoConcepto = signal(0);

  protected readonly unidades = signal<UnidadEjecutora[]>([]);

  protected readonly formulario = inject(FormBuilder).nonNullable.group({
    unidadEjecutoraId: [0, [Validators.required, Validators.min(1)]],
    tipoOperacion: ['TransferenciaPresupuestaria' as TipoOperacion, Validators.required],
    monto: [0, [Validators.required, Validators.min(0.01)]],
    moneda: ['GTQ', Validators.required],
    concepto: ['', [Validators.required, Validators.maxLength(500)]],
    beneficiarioNombre: [''],
    beneficiarioNit: [''],
    cuentaBancaria: [''],
  });

  constructor() {
    this.servicio.unidades().subscribe((lista) => {
      this.unidades.set(lista);

      if (lista.length > 0) {
        this.formulario.controls.unidadEjecutoraId.setValue(lista[0].id);
      }
    });

    this.formulario.controls.tipoOperacion.valueChanges.subscribe((tipo) =>
      this.esPago.set(tipo === 'PagoProveedor'),
    );

    this.formulario.controls.concepto.valueChanges.subscribe((texto) =>
      this.largoConcepto.set(texto.length),
    );
  }

  protected guardar(): void {
    this.guardando.set(true);
    this.error.set(null);

    const datos = this.formulario.getRawValue();

    this.servicio
      .registrar({
        unidadEjecutoraId: Number(datos.unidadEjecutoraId),
        tipoOperacion: datos.tipoOperacion,
        monto: datos.monto,
        moneda: datos.moneda,
        concepto: datos.concepto,
        beneficiarioNombre: this.esPago() ? datos.beneficiarioNombre : null,
        beneficiarioNit: this.esPago() ? datos.beneficiarioNit : null,
        cuentaBancaria: this.esPago() ? datos.cuentaBancaria : null,
      })
      .subscribe({
        next: (resultado) => this.router.navigate(['/solicitudes', resultado.id]),
        error: (respuesta: HttpErrorResponse) => {
          this.error.set(
            respuesta.error ?? { codigo: 'ERROR', detalle: 'No se pudo registrar la solicitud.', correlationId: null },
          );
          this.guardando.set(false);
        },
      });
  }

  protected etiquetaTipo(tipo: TipoOperacion): string {
    return ETIQUETA_TIPO[tipo];
  }
}
