import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es-EC';
import { LOCALE_ID, NgModule, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { CoreModule } from './core/core-module';
import { LayoutModule } from './layout/layout-module';
import { SharedModule } from './shared/shared-module';

registerLocaleData(localeEs, 'es-EC');

@NgModule({
  declarations: [App],
  imports: [BrowserModule, CoreModule, SharedModule, LayoutModule, AppRoutingModule],
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    { provide: LOCALE_ID, useValue: 'es-EC' }
  ],
  bootstrap: [App]
})
export class AppModule {}
