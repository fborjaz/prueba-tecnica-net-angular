import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { WelcomeComponent } from './welcome.component';

const routes: Routes = [{ path: '', component: WelcomeComponent, title: 'Inicio' }];

@NgModule({
  declarations: [WelcomeComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class WelcomeModule {}
