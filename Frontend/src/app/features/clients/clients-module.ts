import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { ClientsComponent } from './clients.component';

const routes: Routes = [{ path: '', component: ClientsComponent, title: 'Clientes' }];

@NgModule({
  declarations: [ClientsComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class ClientsModule {}
