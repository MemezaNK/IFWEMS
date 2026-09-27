import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { KpiCardsComponent } from '../../shared/kpi-cards.component';

/** Portfolio-wide financial position: budget, commitments, accruals, cashflow and invoice pipeline health (SRS §5.7). */
@Component({
  selector: 'teta-finance-dashboard',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule, DataTableComponent, KpiCardsComponent],
  template: `
    @if (data(); as d) {
      <div class="page">
        <div class="page-header">
          <h1>Finance dashboard</h1>
          <a mat-stroked-button routerLink="/finance/invoices"><mat-icon>receipt_long</mat-icon> Invoices</a>
          @if (canErp) { <a mat-stroked-button routerLink="/finance/erp"><mat-icon>sync_alt</mat-icon> ERP interface</a> }
        </div>
        <teta-kpis [kpis]="[
          { label: 'Approved budget', value: d.portfolio.approvedBudget, unit: 'ZAR', status: 'Info' },
          { label: 'Committed', value: d.portfolio.committed, unit: 'ZAR', status: 'Info' },
          { label: 'Invoiced', value: d.portfolio.invoiced, unit: 'ZAR', status: 'Info' },
          { label: 'Paid', value: d.portfolio.paid, unit: 'ZAR', status: 'Info' },
          { label: 'Available', value: d.portfolio.available, unit: 'ZAR', status: d.portfolio.available < 0 ? 'Red' : 'Green' },
          { label: 'Pending certification', value: d.invoicesPendingCertification, status: d.invoicesPendingCertification > 0 ? 'Amber' : 'Green', link: '/finance/invoices?status=Submitted' },
          { label: 'Failed validation', value: d.invoicesValidationFailed, status: d.invoicesValidationFailed > 0 ? 'Red' : 'Green' },
          { label: 'Potential duplicates', value: d.potentialDuplicates, status: d.potentialDuplicates > 0 ? 'Amber' : 'Green' },
          { label: 'ERP errors', value: d.erpErrors, status: d.erpErrors > 0 ? 'Red' : 'Green' },
          { label: 'Unbalanced batches', value: d.unbalancedBatches, status: d.unbalancedBatches > 0 ? 'Red' : 'Green' }
        ]" />
        <div class="card" style="margin-top: 16px">
          <h2 style="margin-top: 0">Cashflow (planned vs actual)</h2>
          <teta-data-table [columns]="cashflowColumns" [rows]="d.portfolio.cashflow" [filterable]="false" [paginate]="false" [exportable]="false" />
        </div>
        <div class="card" style="margin-top: 16px">
          <h2 style="margin-top: 0">By project</h2>
          <teta-data-table [columns]="projectColumns" [rows]="d.portfolio.projects" [filterable]="false" [paginate]="false" exportName="finance-by-project" />
        </div>
      </div>
    }
  `
})
export class FinanceDashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  readonly data = signal<any | null>(null);

  cashflowColumns: Column[] = [{ key: 'period', label: 'Period' }, { key: 'planned', label: 'Planned', type: 'money' }, { key: 'actual', label: 'Actual', type: 'money' }];
  projectColumns: Column[] = [
    { key: 'reference', label: 'Reference' }, { key: 'name', label: 'Project' }, { key: 'budget', label: 'Budget', type: 'money' },
    { key: 'committed', label: 'Committed', type: 'money' }, { key: 'actual', label: 'Actual', type: 'money' },
    { key: 'estimateAtCompletion', label: 'EAC', type: 'money' }, { key: 'variancePercent', label: 'Variance', type: 'percent' }, { key: 'health', label: 'Health', type: 'status' }
  ];

  get canErp(): boolean { return this.auth.has(P.financeErp); }

  ngOnInit(): void {
    this.api.get('finance/dashboard').subscribe(d => this.data.set(d));
  }
}
