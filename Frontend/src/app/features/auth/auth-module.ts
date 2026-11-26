import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { AuthShellComponent } from './auth-shell.component';
import { ForgotPasswordComponent } from './forgot-password.component';
import { LoginComponent } from './login.component';
import { ResetPasswordComponent } from './reset-password.component';

const routes: Routes = [
  {
    path: '',
    component: AuthShellComponent,
    children: [
      { path: 'login', component: LoginComponent, title: 'Iniciar sesión' },
      { path: 'forgot-password', component: ForgotPasswordComponent, title: 'Recuperar contraseña' },
      { path: 'reset-password', component: ResetPasswordComponent, title: 'Nueva contraseña' },
      { path: '', pathMatch: 'full', redirectTo: 'login' }
    ]
  }
];

/** Módulo de autenticación (carga diferida en /auth). */
@NgModule({
  declarations: [AuthShellComponent, LoginComponent, ForgotPasswordComponent, ResetPasswordComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class AuthModule {}
