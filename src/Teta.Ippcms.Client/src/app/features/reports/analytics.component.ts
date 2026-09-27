import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { ReportTable } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { ReportTableComponent } from '../../shared/report-table.component';

/** Cross-portfolio analytics and the geographic distribution of projects, budget and beneficiaries (FR-REP-005/006, with minimum-group-size suppression). */
@Component({
  selector: 'teta-analytics',
  standalone: true,
  imports: [FormsModule, MatFormFieldModule, MatSelectModule, MatTabsModule, DataTableComponent, ReportTableComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>Analytics &amp; map</h1></div>
      <mat-tab-group animationDuration="0">
        <mat-tab label="Analytics">
          <div style="padding-top: 12px">
            @if (analytics(); as t) { <teta-report-table [table]="t" /> }
          </div>
        </mat-tab>
        <mat-tab label="Geographic distribution">
          <div style="padding-top: 12px">
            <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 200px">
              <mat-label>Level</mat-label>
              <mat-select [(ngModel)]="level" (ngModelChange)="loadGeo()">
                <mat-option value="Province">Province</mat-option>
                <mat-option value="District">District</mat-option>
                <mat-option value="Municipality">Municipality</mat-option>
              </mat-select>
            </mat-form-field>
            @if (geo(); as g) {
              <p class="small muted">Minimum group size for reporting: {{ g.minimumGroupSize }}. Projects without location: {{ g.projectsWithoutLocation }}.</p>
              <teta-data-table [columns]="geoColumns" [rows]="g.areas" emptyText="No geographic data." exportName="geographic-distribution" />
            }
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `
})
export class AnalyticsComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly analytics = signal<ReportTable | null>(null);
  readonly geo = signal<any | null>(null);
  level = 'Province';

  geoColumns: Column[] = [
    { key: 'province', label: 'Province' }, { key: 'district', label: 'District' }, { key: 'municipality', label: 'Municipality' },
    { key: 'projects', label: 'Projects', type: 'number' }, { key: 'budget', label: 'Budget', type: 'money' },
    { key: 'beneficiaries', label: 'Beneficiaries', type: 'number' }, { key: 'suppressed', label: 'Suppressed (below minimum group size)', type: 'bool' }
  ];

  ngOnInit(): void {
    this.api.get<ReportTable>('reports/analytics').subscribe(t => this.analytics.set(t));
    this.loadGeo();
  }

  loadGeo(): void {
    this.api.get('reports/geographic', { level: this.level }).subscribe(g => this.geo.set(g));
  }
}
