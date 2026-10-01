import { Component, Input, OnChanges, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { KpiCardsComponent } from '../../shared/kpi-cards.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { Field, openForm } from '../../shared/form-dialog.component';

/**
 * Interactive Learner Delivery & Monitoring Report (TETA-IPPCMS report spec, sections A-N): a
 * project-scoped, tab-based drill-down dashboard mirroring the board-report structure, plus
 * consolidated PDF/XLSX export. Learner identity (name/ID) is never shown here - only reference
 * numbers - per the spec's own instruction that CEO/Board reporting must use aggregated learner
 * information and that individual records require authorised, privacy-controlled access.
 */
@Component({
  selector: 'teta-learner-delivery-report',
  standalone: true,
  imports: [
    FormsModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatSelectModule, MatMenuModule, MatTabsModule,
    DataTableComponent, KpiCardsComponent, StatusChipComponent, DatePipe, DecimalPipe
  ],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Learner Delivery &amp; Monitoring Report</h1>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="min-width: 280px">
          <mat-label>Project</mat-label>
          <mat-select [(ngModel)]="projectId" (ngModelChange)="selectProject($event)">
            @for (p of projects(); track p.value) { <mat-option [value]="p.value">{{ p.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
        @if (projectId) {
          @if (canManageTargets) {
            <button mat-stroked-button (click)="configureTargets()"><mat-icon>tune</mat-icon> Configure targets</button>
          }
          <button mat-stroked-button [matMenuTriggerFor]="exportMenu"><mat-icon>download</mat-icon> Export</button>
          <mat-menu #exportMenu="matMenu">
            <button mat-menu-item (click)="exportAs('pdf')">PDF (board report)</button>
            <button mat-menu-item (click)="exportAs('xlsx')">Excel (all sections)</button>
          </mat-menu>
        }
      </div>

      @if (!projectId) {
        <div class="card"><p class="muted">Select a project above to view its learner delivery and monitoring report.</p></div>
      }

      @if (dashboard(); as d) {
        <div class="card" style="margin-bottom: 16px">
          <div class="grid cols-2">
            <div><strong>Project:</strong> {{ d.projectReference }} - {{ d.projectName }}</div>
            <div><strong>Programme:</strong> {{ d.programmeName ?? '-' }}</div>
            <div><strong>Training Provider:</strong> {{ d.trainingProvider ?? '-' }}</div>
            <div><strong>Project Manager:</strong> {{ d.projectManagerName }}</div>
            <div><strong>Contract Period:</strong> {{ d.contractStart ?? '-' }} to {{ d.contractEnd ?? '-' }}</div>
            <div><strong>Contract Value:</strong> {{ d.contractValue ? (d.contractValue | number: '1.2-2') : '-' }}</div>
            <div><strong>Overall Status:</strong> <teta-status [value]="d.overallStatus" /></div>
            <div><strong>Generated:</strong> {{ d.generatedAtUtc | date: 'yyyy-MM-dd HH:mm' }} UTC</div>
          </div>
        </div>

        <mat-tab-group>
          <mat-tab label="A. Executive Summary">
            <teta-data-table [columns]="kpiColumns" [rows]="d.executiveSummary" [paginate]="false" exportName="executive-summary" />
          </mat-tab>

          <mat-tab label="B. Cohorts">
            <teta-data-table [columns]="cohortColumns" [rows]="d.cohorts" [paginate]="false" exportName="cohorts" />
          </mat-tab>

          <mat-tab label="C. Learner Register">
            <p class="muted" style="margin: 8px 0">Aggregated/reference-only view - learner names and ID numbers are not shown here (POPIA
              minimisation). Full identity reveal remains restricted to authorised operational users on the Beneficiary module.</p>
            <teta-data-table [columns]="registerColumns" [rows]="d.learnerRegister" exportName="learner-register" />
          </mat-tab>

          <mat-tab label="D. Exceptions">
            <teta-data-table [columns]="exceptionColumns" [rows]="d.exceptions" [paginate]="false" exportName="learner-exceptions" />
          </mat-tab>

          <mat-tab label="E. Delivery Performance">
            <teta-data-table [columns]="deliveryColumns" [rows]="d.deliveryPerformance" exportName="delivery-performance" />
          </mat-tab>

          <mat-tab label="F. Monitoring Visits">
            <teta-data-table [columns]="visitColumns" [rows]="d.monitoringVisits" exportName="monitoring-visits" />
          </mat-tab>

          <mat-tab label="G. Findings">
            <teta-data-table [columns]="findingColumns" [rows]="d.findings" exportName="findings" />
          </mat-tab>

          <mat-tab label="H. Corrective Actions">
            <teta-data-table [columns]="actionColumns" [rows]="d.correctiveActions" exportName="corrective-actions" />
          </mat-tab>

          <mat-tab label="I. Evidence">
            <teta-data-table [columns]="evidenceColumns" [rows]="d.evidence" [paginate]="false" exportName="evidence-completeness" />
          </mat-tab>

          <mat-tab label="J. Financial Alignment">
            <div class="card" style="margin-top: 12px">
              <teta-kpis [kpis]="financialKpis(d.financial)" />
            </div>
          </mat-tab>

          <mat-tab label="K. Payments">
            <teta-data-table [columns]="paymentColumns" [rows]="d.paymentMilestones" exportName="payment-milestones" />
          </mat-tab>

          <mat-tab label="L. Provider Scorecard">
            <teta-data-table [columns]="scorecardColumns" [rows]="d.providerScorecard" [paginate]="false" exportName="provider-scorecard" />
          </mat-tab>

          <mat-tab label="M. Risks">
            <teta-data-table [columns]="riskColumns" [rows]="d.risks" exportName="risks" />
          </mat-tab>

          <mat-tab label="N. Management Decisions">
            <ul style="margin-top: 12px">
              @for (dec of d.managementDecisions; track dec) { <li style="margin-bottom: 8px">{{ dec }}</li> }
            </ul>
          </mat-tab>
        </mat-tab-group>
      }
    </div>
  `
})
export class LearnerDeliveryReportComponent implements OnInit, OnChanges {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  @Input() projectId: string | undefined;
  readonly projects = signal<{ value: any; label: string }[]>([]);
  readonly dashboard = signal<any | null>(null);

  get canManageTargets(): boolean { return this.auth.has(P.meManage); }

  kpiColumns: Column[] = [
    { key: 'name', label: 'KPI' }, { key: 'target', label: 'Target', type: 'number' }, { key: 'actual', label: 'Actual', type: 'number' },
    { key: 'achievementPercent', label: 'Achievement %', type: 'percent' }, { key: 'status', label: 'Status', type: 'status' }
  ];
  cohortColumns: Column[] = [
    { key: 'cohort', label: 'Cohort' }, { key: 'registered', label: 'Registered', type: 'number' },
    { key: 'commenced', label: 'Commenced', type: 'number' }, { key: 'active', label: 'Active', type: 'number' },
    { key: 'completed', label: 'Completed', type: 'number' }, { key: 'withdrawn', label: 'Withdrawn', type: 'number' },
    { key: 'completionPercent', label: 'Completion %', type: 'percent' }, { key: 'status', label: 'Status', type: 'status' }
  ];
  registerColumns: Column[] = [
    { key: 'number', label: 'Learner Ref' }, { key: 'cohort', label: 'Cohort' }, { key: 'registeredAtUtc', label: 'Registered', type: 'date' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'progress', label: 'Progress' },
    { key: 'evidenceVerified', label: 'Evidence Verified', type: 'bool' }, { key: 'exception', label: 'Exception' }
  ];
  exceptionColumns: Column[] = [
    { key: 'category', label: 'Category' }, { key: 'learnerCount', label: 'Learners', type: 'number' }, { key: 'impact', label: 'Impact' },
    { key: 'responsibleParty', label: 'Responsible Party' }, { key: 'requiredAction', label: 'Required Action' },
    { key: 'status', label: 'Status', type: 'status' }
  ];
  deliveryColumns: Column[] = [
    { key: 'name', label: 'Deliverable' }, { key: 'planned', label: 'Planned', type: 'date' }, { key: 'actual', label: 'Actual', type: 'datetime' },
    { key: 'varianceDays', label: 'Variance (days)', type: 'number' }, { key: 'evidenceRequired', label: 'Evidence Required', type: 'bool' },
    { key: 'status', label: 'Status', type: 'status' }
  ];
  visitColumns: Column[] = [
    { key: 'number', label: 'Visit' }, { key: 'type', label: 'Type' }, { key: 'plannedDate', label: 'Planned', type: 'date' },
    { key: 'actualDate', label: 'Actual', type: 'date' }, { key: 'scope', label: 'Scope' }, { key: 'status', label: 'Status', type: 'status' }
  ];
  findingColumns: Column[] = [
    { key: 'number', label: 'Finding' }, { key: 'description', label: 'Description' }, { key: 'severity', label: 'Severity', type: 'status' },
    { key: 'closedAtUtc', label: 'Closed', type: 'date' }, { key: 'status', label: 'Status', type: 'status' }
  ];
  actionColumns: Column[] = [
    { key: 'number', label: 'Action' }, { key: 'description', label: 'Description' }, { key: 'ownerName', label: 'Owner' },
    { key: 'dueDate', label: 'Due Date', type: 'date' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'overdue', label: 'Overdue', type: 'bool' }
  ];
  evidenceColumns: Column[] = [
    { key: 'category', label: 'Category' }, { key: 'total', label: 'Total', type: 'number' }, { key: 'verified', label: 'Verified', type: 'number' },
    { key: 'pending', label: 'Pending', type: 'number' }, { key: 'rejected', label: 'Rejected', type: 'number' },
    { key: 'completenessPercent', label: 'Completeness %', type: 'percent' }
  ];
  paymentColumns: Column[] = [
    { key: 'description', label: 'Milestone' }, { key: 'amount', label: 'Amount', type: 'money' }, { key: 'plannedDate', label: 'Planned Date', type: 'date' },
    { key: 'deliveryStatus', label: 'Delivery Status', type: 'status' }, { key: 'paymentStatus', label: 'Payment Status', type: 'status' }
  ];
  scorecardColumns: Column[] = [
    { key: 'area', label: 'Area' }, { key: 'score', label: 'Score', type: 'number' }, { key: 'maxScore', label: 'Max', type: 'number' },
    { key: 'rating', label: 'Rating', type: 'status' }
  ];
  riskColumns: Column[] = [
    { key: 'number', label: 'Risk' }, { key: 'title', label: 'Title' }, { key: 'rating', label: 'Residual Rating', type: 'status' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'ownerName', label: 'Owner' }, { key: 'nextActionDueDate', label: 'Next Action Due', type: 'date' }
  ];

  ngOnInit(): void {
    this.refs.projects().subscribe(p => this.projects.set(p));
  }

  ngOnChanges(): void {
    if (this.projectId) this.load();
  }

  selectProject(projectId: string): void {
    this.router.navigate(['/reports/learner-delivery', projectId]);
  }

  load(): void {
    if (!this.projectId) return;
    this.api.get(`reports/learner-delivery/${this.projectId}`).subscribe(d => this.dashboard.set(d));
  }

  financialKpis(f: any): { label: string; value: number; unit?: string; status: string }[] {
    const statusFor = (v: number) => (v >= 0 ? 'Green' : 'Red');
    return [
      { label: 'Approved Budget', value: f.approvedBudget, unit: 'ZAR', status: 'NotConfigured' },
      { label: 'Committed', value: f.committed, unit: 'ZAR', status: 'NotConfigured' },
      { label: 'Actual Expenditure', value: f.actualExpenditure, unit: 'ZAR', status: 'NotConfigured' },
      { label: 'Remaining Budget', value: f.remainingBudget, unit: 'ZAR', status: statusFor(f.remainingBudget) },
      { label: 'Forecast Final Cost', value: f.forecastFinalCost, unit: 'ZAR', status: 'NotConfigured' },
      { label: 'Forecast Variance vs Budget', value: f.forecastVarianceVsBudget, unit: 'ZAR', status: statusFor(f.forecastVarianceVsBudget) }
    ];
  }

  exportAs(format: string): void {
    if (!this.projectId) return;
    this.api.download(`reports/learner-delivery/${this.projectId}/export`, { format })
      .subscribe(r => ApiService.saveBlob(r, `learner-delivery-report.${format}`));
  }

  async configureTargets(): Promise<void> {
    if (!this.projectId) return;
    const current: any = await new Promise(resolve => this.api.get(`reports/learner-delivery/${this.projectId}/target`).subscribe(resolve));
    const fields: Field[] = [
      { key: 'contractedLearners', label: 'Contracted Learners', type: 'number', required: true, min: 0 },
      { key: 'learnersDueForCompletion', label: 'Learners Due for Completion', type: 'number', required: true, min: 0 },
      { key: 'monitoringVisitsPlanned', label: 'Monitoring Visits Planned', type: 'number', required: true, min: 0 },
      { key: 'withdrawalTolerancePercent', label: 'Withdrawal Tolerance (%)', type: 'number', required: true, min: 0, max: 100 }
    ];
    const v = await openForm(this.dialog, { title: 'Configure learner delivery targets', fields, value: current,
      intro: 'These TETA-approved targets drive the Executive Summary KPIs (contract/target vs actual) on the Learner Delivery & Monitoring Report.' });
    if (!v) return;
    this.api.put(`reports/learner-delivery/${this.projectId}/target`, v).subscribe(() => this.load());
  }
}
