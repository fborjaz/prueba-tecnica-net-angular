import { NgModule } from '@angular/core';
import { SharedModule } from '../shared/shared-module';
import { ShellComponent } from './shell.component';

/** Estructura visual autenticada (sidebar + contenido). */
@NgModule({
  declarations: [ShellComponent],
  imports: [SharedModule],
  exports: [ShellComponent]
})
export class LayoutModule {}
