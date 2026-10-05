import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { map } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { Field, openForm } from '../../shared/form-dialog.component';
import { issueFields, riskFields } from '../common/forms';

/** Issues, external/internal dependencies, change control with impact assessment, and the project risk register. */
@Component({
  selector: 'teta-project-controls',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent, RouterLink],
  template: `
    <div style="padding-top: 12px" class="grid">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Issues</h2>
          @if (canManage) { <button mat-stroked-button (click)="editIssue()"><mat-icon>add</mat-icon> Issue</button> }</div>
        <teta-data-table [columns]="issueColumns" [rows]="issues()" [actions]="canManage ? issueActions : undefined" [paginate]="false" />
        <ng-template #issueActions let-row><button mat-icon-button (click)="editIssue(row)" title="Edit"><mat-icon>edit</mat-icon></button></ng-template>
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Change requests</h2>
          @if (canManage) { <button mat-stroked-button (click)="editChange()"><mat-icon>add</mat-icon> Change request</button> }</div>
        <teta-data-table [columns]="changeColumns" [rows]="changes()" [actions]="changeActions" [paginate]="false" />
        <ng-template #changeActions let-row>
          <button mat-button (click)="impact(row)">Impact</button>
          @if (canManage && row.status === 'Draft') {
            <button mat-icon-button (click)="editChange(row)" title="Edit"><mat-icon>edit</mat-icon></button>
            <button mat-button color="primary" (click)="submitChange(row)">Submit</button>
          }
        </ng-template>
        @if (impactText()) { <div class="info-banner" style="margin-top: 8px; white-space: pre-line">{{ impactText() }}</div> }
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Dependencies register</h2>
          @if (canManage) { <button mat-stroked-button (click)="editDependency()"><mat-icon>add</mat-icon> Dependency</button> }</div>
        <teta-data-table [columns]="dependencyColumns" [rows]="dependencies()" [actions]="canManage ? depActions : undefined" [paginate]="false" />
        <ng-template #depActions let-row><button mat-icon-button (click)="editDependency(row)" title="Edit"><mat-icon>edit</mat-icon></button></ng-template>
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Risks</h2>
          @if (canRisk) { <button mat-stroked-button (click)="addRisk()"><mat-icon>add</mat-icon> Risk</button> }</div>
        <teta-data-table [columns]="riskColumns" [rows]="risks()" (rowClick)="openRisk($event)" [paginate]="false" />
      </div>
    </div>
  `
})
export class ProjectControlsComponent implements OnChanges {
  @Input({ required: true }) project!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  readonly issues = signal<any[]>([]);
  readonly changes = signal<any[]>([]);
  readonly dependencies = signal<any[]>([]);
  readonly risks = signal<any[]>([]);
  readonly impactText = signal('');

  issueColumns: Column[] = [{ key: 'number', label: 'Issue' }, { key: 'title', label: 'Title' }, { key: 'severity', label: 'Severity', type: 'status' },
    { key: 'ownerName', label: 'Owner' }, { key: 'dueDate', label: 'Due', type: 'date' },
    { key: 'status', label: 'Status', type: 'status', value: r => (r.isOverdue ? 'Overdue' : r.status) }, { key: 'escalationLevel', label: 'Escalation', type: 'number' }];
  changeColumns: Column[] = [{ key: 'number', label: 'CR' }, { key: 'title', label: 'Title' }, { key: 'type', label: 'Type' },
    { key: 'costImpact', label: 'Cost impact', type: 'money' }, { key: 'scheduleImpactDays', label: 'Days', type: 'number' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'decidedBy', label: 'Decided by' }];
  dependencyColumns: Column[] = [{ key: 'description', label: 'Dependency' }, { key: 'direction', label: 'Direction' }, { key: 'dependsOn', label: 'Depends on' },
    { key: 'neededBy', label: 'Needed by', type: 'date' }, { key: 'ownerName', label: 'Owner' }, { key: 'status', label: 'Status', type: 'status' }];
  riskColumns: Column[] = [{ key: 'number', label: 'Risk' }, { key: 'title', label: 'Title' }, { key: 'inherentRating', label: 'Inherent', type: 'status' },
    { key: 'residualRating', label: 'Residual', type: 'status' }, { key: 'ownerName', label: 'Owner' }, { key: 'reviewDate', label: 'Review', type: 'date' },
    { key: 'overdueTreatments', label: 'Overdue treatments', type: 'number' }];

