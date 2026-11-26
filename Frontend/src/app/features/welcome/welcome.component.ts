import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, Welcome } from '../../core/models/api.models';

const ROLE_LABELS: Record<string, string> = { ADMINISTRADOR: 'Administrador', GESTOR: 'Gestor', CAJERO: 'Cajero' };

/** Pantalla de bienvenida para todos los roles. */
@Component({
  selector: 'app-welcome',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './welcome.component.html',
  styleUrl: './welcome.component.scss'
})
export class WelcomeComponent implements OnInit {
  private readonly http = inject(HttpClient);

  readonly data = signal<Welcome | null>(null);
  readonly failed = signal(false);
  readonly greeting = computed(() => {
    const h = new Date().getHours();
    return h < 12 ? 'Buenos días' : h < 19 ? 'Buenas tardes' : 'Buenas noches';
  });
  readonly roleLabel = computed(() => ROLE_LABELS[this.data()?.user.role ?? ''] ?? '');
  readonly attendedDigits = computed(() => String(this.data()?.attendedTurnsToday ?? 0).padStart(3, '0').split(''));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.failed.set(false);
    this.http
      .get<ApiResponse<Welcome>>(`${environment.apiUrl}/dashboard/welcome`)
      .pipe(map(r => r.data!))
      .subscribe({ next: d => this.data.set(d), error: () => this.failed.set(true) });
  }
}
