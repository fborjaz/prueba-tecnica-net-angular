import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterModule } from '@angular/router';
import { ClientFormDialogComponent } from './components/client-form-dialog.component';
import { BulkUploadDialogComponent, ConfirmDialogComponent } from './components/dialogs';
import { IconComponent } from './components/icon.component';
import { TurnDisplayComponent } from './components/turn-display.component';
import { EmptyStateComponent, FieldErrorComponent, PageHeaderComponent, StatusChipComponent } from './components/ui.components';

const COMPONENTS = [
  IconComponent,
  PageHeaderComponent,
  FieldErrorComponent,
  StatusChipComponent,
  EmptyStateComponent,
  ConfirmDialogComponent,
  BulkUploadDialogComponent,
  TurnDisplayComponent,
  ClientFormDialogComponent
];

const MODULES = [
  CommonModule,
  FormsModule,
  ReactiveFormsModule,
  RouterModule,
  MatDialogModule,
  MatSnackBarModule,
  MatProgressBarModule,
  MatTooltipModule
];

/** Componentes, directivas y módulos reutilizados por todos los módulos de funcionalidad. */
@NgModule({
  declarations: COMPONENTS,
  imports: MODULES,
  exports: [...MODULES, ...COMPONENTS]
})
export class SharedModule {}
