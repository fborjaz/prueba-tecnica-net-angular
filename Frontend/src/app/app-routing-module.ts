import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/guards/auth.guards';
import { ROLES } from './core/models/api.models';
import { ShellComponent } from './layout/shell.component';

/**
 * Todas las rutas cargan módulos de forma diferida (loadChildren).
 * - authGuard: exige sesión para el área privada.
 * - roleGuard (canMatch): valida el rol y que la ruta esté en el menú de la BD para ese rol;
 *   si no, el módulo ni siquiera se descarga.
 */
const routes: Routes = [
  {
    path: 'auth',
    canMatch: [guestGuard],
    loadChildren: () => import('./features/auth/auth-module').then(m => m.AuthModule)
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      {
        path: 'welcome',
        loadChildren: () => import('./features/welcome/welcome-module').then(m => m.WelcomeModule)
      },
      {
        path: 'dashboard',
        canMatch: [roleGuard],
        data: { roles: [ROLES.admin] },
        loadChildren: () => import('./features/dashboard/dashboard-module').then(m => m.DashboardModule)
      },
      {
        path: 'users',
        canMatch: [roleGuard],
        data: { roles: [ROLES.admin, ROLES.manager] },
        loadChildren: () => import('./features/users/users-module').then(m => m.UsersModule)
      },
      {
        path: 'cash',
        canMatch: [roleGuard],
        data: { roles: [ROLES.admin, ROLES.manager] },
        loadChildren: () => import('./features/cash/cash-module').then(m => m.CashModule)
      },
      {
        path: 'turns',
        canMatch: [roleGuard],
        data: { roles: [ROLES.manager] },
        loadChildren: () => import('./features/turns/turns-module').then(m => m.TurnsModule)
      },
      {
        path: 'cashier',
        canMatch: [roleGuard],
        data: { roles: [ROLES.cashier] },
        loadChildren: () => import('./features/cashier/cashier-module').then(m => m.CashierModule)
      },
      {
        path: 'clients',
        canMatch: [roleGuard],
        data: { roles: [ROLES.cashier] },
        loadChildren: () => import('./features/clients/clients-module').then(m => m.ClientsModule)
      },
      {
        path: 'services',
        canMatch: [roleGuard],
        data: { roles: [ROLES.admin] },
        loadChildren: () => import('./features/services/services-module').then(m => m.ServicesModule)
      },
      { path: '', pathMatch: 'full', redirectTo: 'welcome' }
    ]
  },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { scrollPositionRestoration: 'top' })],
  exports: [RouterModule]
})
export class AppRoutingModule {}
