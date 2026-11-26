import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { CashFormDialogComponent } from './cash-form-dialog.component';
import { CashListComponent } from './cash-list.component';

const routes: Routes = [{ path: '', component: CashListComponent, title: 'Cajas' }];

/** Cajas y asignación de cajeros (administrador y gestor). */
@NgModule({
  declarations: [CashListComponent, CashFormDialogComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class CashModule {}
