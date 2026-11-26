import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { CashierComponent } from './cashier.component';
import { ContractDialogComponent } from './contract-dialog.component';

const routes: Routes = [{ path: '', component: CashierComponent, title: 'Procesos de caja' }];

/**
 * Procesos de caja (solo cajero): abrir/cerrar caja, atender la cola de turnos,
 * contratar, registrar pagos, cambiar servicio o forma de pago y cancelar contratos.
 */
@NgModule({
  declarations: [CashierComponent, ContractDialogComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class CashierModule {}
