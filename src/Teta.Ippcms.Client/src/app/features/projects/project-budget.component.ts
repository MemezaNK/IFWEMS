import { CurrencyPipe } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';

/** Multi-year budget lines, baseline, revisions, forecasts and the integrated financial view (FR-BUD, FR-FIN-001). */
@Component({
  selector: 'teta-project-budget',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent, CurrencyPipe],
  template: `
    <div style="padding-top: 12px" class="grid">
      @if (summary(); as s) {
        <div class="kpis">
          <div class="kpi Info"><div class="value">{{ s.approvedBudget | currency: 'ZAR' : 'R ' : '1.0-0' }}</div><div class="label">Approved budget</div></div>
          <div class="kpi" [class.Green]="s.reconciled" [class.Red]="!s.reconciled"><div class="value">{{ s.totalRevised | currency: 'ZAR' : 'R ' : '1.0-0' }}</div>
            <div class="label">Revised budget {{ s.reconciled ? '(reconciled)' : '(not reconciled)' }}</div></div>
          <div class="kpi Info"><div class="value">{{ s.committed | currency: 'ZAR' : 'R ' : '1.0-0' }}</div><div class="label">Committed</div></div>
          <div class="kpi Info"><div class="value">{{ s.pendingRequisitions | currency: 'ZAR' : 'R ' : '1.0-0' }}</div><div class="label">Pending requisitions</div></div>
          <div class="kpi" [class.Green]="s.available >= 0" [class.Red]="s.available < 0"><div class="value">{{ s.available | currency: 'ZAR' : 'R ' : '1.0-0' }}</div><div class="label">Available</div></div>
        </div>
        <div class="card">
          <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Budget lines</h2>
            @if (canManage) { <button mat-stroked-button (click)="addLine()"><mat-icon>add</mat-icon> Line</button>
              <button mat-flat-button color="primary" (click)="baseline()">Baseline budget</button> }</div>
          <teta-data-table [columns]="lineColumns" [rows]="s.lines" [actions]="canManage ? lineActions : undefined" [paginate]="false" exportName="budget-lines" />
          <ng-template #lineActions let-row>
            @if (!row.isBaselined) { <button mat-icon-button title="Edit" (click)="editLine(row)"><mat-icon>edit</mat-icon></button> }
            @if (row.isBaselined) { <button mat-button (click)="revise(row)">Revise</button> }
            <button mat-button (click)="forecast(row)">Forecast</button>
          </ng-template>
        </div>
      }
      <div class="card"><h2>Revision history</h2><teta-data-table [columns]="revisionColumns" [rows]="revisions()" [paginate]="false" /></div>
      @if (view(); as v) {
        <div class="card">
          <h2>Financial view (budget · commitments · actuals · accruals · EAC)</h2>
          <div class="kpis">
            <div class="kpi Info"><div class="value">{{ v.actual | currency: 'ZAR' : 'R ' : '1.0-0' }}</div><div class="label">Actual expenditure</div></div>
            <div class="kpi Info"><div class="value">{{ v.accrued | currency: 'ZAR' : 'R ' : '1.0-0' }}</div>
              <div class="label">Accrued <span class="muted" style="font-weight: 400">(manually captured)</span></div></div>
            <div class="kpi Info"><div class="value">{{ v.invoiced | currency: 'ZAR' : 'R ' : '1.0-0' }}</div><div class="label">Invoiced</div></div>
            <div class="kpi Info"><div class="value">{{ v.paid | currency: 'ZAR' : 'R ' : '1.0-0' }}</div><div class="label">Paid</div></div>
            <div class="kpi" [class.Red]="v.variancePercent > 10" [class.Amber]="v.variancePercent > 5 && v.variancePercent <= 10" [class.Green]="v.variancePercent <= 5">
              <div class="value">{{ v.estimateAtCompletion | currency: 'ZAR' : 'R ' : '1.0-0' }}</div><div class="label">Estimate at completion ({{ v.variancePercent }}%)</div></div>
          </div>
          @if (canForecastCost) { <button mat-stroked-button (click)="recordForecast()">Record cost forecast (EAC)</button> }
          <h2 style="margin-top: 12px">Cash flow (planned payments vs actual)</h2>
          <teta-data-table [columns]="cashColumns" [rows]="v.cashflow" [filterable]="false" [paginate]="false" />

          <div class="toolbar-row" style="margin-top: 16px">
            <h2 style="margin: 0; flex: 1">Accruals</h2>
            @if (canManageAccruals) { <button mat-stroked-button (click)="addAccrual()"><mat-icon>add</mat-icon> Accrual</button> }
          </div>
          <p class="muted small" style="margin-top: -6px">
            Manually captured against this project (FR-FIN-008) &mdash; there is no automated ERP feed for accruals yet, unlike commitments, expenditure and payments.
          </p>
          <teta-data-table [columns]="accrualColumns" [rows]="accruals()" [filterable]="false" [paginate]="false"
                            emptyText="No accruals captured for this project yet." />
        </div>
      }
    </div>
  `
})
export class ProjectBudgetComponent implements OnChanges {
  @Input({ required: true }) project!: any;
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  readonly summary = signal<any | null>(null);
  readonly revisions = signal<any[]>([]);
  readonly view = signal<any | null>(null);
  readonly accruals = signal<any[]>([]);

