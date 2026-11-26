import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { TurnsComponent } from './turns.component';

const routes: Routes = [{ path: '', component: TurnsComponent, title: 'Turnos' }];

/** Asignación de turnos por caja (gestor). */
@NgModule({
  declarations: [TurnsComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class TurnsModule {}
