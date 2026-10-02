import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { KpiCardsComponent } from '../../shared/kpi-cards.component';

/**
 * User Activity Report (admin-level): "done vs to-do" stats across all active users, or a
 * drill-down into a specific user's completed and outstanding items, with PDF export.
 */
@Component({
  selector: 'teta-user-activity-report',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatSelectModule, MatTabsModule, DataTableComponent, KpiCardsComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>User Activity Report</h1>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="min-width: 280px">
          <mat-label>User</mat-label>
          <mat-select [(ngModel)]="userId" (ngModelChange)="load()">
            <mat-option [value]="undefined">All users (summary)</mat-option>
            @for (u of users(); track u.value) { <mat-option [value]="u.value">{{ u.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <button mat-stroked-button (click)="export()"><mat-icon>download</mat-icon> Export PDF</button>
      </div>

      @if (report(); as r) {
        @if (r.detail; as d) {
          <div class="card" style="margin-bottom: 16px">
            <teta-kpis [kpis]="detailKpis(d.summary)" />
          </div>
          <mat-tab-group>
            <mat-tab label="Completed">
              <teta-data-table [columns]="itemColumns" [rows]="d.completed" emptyText="No completed items in this period." exportName="activity-completed" />
            </mat-tab>
            <mat-tab label="Pending">
              <teta-data-table [columns]="itemColumns" [rows]="d.pending" emptyText="No outstanding items." exportName="activity-pending" />
            </mat-tab>
          </mat-tab-group>
        } @else {
          <div class="card">
            <h2 style="margin-top: 0">All users</h2>
            <teta-data-table [columns]="summaryColumns" [rows]="r.users" emptyText="No activity recorded." exportName="user-activity-summary" />
          </div>
        }
      }
    </div>
  `
})
export class UserActivityReportComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly refs = inject(ReferenceService);

  userId: string | undefined;
  readonly users = signal<{ value: any; label: string }[]>([]);
  readonly report = signal<any | null>(null);

  summaryColumns: Column[] = [
    { key: 'username', label: 'Username' },
    { key: 'displayName', label: 'Name' },
    { key: 'roles', label: 'Roles', value: row => (row.roles ?? []).join(', ') },
    { key: 'tasksCompletedTotal', label: 'Completed', type: 'number' },
    { key: 'tasksPendingTotal', label: 'Pending', type: 'number' },
    { key: 'auditActions', label: 'Audit Actions', type: 'number' },
    { key: 'lastActivityAtUtc', label: 'Last Activity', type: 'datetime' },
    { key: 'isActive', label: 'Active', type: 'bool' }
  ];
  itemColumns: Column[] = [
    { key: 'type', label: 'Type' },
    { key: 'reference', label: 'Reference' },
    { key: 'detail', label: 'Detail' },
    { key: 'status', label: 'Status', type: 'status' },
    { key: 'occurredAtUtc', label: 'Date', type: 'datetime' }
  ];

  ngOnInit(): void {
    this.refs.users().subscribe(u => this.users.set(u));
    this.load();
  }

  load(): void {
    this.api.get('reports/user-activity', { userId: this.userId }).subscribe(r => this.report.set(r));
  }

  export(): void {
    this.api.download('reports/user-activity/export', { userId: this.userId, format: 'pdf' })
      .subscribe(r => ApiService.saveBlob(r, 'user-activity-report.pdf'));
  }

  detailKpis(summary: any): { label: string; value: number; unit?: string; status: string }[] {
    return [
      { label: 'Tasks Completed', value: summary.tasksCompletedTotal, status: 'NotConfigured' },
      { label: 'Tasks Pending', value: summary.tasksPendingTotal, status: summary.tasksPendingTotal > 0 ? 'Amber' : 'Green' },
      { label: 'Decisions Made', value: summary.decisionsMade, status: 'NotConfigured' },
      { label: 'Audit Actions', value: summary.auditActions, status: 'NotConfigured' }
    ];
  }
}