  get canManage(): boolean { return this.auth.has(P.executionManage); }
  get canRisk(): boolean { return this.auth.has(P.riskManage); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    const id = this.project.id;
    this.api.get<any[]>('issues', { projectId: id, openOnly: false }).subscribe(r => this.issues.set(r));
    this.api.get<any[]>('change-requests', { projectId: id }).subscribe(r => this.changes.set(r));
    this.api.get<any[]>(`projects/${id}/dependency-register`).subscribe(r => this.dependencies.set(r));
    if (this.auth.hasAny(P.riskRead, P.riskManage)) this.api.get<any[]>('assurance/risks', { projectId: id, openOnly: false }).subscribe(r => this.risks.set(r));
  }

  async editIssue(row?: any): Promise<void> {
    const v = await openForm(this.dialog, { title: row ? 'Edit issue' : 'New issue', fields: issueFields(this.refs, !!row), value: row });
    if (!v) return;
    const body = { ...v, projectId: this.project.id };
    (row ? this.api.put(`issues/${row.id}`, body) : this.api.post('issues', body)).subscribe(() => this.load());
  }

  private changeFields(projectId: string): Field[] {
    return [
      { key: 'type', label: 'Change type', type: 'select', required: true, options: ['Scope', 'Cost', 'Schedule', 'Benefit'].map(t => ({ value: t, label: t })) },
      { key: 'title', label: 'Title', required: true, wide: true }, { key: 'description', label: 'Description', type: 'textarea', required: true },
      { key: 'justification', label: 'Justification', type: 'textarea', required: true },
      { key: 'costImpact', label: 'Cost impact (R)', type: 'number', required: true }, { key: 'scheduleImpactDays', label: 'Schedule impact (days)', type: 'number', required: true },
      { key: 'proposedEndDate', label: 'Proposed end date', type: 'date' }, { key: 'benefitImpact', label: 'Benefit impact', type: 'textarea' },
      { key: 'contractImpact', label: 'Contract impact', type: 'textarea' },
      { key: 'budgetLineId', label: 'Budget line', type: 'select', hint: 'Required when the change has a cost impact.',
        options: this.api.get<any>(`projects/${projectId}/budget`).pipe(map(b => (b.lines as any[]).map(l => ({ value: l.id, label: `${l.financialYear} · ${l.costCategory} · ${l.fundingSource}` })))) }
    ];
  }

  async editChange(row?: any): Promise<void> {
    const v = await openForm(this.dialog, { title: row ? 'Edit change request' : 'New change request', fields: this.changeFields(this.project.id),
      value: row ?? { costImpact: 0, scheduleImpactDays: 0 } });
    if (!v) return;
    const body = { ...v, projectId: this.project.id, contractId: row?.contractId ?? null };
    (row ? this.api.put(`change-requests/${row.id}`, body) : this.api.post('change-requests', body)).subscribe(() => this.load());
  }

  impact(row: any): void {
    this.api.get<any>(`change-requests/${row.id}/impact`).subscribe(i => this.impactText.set(
      `${row.number}: ${i.summary}` + (i.exceedsBudgetAvailability ? '\nWarning: exceeds available budget.' : '')));
  }

  submitChange(row: any): void {
    this.api.post(`change-requests/${row.id}/submit`).subscribe(() => { this.snack.open('Change request submitted.', 'OK', { duration: 3000 }); this.load(); });
  }

  async editDependency(row?: any): Promise<void> {
    const v = await openForm(this.dialog, { title: row ? 'Edit dependency' : 'New dependency', fields: [
      { key: 'description', label: 'Description', type: 'textarea', required: true },
      { key: 'direction', label: 'Direction', type: 'select', required: true, options: ['Internal', 'External'].map(t => ({ value: t, label: t })) },
      { key: 'dependsOn', label: 'Depends on', required: true }, { key: 'impact', label: 'Impact' }, { key: 'neededBy', label: 'Needed by', type: 'date' },
      { key: 'ownerName', label: 'Owner' },
      { key: 'status', label: 'Status', type: 'select', required: true, options: ['Open', 'OnTrack', 'AtRisk', 'Late', 'Resolved'].map(t => ({ value: t, label: t })) }],
      value: row ?? { direction: 'External', status: 'Open' } });
    if (!v) return;
    const path = `projects/${this.project.id}/dependency-register`;
    (row ? this.api.put(`${path}/${row.id}`, v) : this.api.post(path, v)).subscribe(() => this.load());
  }

  async addRisk(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New project risk', fields: riskFields(this.refs), value: { reviewDate: new Date().toISOString().substring(0, 10) } });
    if (v) this.api.post('assurance/risks', { ...v, parentType: 'Project', parentId: this.project.id }).subscribe(() => { this.load(); this.changed.emit(); });
  }

  openRisk(row: any): void {
    this.router.navigate(['/assurance/risks', row.id]);
  }
}
