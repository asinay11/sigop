import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'sigop-login',
  imports: [ReactiveFormsModule],
  template: `
    <main>
      <section class="tarjeta">
        <div class="encabezado">
          <span class="sigla">SIGOP</span>
          <h1>Sistema de Gestión de Operaciones Presupuestarias</h1>
          <p class="institucion">Ministerio de Finanzas Públicas</p>
        </div>

        <form [formGroup]="formulario" (ngSubmit)="entrar()">
          <label>
            <span class="rotulo">Usuario</span>
            <input type="text" formControlName="usuario" autocomplete="username" autofocus />
          </label>

          <label>
            <span class="rotulo">Contraseña</span>
            <input type="password" formControlName="password" autocomplete="current-password" />
          </label>

          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }

          <button type="submit" [disabled]="formulario.invalid || cargando()">
            {{ cargando() ? 'Verificando…' : 'Iniciar sesión' }}
          </button>
        </form>
      </section>

      <p class="pie">Uso exclusivo de personal autorizado. Todos los accesos quedan registrados.</p>
    </main>
  `,
  styles: `
    main {
      min-height: 100vh; display: flex; flex-direction: column;
      align-items: center; justify-content: center; gap: 18px; padding: 24px;
    }
    .tarjeta {
      width: 100%; max-width: 24rem;
      background: var(--superficie); border: 1px solid var(--borde);
      border-radius: var(--radio); padding: 32px;
    }
    .encabezado { text-align: center; margin-bottom: 28px; }
    .sigla {
      display: inline-block; font-size: 19px; font-weight: 600;
      letter-spacing: .06em; color: var(--primario);
    }
    h1 { font-size: 13px; font-weight: 400; color: var(--texto-suave); margin: 8px 0 2px; line-height: 1.4; }
    .institucion { font-size: 11.5px; color: var(--texto-debil); margin: 0; }
    form { display: flex; flex-direction: column; gap: 16px; }
    label { display: flex; flex-direction: column; gap: 5px; }
    .rotulo {
      font-size: 11px; font-weight: 500; letter-spacing: .06em;
      text-transform: uppercase; color: var(--texto-suave);
    }
    input {
      padding: 8px 10px; border: 1px solid var(--borde-fuerte);
      border-radius: var(--radio); background: var(--superficie-tenue); color: var(--texto);
    }
    input:focus { background: var(--superficie); }
    button {
      margin-top: 4px; padding: 9px; cursor: pointer;
      background: var(--primario); color: #fff;
      border: none; border-radius: var(--radio); font-weight: 500;
    }
    button:hover:not(:disabled) { background: var(--primario-hover); }
    button:disabled { opacity: .55; cursor: default; }
    .error {
      margin: 0; padding: 8px 10px; font-size: 12px;
      color: var(--peligro); background: var(--peligro-fondo);
      border: 1px solid var(--peligro-borde); border-radius: var(--radio);
    }
    .pie { font-size: 11px; color: var(--texto-debil); margin: 0; text-align: center; }
  `,
})
export class LoginPagina {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly cargando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly formulario = inject(FormBuilder).nonNullable.group({
    usuario: ['', Validators.required],
    password: ['', Validators.required],
  });

  protected entrar(): void {
    this.cargando.set(true);
    this.error.set(null);

    const { usuario, password } = this.formulario.getRawValue();

    this.auth.login(usuario, password).subscribe({
      next: () => this.router.navigate(['/solicitudes']),

      error: () => {
        this.error.set('Usuario o contraseña incorrectos.');
        this.cargando.set(false);
      },
    });
  }
}
