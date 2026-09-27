import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { KpiCardsComponent } from '../../shared/kpi-cards.component'; // exports teta-kpis

/** Procurement pipeline KPIs, ageing, method mix and exposure to delay (SRS §5.4, §12). */
@Component({
  selector: 'teta-procurement-dashboard',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, RouterLink, DataTableComponent, KpiCardsComponent],
  template: `
    @if (data(); as d) {
      <div class="page">
        <div class="page-header">
          <h1>Procurement dashboard</h1>
          <a mat-stroked-button routerLink="/procurement"><mat-icon>gavel</mat-icon> All procurements</a>
          <a mat-stroked-button routerLink="/procurement/transparency"><mat-icon>visibility</mat-icon> Transparency register</a>
        </div>
        <teta-kpis [kpis]="[
          { label: 'Total processes', value: d.total, status: 'Info' },
          { label: 'Total estimated value', value: d.totalEstimatedValue, unit: 'ZAR', status: 'Info' },
          { label: 'Delayed', value: d.delayed, status: d.delayed > 0 ? 'Amber' : 'Green', link: '/procurement?status=Published' },
          { label: 'Pending deviations', value: d.pendingExceptions, status: d.pendingExceptions > 0 ? 'Amber' : 'Green', link: '/procurement/exceptions' },
          { label: 'Approved deviations', value: d.approvedExceptions, status: 'Info' },
          { label: 'Late bids', value: d.lateBids, status: d.lateBids > 0 ? 'Red' : 'Green' }
        ]" />

        <div class="grid cols-2" style="margin-top: 16px">
          <div class="card">
            <h2 style="margin-top: 0">By status</h2>
            <teta-data-table [columns]="statusColumns" [rows]="d.byStatus" [filterable]="false" [paginate]="false" [exportable]="false" />
          </div>
          <div class="card">
            <h2 style="margin-top: 0">By method</h2>
            <teta-data-table [columns]="methodColumns" [rows]="d.byMethod" [filterable]="false" [paginate]="false" [exportable]="false" />
          </div>
          <div class="card">
            <h2 style="margin-top: 0">Ageing</h2>
            <teta-data-table [columns]="ageColumns" [rows]="d.ageing" [filterable]="false" [paginate]="false" [exportable]="false" />
          </div>
          <div class="card" style="grid-column: 1 / -1">
            <h2 style="margin-top: 0">Oldest open processes</h2>
            <teta-data-table [columns]="listColumns" [rows]="d.oldest" (rowClick)="open($event)" [filterable]="false" [paginate]="false" [exportable]="false" />
          </div>
          <div class="card" style="grid-column: 1 / -1">
            <h2 style="margin-top: 0">Delayed against plan</h2>
            <teta-data-table [columns]="listColumns" [rows]="d.delayedItems" (rowClick)="open($event)" [filterable]="false" [paginate]="false" [exportable]="false" />
          </div>
        </div>
      </div>
    }
  `
})
export class ProcurementDashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly data = signal<any | null>(null);

  statusColumns: Column[] = [{ key: 'status', label: 'Status', type: 'status' }, { key: 'count', label: 'Count', type: 'number' }, { key: 'value', label: 'Value', type: 'money' }];
  methodColumns: Column[] = [{ key: 'method', label: 'Method' }, { key: 'count', label: 'Count', type: 'number' }, { key: 'value', label: 'Value', type: 'money' }];
  ageColumns: Column[] = [{ key: 'bucket', label: 'Age bucket' }, { key: 'count', label: 'Count', type: 'number' }];
  listColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'title', label: 'Title' }, { key: 'method', label: 'Method' },
    { key: 'estimatedValue', label: 'Estimated value', type: 'money' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'daysOpen', label: 'Days open', type: 'number' }
  ];

  ngOnInit(): void {
    this.api.get('procurement/dashboard').subscribe(d => this.data.set(d));
  }

  open(row: any): void {
    this.router.navigate(['/procurement', row.id]);
  }
}
