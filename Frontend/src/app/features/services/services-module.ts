import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { ServiceFormDialogComponent } from './service-form-dialog.component';
import { ServicesComponent } from './services.component';

const routes: Routes = [{ path: '', component: ServicesComponent, title: 'Servicios' }];

@NgModule({
  declarations: [ServicesComponent, ServiceFormDialogComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class ServicesModule {}
