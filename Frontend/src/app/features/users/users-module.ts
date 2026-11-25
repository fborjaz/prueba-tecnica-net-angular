import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { UserFormDialogComponent } from './user-form-dialog.component';
import { UsersListComponent } from './users-list.component';

const routes: Routes = [{ path: '', component: UsersListComponent, title: 'Usuarios' }];

/** Administración de usuarios (administrador y gestor). */
@NgModule({
  declarations: [UsersListComponent, UserFormDialogComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class UsersModule {}
