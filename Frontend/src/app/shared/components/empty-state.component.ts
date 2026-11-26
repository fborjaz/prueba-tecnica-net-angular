import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Estado vacío con una indicación de qué hacer. */
@Component({
  selector: 'app-empty',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss'
})
export class EmptyStateComponent {
  readonly icon = input('search');
  readonly title = input.required<string>();
  readonly hint = input<string>();
}
