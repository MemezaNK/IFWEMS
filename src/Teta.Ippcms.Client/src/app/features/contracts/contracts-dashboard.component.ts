import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { KpiCardsComponent } from '../../shared/kpi-cards.component';
import { ApiService } from '../../core/api.service';

/** Contract portfolio health: value, expiry exposure, variations and breaches (SRS §5.6, §12). */
@Component({
  selector: 'teta-contracts-dashboard',
  standalone: true,
  imports: [DataTableComponent, KpiCardsComponent],
  template: `
    @if (data(); as d) {
      <div class="page">
        <div class="page-header"><h1>Contracts dashboard</h1></div>
        <teta-kpis [kpis]="[
          { label: 'Active contracts', value: d.active, status: 'Info' },
          { label: 'Active value', value: d.activeValue, unit: 'ZAR', status: 'Info' },
          { label: 'Variation value', value: d.variationValue, unit: 'ZAR', status: 'Info' },
          { label: 'Expiring in 90 days', value: d.expiringIn90Days, status: d.expiringIn90Days > 0 ? 'Amber' : 'Green', link: '/contracts?expiringOnly=true' },
          { label: 'Expired', value: d.expired, status: d.expired > 0 ? 'Red' : 'Green' },
          { label: 'Pending variations', value: d.pendingVariations, status: d.pendingVariations > 0 ? 'Amber' : 'Green' },
          { label: 'Open breaches', value: d.openBreaches, status: d.openBreaches > 0 ? 'Red' : 'Green' },
          { label: 'Average performance', value: d.averagePerformance ?? 0, status: 'Info' }
        ]" />
        <div class="grid cols-2" style="margin-top: 16px">
          <div class="card"><h2 style="margin-top: 0">By status</h2>
            <teta-data-table [columns]="nvColumns" [rows]="d.byStatus" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
          <div class="card"><h2 style="margin-top: 0">Rating distribution</h2>
            <teta-data-table [columns]="nvColumns" [rows]="d.ratingDistribution" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
          <div class="card" style="grid-column: 1 / -1"><h2 style="margin-top: 0">Expiring soon</h2>
            <teta-data-table [columns]="listColumns" [rows]="d.expiring" (rowClick)="open($event)" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
          <div class="card" style="grid-column: 1 / -1"><h2 style="margin-top: 0">Requires attention (breaches / low rating)</h2>
            <teta-data-table [columns]="listColumns" [rows]="d.exceptions" (rowClick)="open($event)" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
        </div>
      </div>
    }
  `
})
export class ContractsDashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly data = signal<any | null>(null);

  nvColumns: Column[] = [{ key: 'name', label: 'Name' }, { key: 'value', label: 'Value', type: 'number' }];
  listColumns: Column[] = [
    { key: 'contractNumber', label: 'Number' }, { key: 'title', label: 'Title' }, { key: 'supplierName', label: 'Supplier' },
    { key: 'currentEndDate', label: 'End date', type: 'date' }, { key: 'daysToExpiry', label: 'Days to expiry', type: 'number' },
    { key: 'openBreaches', label: 'Open breaches', type: 'number' }, { key: 'performanceRating', label: 'Rating', type: 'number' }
  ];

  ngOnInit(): void {
    this.api.get('contracts/dashboard').subscribe(d => this.data.set(d));
  }

  open(row: any): void {
    this.router.navigate(['/contracts', row.id]);
  }
}
