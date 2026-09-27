import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { Router } from '@angular/router';

/** Cross-cutting register of at-risk items — over-budget, overdue milestones, aged procurements, expiring contracts, unpaid invoices (FR-REP-004). */
@Component({
  selector: 'teta-exceptions-report',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Exceptions</h1>
        <button mat-stroked-button (click)="export()"><mat-icon>download</mat-icon> Export</button>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="open($event)" emptyText="No exceptions found." exportName="exceptions" />
    </div>
  `
})
export class ExceptionsReportComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly rows = signal<any[]>([]);

  columns: Column[] = [
    { key: 'category', label: 'Category' }, { key: 'reference', label: 'Reference' }, { key: 'description', label: 'Description' },
    { key: 'severity', label: 'Severity', type: 'status' }, { key: 'projectReference', label: 'Project' }, { key: 'dueDate', label: 'Due', type: 'date' },
    { key: 'daysOverdue', label: 'Days overdue', type: 'number' }
  ];

  ngOnInit(): void {
    this.api.get<any[]>('reports/exceptions').subscribe(r => this.rows.set(r));
  }

  open(row: any): void {
    if (row.link) this.router.navigateByUrl(row.link);
  }

  export(): void {
    this.api.download('reports/exceptions/export', { format: 'xlsx' }).subscribe(r => ApiService.saveBlob(r, 'exceptions.xlsx'));
  }
}
