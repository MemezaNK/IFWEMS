import { DecimalPipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Kpi } from '../core/models';

/** KPI tiles; each tile drills down to the transactions behind it (FR-REP-002). */
@Component({
  selector: 'teta-kpis',
  standalone: true,
  imports: [RouterLink, DecimalPipe],
  template: `
    <div class="kpis">
      @for (k of kpis; track k.label) {
        <a class="kpi" [class]="'kpi ' + k.status" [routerLink]="path(k)" [queryParams]="query(k)">
          <div class="value">
            @if (k.unit === 'ZAR') { R {{ k.value | number: '1.0-0' }} } @else { {{ k.value | number: '1.0-1' }}{{ k.unit === '%' ? '%' : '' }} }
          </div>
          <div class="label">{{ k.label }}</div>
        </a>
      }
    </div>
  `
})
export class KpiCardsComponent {
  @Input() kpis: Kpi[] = [];

  private link(k: Kpi): string {
    return k.drillLink ?? k.link ?? '/';
  }

  path(k: Kpi): string {
    return this.link(k).split('?')[0];
  }

  query(k: Kpi): Record<string, string> {
    const q = this.link(k).split('?')[1];
    const result: Record<string, string> = {};
    if (q) new URLSearchParams(q).forEach((v, key) => (result[key] = v));
    return result;
  }
}
