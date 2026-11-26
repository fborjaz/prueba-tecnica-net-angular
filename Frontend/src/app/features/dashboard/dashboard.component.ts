import { HttpClient, HttpParams } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, DashboardIndicators } from '../../core/models/api.models';
import { NotifyService } from '../../core/services/notify.service';

const iso = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

const daysAgo = (n: number) => {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return iso(d);
};

type Preset = 'today' | '7' | '30';

/** Indicadores para el administrador (sp_dashboard_indicators). */
@Component({
  selector: 'app-dashboard',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotifyService);

  readonly data = signal<DashboardIndicators | null>(null);
  readonly from = signal(daysAgo(6));
  readonly to = signal(iso(new Date()));
  readonly preset = signal<Preset | null>('7');

  readonly sessionShare = computed(() => {
    const u = this.data()?.users;
    return u && u.totalUsers ? Math.round((u.activeSessions / u.totalUsers) * 100) : 0;
  });
  readonly maxCashAttended = computed(() => Math.max(1, ...(this.data()?.cashes ?? []).map(c => c.turnsGenerated)));
  readonly totals = computed(() => {
    const daily = this.data()?.daily ?? [];
    return {
      generated: daily.reduce((s, d) => s + d.turnsGenerated, 0),
      attended: daily.reduce((s, d) => s + d.turnsAttended, 0)
    };
  });

  ngOnInit(): void {
    this.load();
  }

  applyPreset(preset: Preset): void {
    this.preset.set(preset);
    this.to.set(iso(new Date()));
    this.from.set(preset === 'today' ? iso(new Date()) : daysAgo(Number(preset) - 1));
    this.load();
  }

  setRange(kind: 'from' | 'to', value: string): void {
    if (!value) return;
    (kind === 'from' ? this.from : this.to).set(value);
    this.preset.set(null);
    if (this.from() > this.to()) {
      this.notify.error('La fecha inicial no puede ser mayor que la final.');
      return;
    }
    this.load();
  }

  load(): void {
    const params = new HttpParams().set('from', this.from()).set('to', this.to());
    this.http
      .get<ApiResponse<DashboardIndicators>>(`${environment.apiUrl}/dashboard/indicators`, { params })
      .pipe(map(r => r.data!))
      .subscribe(d => this.data.set(d));
  }
}
