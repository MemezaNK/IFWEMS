import { DecimalPipe } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Kpi } from '../core/models';

/** Keyword -> icon lookup for KPI tiles, checked in order against the (lowercased) label.
    Purely a client-side presentation choice -- the API doesn't carry an icon field, so this
    infers a sensible glyph from what the KPI is actually about rather than showing the same
    generic icon everywhere. */
const ICON_RULES: Array<[RegExp, string]> = [
  [/budget|expenditure|spen(d|t)|committed|cost/, 'payments'],
  [/risk/, 'gpp_maybe'],
  [/audit/, 'fact_check'],
  [/overdue|expir|late|due/, 'schedule'],
  [/approval|waiting|awaiting/, 'pending_actions'],
  [/action/, 'task_alt'],
  [/contract/, 'description'],
  [/procurement|bid|tender/, 'shopping_cart'],
  [/complian|quality|verified/, 'verified'],
  [/finding|issue|exception|error/, 'report_problem'],
  [/visit|monitoring/, 'visibility'],
  [/indicator|on track|progress/, 'trending_up'],
  [/project|programme/, 'folder_open']
];

/** KPI tiles; each tile drills down to the transactions behind it (FR-REP-002). */
@Component({
  selector: 'teta-kpis',
  standalone: true,
  imports: [RouterLink, DecimalPipe, MatIconModule],
  template: `
    <div class="kpis">
      @for (k of kpis; track k.label) {
        <a class="kpi" [class]="'kpi ' + k.status" [routerLink]="selectable(k) ? null : path(k)" [queryParams]="query(k)"
           [style.cursor]="selectable(k) ? 'pointer' : null" (click)="select(k)">
          <div class="kpi-icon"><mat-icon>{{ icon(k) }}</mat-icon></div>
          <div class="kpi-body">
            <div class="value">
              @if (k.unit === 'ZAR') { R {{ k.value | number: '1.0-0' }} } @else { {{ k.value | number: '1.0-1' }}{{ k.unit === '%' ? '%' : '' }} }
            </div>
            <div class="label">{{ k.label }}</div>
          </div>
        </a>
      }
    </div>
  `
})
export class KpiCardsComponent {
  @Input() kpis: Kpi[] = [];
  /** KPI codes that raise <c>kpiSelect</c> on click instead of navigating to their drill link. */
  @Input() selectableCodes: string[] = [];
  @Output() kpiSelect = new EventEmitter<Kpi>();

  selectable(k: Kpi): boolean {
    return !!k.code && this.selectableCodes.includes(k.code);
  }

  select(k: Kpi): void {
    if (this.selectable(k)) this.kpiSelect.emit(k);
  }

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

  icon(k: Kpi): string {
    const label = (k.label ?? '').toLowerCase();
    for (const [pattern, name] of ICON_RULES) {
      if (pattern.test(label)) return name;
    }
    if (k.unit === 'ZAR') return 'payments';
    if (k.unit === '%') return 'trending_up';
    return 'insights';
  }
}
