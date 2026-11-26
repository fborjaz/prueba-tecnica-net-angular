import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared-module';
import { DailyChartComponent } from './daily-chart.component';
import { DashboardComponent } from './dashboard.component';

const routes: Routes = [{ path: '', component: DashboardComponent, title: 'Dashboard' }];

@NgModule({
  declarations: [DashboardComponent, DailyChartComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class DashboardModule {}
