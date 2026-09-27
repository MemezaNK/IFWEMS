import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { KpiCardsComponent } from '../../shared/kpi-cards.component';

/** M&E pipeline: visits, findings and corrective actions, beneficiary reach (SRS §5.9). */
@Component({
  selector: 'teta-monitoring-dashboard',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule, DataTableComponent, KpiCardsComponent],
  template: `
    @if (data(); as d) {
      <div class="page">
        <div class="page-header">
          <h1>M&amp;E dashboard</h1>
          <a mat-stroked-button routerLink="/me/visits"><mat-icon>place</mat-icon> Visits</a>
          <a mat-stroked-button routerLink="/me/beneficiaries"><mat-icon>diversity_3</mat-icon> Beneficiaries</a>
        </div>
        <teta-kpis [kpis]="[
          { label: 'Visits scheduled', value: d.visitsScheduled, status: 'Info' },
          { label: 'Visits completed', value: d.visitsCompleted, status: 'Green' },
          { label: 'Visits overdue', value: d.visitsOverdue, status: d.visitsOverdue > 0 ? 'Red' : 'Green' },
          { label: 'Open findings', value: d.openFindings, status: d.openFindings > 0 ? 'Amber' : 'Green', link: '/me/actions' },
          { label: 'Critical findings', value: d.criticalFindings, status: d.criticalFindings > 0 ? 'Red' : 'Green' },
          { label: 'Overdue actions', value: d.overdueActions, status: d.overdueActions > 0 ? 'Red' : 'Green', link: '/me/actions' },
          { label: 'Beneficiaries reached', value: d.beneficiaries, status: 'Info', link: '/me/beneficiaries' },
          { label: 'Completions', value: d.completions, status: 'Green' },
          { label: 'Potential duplicates', value: d.potentialDuplicates, status: d.potentialDuplicates > 0 ? 'Amber' : 'Green' }
        ]" />
        <div class="grid cols-2" style="margin-top: 16px">
          <div class="card"><h2 style="margin-top: 0">Findings by severity</h2>
            <teta-data-table [columns]="countColumns" [rows]="d.findingsBySeverity" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
          <div class="card"><h2 style="margin-top: 0">Beneficiaries by status</h2>
            <teta-data-table [columns]="countColumns" [rows]="d.beneficiariesByStatus" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
          <div class="card" style="grid-column: 1 / -1"><h2 style="margin-top: 0">Overdue corrective actions</h2>
            <teta-data-table [columns]="actionColumns" [rows]="d.overdueActionList" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
        </div>
      </div>
    }
  `
})
export class MonitoringDashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly data = signal<any | null>(null);

  countColumns: Column[] = [{ key: 'name', label: 'Name' }, { key: 'count', label: 'Count', type: 'number' }];
  actionColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'description', label: 'Description' }, { key: 'ownerName', label: 'Owner' },
    { key: 'dueDate', label: 'Due', type: 'date' }, { key: 'escalationLevel', label: 'Escalation', type: 'number' }
  ];

  ngOnInit(): void {
    this.api.get('me/dashboard').subscribe(d => this.data.set(d));
  }
}
