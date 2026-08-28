import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./paginas/login').then((m) => m.LoginPagina) },

  {
    path: 'solicitudes',
    canActivate: [authGuard],
    children: [
      { path: '', loadComponent: () => import('./paginas/lista').then((m) => m.ListaPagina) },
      { path: 'nueva', loadComponent: () => import('./paginas/nueva').then((m) => m.NuevaPagina) },
      { path: ':id', loadComponent: () => import('./paginas/detalle').then((m) => m.DetallePagina) },
    ],
  },

  { path: '', pathMatch: 'full', redirectTo: 'solicitudes' },
  { path: '**', redirectTo: 'solicitudes' },
];
