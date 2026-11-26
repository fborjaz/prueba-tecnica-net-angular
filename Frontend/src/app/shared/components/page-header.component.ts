import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Encabezado de página: título en tipografía de señalética, descripción y acciones. */
@Component({
  selector: 'app-page-header',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './page-header.component.html',
  styleUrl: './page-header.component.scss'
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly description = input<string>();
}
