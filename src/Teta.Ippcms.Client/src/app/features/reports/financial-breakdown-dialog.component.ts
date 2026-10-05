import { CurrencyPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { Column, DataTableComponent } from '../../shared/data-table.component';

export interface FinancialBreakdownData {
  /** 'BUDGET' shows the revised budget per project; 'COMMITTED' shows open commitments per project. */
  metric: 'BUDGET' | 'COMMITTED';
  breakdown: {
    financialYear: string;
    projects: Array<{ projectId: string; reference: string; name: string; revisedBudget: number; committed: number }>;
    totalRevisedBudget: number;
    totalCommitted: number;
  };
}

/** Per-project breakdown behind the Revised budget and Committed tiles on the executive dashboard. */
@Component({
  selector: 'teta-financial-breakdown-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, CurrencyPipe, DataTableComponent],
  template: `
    <h2 mat-dialog-title>{{ title }} per project &ndash; {{ data.breakdown.financialYear }}</h2>
    <mat-dialog-content>
      <teta-data-table [columns]="columns" [rows]="rows" [filterable]="false" [paginate]="false" [exportable]="false" emptyText="No projects have a figure for this financial year." />
      <p class="total"><strong>Total:</strong> {{ total | currency: 'ZAR' : 'R ' : '1.2-2' }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end"><button mat-button mat-dialog-close>Close</button></mat-dialog-actions>
  `,
  styles: [`.total { text-align: right; margin: 12px 16px 0; }`]
})
export class FinancialBreakdownDialog {
  readonly data = inject<FinancialBreakdownData>(MAT_DIALOG_DATA);
  readonly isBudget = this.data.metric === 'BUDGET';
  readonly title = this.isBudget ? 'Revised budget' : 'Committed';
  readonly total = this.isBudget ? this.data.breakdown.totalRevisedBudget : this.data.breakdown.totalCommitted;
  readonly rows = this.data.breakdown.projects
    .filter(p => (this.isBudget ? p.revisedBudget : p.committed) !== 0)
    .map(p => ({ reference: p.reference, name: p.name, amount: this.isBudget ? p.revisedBudget : p.committed }));
  readonly columns: Column[] = [
    { key: 'reference', label: 'Reference' }, { key: 'name', label: 'Project' }, { key: 'amount', label: this.title, type: 'money' }
  ];
}
