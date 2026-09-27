import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { KpiCardsComponent } from '../../shared/kpi-cards.component';

/** Combined risk and assurance position: heat map, top risks and overdue audit findings (SRS §5.9, §12). */
@Component({
  selector: 'teta-assurance-dashboard',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule, DataTableComponent, KpiCardsComponent],
  template: `
    @if (data(); as d) {
      <div class="page">
        <div class="page-header">
          <h1>Risk &amp; assurance</h1>
          <a mat-stroked-button routerLink="/assurance/risks"><mat-icon>warning</mat-icon> Risk register</a>
          <a mat-stroked-button routerLink="/assurance/compliance"><mat-icon>verified_user</mat-icon> Compliance</a>
          <a mat-stroked-button routerLink="/assurance/audit-findings"><mat-icon>policy</mat-icon> Audit findings</a>
        </div>
        <teta-kpis [kpis]="[
          { label: 'Open risks', value: d.openRisks, status: 'Info', link: '/assurance/risks' },
          { label: 'Critical risks', value: d.criticalRisks, status: d.criticalRisks > 0 ? 'Red' : 'Green' },
          { label: 'High risks', value: d.highRisks, status: d.highRisks > 0 ? 'Amber' : 'Green' },
          { label: 'Overdue treatments', value: d.overdueTreatments, status: d.overdueTreatments > 0 ? 'Red' : 'Green' },
          { label: 'Overdue reviews', value: d.overdueReviews, status: d.overdueReviews > 0 ? 'Amber' : 'Green' },
          { label: 'Open audit findings', value: d.openAuditFindings, status: d.openAuditFindings > 0 ? 'Amber' : 'Green', link: '/assurance/audit-findings' },
          { label: 'Overdue audit actions', value: d.overdueAuditActions, status: d.overdueAuditActions > 0 ? 'Red' : 'Green' },
          { label: 'Non-compliant attestations', value: d.nonCompliantAttestations, status: d.nonCompliantAttestations > 0 ? 'Red' : 'Green', link: '/assurance/compliance' },
          { label: 'Coverage gaps', value: d.coverageGaps, status: d.coverageGaps > 0 ? 'Amber' : 'Green' }
        ]" />
        <div class="card" style="margin-top: 16px">
          <h2 style="margin-top: 0">Risk heat map ({{ d.heatMap.basis }})</h2>
          <table class="full-width small">
            <tr class="muted"><td>Likelihood \\ Impact</td>@for (i of [1,2,3,4,5]; track i) { <td>{{ i }}</td> }</tr>
            @for (l of [5,4,3,2,1]; track l) {
              <tr><td>{{ l }}</td>
                @for (i of [1,2,3,4,5]; track i) {
                  <td [style.background]="cell(d.heatMap, l, i)?.colour" style="text-align:center">{{ cell(d.heatMap, l, i)?.count ?? 0 }}</td>
                }
              </tr>
            }
          </table>
        </div>
        <div class="grid cols-2" style="margin-top: 16px">
          <div class="card"><h2 style="margin-top: 0">Top risks</h2>
            <teta-data-table [columns]="riskColumns" [rows]="d.topRisks" (rowClick)="openRisk($event)" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
          <div class="card"><h2 style="margin-top: 0">Overdue audit findings</h2>
            <teta-data-table [columns]="findingColumns" [rows]="d.overdueFindings" [filterable]="false" [paginate]="false" [exportable]="false" /></div>
        </div>
      </div>
    }
  `
})
export class AssuranceDashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly data = signal<any | null>(null);

  riskColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'title', label: 'Title' }, { key: 'residualRating', label: 'Residual rating', type: 'status' },
    { key: 'ownerName', label: 'Owner' }, { key: 'reviewDate', label: 'Review date', type: 'date' }
  ];
  findingColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'title', label: 'Title' }, { key: 'rating', label: 'Rating', type: 'status' }, { key: 'dueDate', label: 'Due', type: 'date' }
  ];

  ngOnInit(): void {
    this.api.get('assurance/dashboard').subscribe(d => this.data.set(d));
  }

  cell(heatMap: any, likelihood: number, impact: number): any {
    return heatMap.cells.find((c: any) => c.likelihood === likelihood && c.impact === impact);
  }

  openRisk(row: any): void {
    this.router.navigate(['/assurance/risks', row.id]);
  }
}
