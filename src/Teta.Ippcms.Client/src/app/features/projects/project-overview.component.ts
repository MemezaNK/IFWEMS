import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { confirmAction, openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { projectFields } from './project-forms';

@Component({
  selector: 'teta-project-overview',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, StatusChipComponent, DataTableComponent, CurrencyPipe, DatePipe, DecimalPipe],
  template: `
    <div style="padding-top: 12px" class="grid cols-2">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Details</h2>
          @if (canManage) { <button mat-stroked-button (click)="edit()"><mat-icon>edit</mat-icon> Edit</button>
            <button mat-stroked-button (click)="changeStatus()">Change status</button> }
        </div>
        <dl class="dl">
          <dt>Project number</dt><dd>{{ project.projectNumber ?? 'Issued on business case approval' }}</dd>
          <dt>Draft reference</dt><dd>{{ project.draftReference }}</dd>
          <dt>Description</dt><dd>{{ project.description }}</dd>
          <dt>Type / unit</dt><dd>{{ project.projectType }} · {{ project.orgUnit }}</dd>
          <dt>Location</dt><dd>{{ project.province }} {{ project.district }} {{ project.municipality }}</dd>
          <dt>Business owner</dt><dd>{{ project.businessOwner }}</dd>
          <dt>Planned</dt><dd>{{ project.plannedStart }} → {{ project.plannedEnd }}</dd>
          <dt>Actual</dt><dd>{{ project.actualStart }} → {{ project.actualEnd }}</dd>
          <dt>Approved budget</dt><dd>{{ project.approvedBudget | currency: 'ZAR' : 'R ' }}</dd>
          <dt>Priority score</dt><dd>{{ project.priorityScore | number: '1.0-2' }}</dd>
          <dt>Progress</dt><dd>{{ project.percentComplete | number: '1.0-1' }}%</dd>
          <dt>Open issues / risks</dt><dd>{{ project.openIssues }} / {{ project.openRisks }}</dd>
          <dt>Procurements / contracts</dt><dd>{{ project.procurements }} / {{ project.contracts }}</dd>
        </dl>
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Health</h2>
          @if (canManage) { <button mat-stroked-button (click)="recalculate()"><mat-icon>refresh</mat-icon> Recalculate</button> }
          <button mat-stroked-button (click)="statusReport()"><mat-icon>print</mat-icon> Status report</button>
        </div>
        <p><teta-status [value]="project.health" /> {{ project.healthExplanation }}</p>
        <teta-data-table [columns]="healthColumns" [rows]="health()" [filterable]="false" [exportable]="false" [pageSize]="10" />
      </div>
      <div class="card">
        <h2>Status history</h2>
        <teta-data-table [columns]="historyColumns" [rows]="history()" [filterable]="false" [exportable]="false" [pageSize]="10" />
      </div>
      @if (report(); as r) {
        <div class="card" style="grid-column: 1 / -1">
          <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Status report – {{ r.reference }}</h2>
            <button mat-stroked-button class="no-print" (click)="print()"><mat-icon>print</mat-icon> Print</button></div>
          <dl class="dl">
            <dt>Generated</dt><dd>{{ r.generatedAtUtc | date: 'yyyy-MM-dd HH:mm' }}</dd>
            <dt>Health</dt><dd>{{ r.health }} – {{ r.healthExplanation }}</dd>
            <dt>Progress</dt><dd>{{ r.percentComplete | number: '1.0-1' }}% · planned end {{ r.plannedEnd }} · forecast end {{ r.forecastEnd }}</dd>
            <dt>Budget / committed / actual / EAC</dt>
            <dd>{{ r.approvedBudget | currency: 'ZAR' : 'R ' }} / {{ r.committed | currency: 'ZAR' : 'R ' }} / {{ r.actual | currency: 'ZAR' : 'R ' }} / {{ r.estimateAtCompletion | currency: 'ZAR' : 'R ' }}</dd>
            <dt>Overdue milestones</dt><dd>{{ r.overdueMilestones.length }}</dd>
            <dt>Open issues</dt><dd>{{ r.openIssues.length }}</dd>
            <dt>Top risks</dt><dd>@for (x of r.topRisks; track x.number) { <div>{{ x.number }} {{ x.title }} ({{ x.residualRating }}, {{ x.ownerName }})</div> }</dd>
          </dl>
        </div>
      }
    </div>
  `,
})
export class ProjectOverviewComponent implements OnChanges {
  @Input({ required: true }) project!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  readonly health = signal<any[]>([]);
  readonly history = signal<any[]>([]);
  readonly report = signal<any | null>(null);

  healthColumns: Column[] = [
    { key: 'calculatedAtUtc', label: 'Calculated', type: 'datetime' }, { key: 'overall', label: 'Overall', type: 'status' },
    { key: 'scheduleVarianceDays', label: 'Schedule variance (days)', type: 'number' }, { key: 'costVariancePercent', label: 'Cost variance', type: 'percent' },
    { key: 'criticalOpenRisks', label: 'Critical risks', type: 'number' }, { key: 'overdueMilestones', label: 'Overdue milestones', type: 'number' }
  ];
  historyColumns: Column[] = [
    { key: 'changedAtUtc', label: 'When', type: 'datetime' }, { key: 'fromStatus', label: 'From' }, { key: 'toStatus', label: 'To', type: 'status' },
    { key: 'toStage', label: 'Stage' }, { key: 'changedBy', label: 'By' }, { key: 'reason', label: 'Reason' }
  ];

  get canManage(): boolean { return this.auth.has(P.projectManage); }

  ngOnChanges(): void {
    this.api.get<any[]>(`projects/${this.project.id}/health`).subscribe(h => this.health.set(h));
    this.api.get<any[]>(`projects/${this.project.id}/status-history`).subscribe(h => this.history.set(h));
  }

  async edit(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit project', fields: projectFields(this.refs), value: this.project });
    if (v) this.api.put(`projects/${this.project.id}`, v, { version: this.project.version }).subscribe(() => this.changed.emit());
  }

  async changeStatus(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Change project status', fields: [
      { key: 'status', label: 'New status', type: 'select', required: true,
        options: ['InExecution', 'OnHold', 'Closing', 'Cancelled'].map(s => ({ value: s, label: s })) },
      { key: 'reason', label: 'Reason', type: 'textarea', required: true }] }, '520px');
    if (v) this.api.post(`projects/${this.project.id}/status`, v).subscribe(() => this.changed.emit());
  }

  recalculate(): void {
    this.api.post(`projects/${this.project.id}/health`).subscribe(() => { this.changed.emit(); this.ngOnChanges(); });
  }

  statusReport(): void {
    this.api.get(`projects/${this.project.id}/status-report`).subscribe(r => this.report.set(r));
  }

  print(): void {
    window.print();
  }

  protected readonly confirm = confirmAction;
}
