import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/auth.service';
import { ApiService } from '../../core/api.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { KpiCardsComponent } from '../../shared/kpi-cards.component';

/** Executive dashboard and the full standard report catalogue (RPT-001..015, SRS §5.10/§11). */
@Component({
  selector: 'teta-reports-catalogue',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule, DataTableComponent, KpiCardsComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Reports</h1>
        <a mat-stroked-button routerLink="/reports/exceptions"><mat-icon>crisis_alert</mat-icon> Exceptions</a>
        <a mat-stroked-button routerLink="/reports/board-packs"><mat-icon>menu_book</mat-icon> Board packs</a>
        <a mat-stroked-button routerLink="/reports/analytics"><mat-icon>map</mat-icon> Analytics &amp; map</a>
        <a mat-stroked-button routerLink="/reports/data-quality"><mat-icon>cleaning_services</mat-icon> Data quality</a>
        <a mat-stroked-button routerLink="/reports/learner-delivery"><mat-icon>school</mat-icon> Learner delivery</a>
      </div>
      @if (dashboard(); as d) {
        <teta-kpis [kpis]="d.kpis" />
        <div class="grid cols-2" style="margin-top: 16px">
          <div class="card"><h2 style="margin-top: 0">Health</h2>
            <teta-data-table [columns]="healthColumns" [rows]="d.health" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
          <div class="card"><h2 style="margin-top: 0">Top exceptions</h2>
            <teta-data-table [columns]="exceptionColumns" [rows]="d.topExceptions" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
          <div class="card" style="grid-column: 1 / -1"><h2 style="margin-top: 0">Programme roll-up</h2>
            <teta-data-table [columns]="programmeColumns" [rows]="d.programmes" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
        </div>
      }
      <div class="card" style="margin-top: 16px">
        <h2 style="margin-top: 0">Report catalogue</h2>
        <teta-data-table [columns]="catalogueColumns" [rows]="catalogue()" (rowClick)="run($event)" emptyText="No reports available." [exportable]="false" />
      </div>
    </div>
  `
})
export class ReportsCatalogueComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly dashboard = signal<any | null>(null);
  readonly catalogue = signal<any[]>([]);

  healthColumns: Column[] = [{ key: 'health', label: 'Health', type: 'status' }, { key: 'count', label: 'Count', type: 'number' }];
  exceptionColumns: Column[] = [
    { key: 'category', label: 'Category' }, { key: 'reference', label: 'Reference' }, { key: 'description', label: 'Description' },
    { key: 'severity', label: 'Severity', type: 'status' }, { key: 'daysOverdue', label: 'Days overdue', type: 'number' }
  ];
  programmeColumns: Column[] = [
    { key: 'name', label: 'Programme' }, { key: 'portfolioName', label: 'Portfolio' }, { key: 'projects', label: 'Projects', type: 'number' },
    { key: 'red', label: 'Red', type: 'number' }, { key: 'amber', label: 'Amber', type: 'number' }, { key: 'green', label: 'Green', type: 'number' },
    { key: 'budget', label: 'Budget', type: 'money' }, { key: 'actual', label: 'Actual', type: 'money' }
  ];
  catalogueColumns: Column[] = [
    { key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'audience', label: 'Audience' }, { key: 'frequency', label: 'Frequency' },
    { key: 'description', label: 'Description' }
  ];

  ngOnInit(): void {
    if (this.auth.hasAny(P.reportsRead, P.reportsBoard, P.portfolioRead)) {
      this.api.get('reports/executive-dashboard').subscribe(d => this.dashboard.set(d));
    }
    this.api.get<any[]>('reports/catalogue').subscribe(r => this.catalogue.set(r));
  }

  run(row: any): void {
    this.router.navigate(['/reports/run', row.code]);
  }
}