  lineColumns: Column[] = [
    { key: 'financialYear', label: 'FY' }, { key: 'costCategory', label: 'Cost category' }, { key: 'fundingSource', label: 'Funding source' },
    { key: 'originalAmount', label: 'Original', type: 'money' }, { key: 'revisedAmount', label: 'Revised', type: 'money' },
    { key: 'forecastAmount', label: 'Forecast', type: 'money' }, { key: 'isCarryOver', label: 'Carry-over', type: 'bool' }, { key: 'isBaselined', label: 'Baselined', type: 'bool' }
  ];
  revisionColumns: Column[] = [
    { key: 'revisedAtUtc', label: 'When', type: 'datetime' }, { key: 'previousRevised', label: 'Previous', type: 'money' },
    { key: 'newRevised', label: 'New', type: 'money' }, { key: 'reason', label: 'Reason' }, { key: 'revisedBy', label: 'By' }
  ];
  cashColumns: Column[] = [{ key: 'period', label: 'Month' }, { key: 'planned', label: 'Planned', type: 'money' }, { key: 'actual', label: 'Actual', type: 'money' }];
  accrualColumns: Column[] = [
    { key: 'period', label: 'Period' }, { key: 'amount', label: 'Amount', type: 'money' }, { key: 'source', label: 'Source' },
    { key: 'description', label: 'Description' }, { key: 'isReversed', label: 'Reversed', type: 'bool' }
  ];

  get canManage(): boolean { return this.auth.hasAny(P.budgetManage, P.budgetApprove); }
  get canForecastCost(): boolean { return this.auth.hasAny(P.financeManage, P.executionManage); }
  get canManageAccruals(): boolean { return this.auth.has(P.financeManage); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    const id = this.project.id;
    this.api.get(`projects/${id}/budget`).subscribe(s => this.summary.set(s));
    this.api.get<any[]>(`projects/${id}/budget/revisions`).subscribe(r => this.revisions.set(r));
    if (this.auth.has(P.financeRead)) {
      this.api.get(`finance/projects/${id}`).subscribe(v => this.view.set(v));
      this.api.get<any[]>(`finance/projects/${id}/accruals`).subscribe(a => this.accruals.set(a));
    }
  }

  private lineFields() {
    return [
      { key: 'financialYear', label: 'Financial year', required: true },
      { key: 'costCategory', label: 'Cost category', type: 'select' as const, required: true, options: this.refs.category('CostCategory') },
      { key: 'fundingSource', label: 'Funding source', type: 'select' as const, required: true, options: this.refs.category('FundingSource') },
      { key: 'amount', label: 'Amount (R)', type: 'number' as const, required: true, min: 0 },
      { key: 'isCarryOver', label: 'Carry-over from a previous year', type: 'checkbox' as const },
      { key: 'carryOverFromYear', label: 'Carried over from FY' }
    ];
  }

  async addLine(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Budget line', fields: this.lineFields(), value: { financialYear: '2026/27' } });
    if (v) this.api.post(`projects/${this.project.id}/budget/lines`, v).subscribe(() => this.load());
  }

  async editLine(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Budget line', fields: this.lineFields(), value: { ...row, amount: row.originalAmount } });
    if (v) this.api.put(`projects/${this.project.id}/budget/lines/${row.id}`, v).subscribe(() => this.load());
  }

  baseline(): void {
    this.api.post(`projects/${this.project.id}/budget/baseline`).subscribe(() => this.load());
  }

  async revise(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Revise baselined budget line', intro: 'The original amount stays on record; the revision is audited (FR-BUD-002).',
      fields: [{ key: 'newRevisedAmount', label: 'New revised amount', type: 'number', required: true, min: 0 },
        { key: 'reason', label: 'Reason', type: 'textarea', required: true }], value: { newRevisedAmount: row.revisedAmount } }, '560px');
    if (v) this.api.post(`projects/${this.project.id}/budget/lines/${row.id}/revise`, v).subscribe(() => this.load());
  }

  async forecast(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Forecast', fields: [{ key: 'forecastAmount', label: 'Forecast amount', type: 'number', required: true, min: 0 }],
      value: { forecastAmount: row.forecastAmount } }, '480px');
    if (v) this.api.post(`projects/${this.project.id}/budget/lines/${row.id}/forecast`, v).subscribe(() => this.load());
  }

  async recordForecast(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Estimate at completion', fields: [
      { key: 'estimateAtCompletion', label: 'Estimate at completion (R)', type: 'number', required: true, min: 0 },
      { key: 'commentary', label: 'Commentary', type: 'textarea', required: true }] }, '560px');
    if (v) this.api.post(`finance/projects/${this.project.id}/forecasts`, v).subscribe(() => this.load());
  }

  async addAccrual(): Promise<void> {
    const v = await openForm(this.dialog, {
      title: 'Add accrual', intro: 'Captured manually — the ERP interface does not send an accrual message type yet (FR-FIN-008), so this is the only way an accrual enters the financial view.',
      fields: [
        { key: 'period', label: 'Period (e.g. 2026/27 Q3)', required: true },
        { key: 'amount', label: 'Amount (R)', type: 'number', required: true, min: 0 },
        { key: 'description', label: 'Description', type: 'textarea' }
      ]
    }, '520px');
    if (v) this.api.post(`finance/accruals`, { ...v, projectId: this.project.id }).subscribe(() => this.load());
  }
}
