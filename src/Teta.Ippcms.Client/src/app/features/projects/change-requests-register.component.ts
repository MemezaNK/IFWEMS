import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { Field, openForm } from '../../shared/form-dialog.component';

/** Cross-project change control register (FR-EXE-010): scope/cost/schedule/benefit changes with impact assessment and approval. */
@Component({
  selector: 'teta-change-requests-register',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatSelectModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Change requests</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="edit()"><mat-icon>add</mat-icon> New change request</button> }
      </div>
      <div class="toolbar-row">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="min-width: 260px">
          <mat-label>Project</mat-label>
          <mat-select [(ngModel)]="projectId" (ngModelChange)="load()">
            <mat-option [value]="undefined">All projects</mat-option>
            @for (p of projects(); track p.value) { <mat-option [value]="p.value">{{ p.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" [actions]="actions" emptyText="No change requests found." exportName="change-requests" />
      <ng-template #actions let-row>
        <button mat-button (click)="impact(row)">Impact</button>
        @if (canManage && row.status === 'Draft') {
          <button mat-icon-button (click)="edit(row)" title="Edit"><mat-icon>edit</mat-icon></button>
          <button mat-button color="primary" (click)="submit(row)">Submit</button>
        }
      </ng-template>
      @if (impactText()) { <div class="info-banner" style="margin-top: 8px; white-space: pre-line">{{ impactText() }}</div> }
    </div>
  `
})
export class ChangeRequestsRegisterComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly rows = signal<any[]>([]);
  readonly projects = signal<{ value: any; label: string }[]>([]);
  readonly impactText = signal('');
  projectId: string | undefined;

  columns: Column[] = [
    { key: 'number', label: 'CR' }, { key: 'projectReference', label: 'Project' }, { key: 'title', label: 'Title' }, { key: 'type', label: 'Type' },
    { key: 'costImpact', label: 'Cost impact', type: 'money' }, { key: 'scheduleImpactDays', label: 'Days', type: 'number' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'decidedBy', label: 'Decided by' }
  ];

  get canManage(): boolean { return this.auth.has(P.executionManage); }

  ngOnInit(): void {
    this.refs.projects().subscribe(p => this.projects.set(p));
    this.load();
  }

  load(): void {
    this.api.get<any[]>('change-requests', { projectId: this.projectId }).subscribe(r => this.rows.set(r));
  }

  private fields(): Field[] {
    return [
      { key: 'type', label: 'Change type', type: 'select', required: true, options: ['Scope', 'Cost', 'Schedule', 'Benefit'].map(t => ({ value: t, label: t })) },
      { key: 'title', label: 'Title', required: true, wide: true }, { key: 'description', label: 'Description', type: 'textarea', required: true },
      { key: 'justification', label: 'Justification', type: 'textarea', required: true },
      { key: 'costImpact', label: 'Cost impact (R)', type: 'number', required: true }, { key: 'scheduleImpactDays', label: 'Schedule impact (days)', type: 'number', required: true },
      { key: 'proposedEndDate', label: 'Proposed end date', type: 'date' }, { key: 'benefitImpact', label: 'Benefit impact', type: 'textarea' },
      { key: 'contractImpact', label: 'Contract impact', type: 'textarea' }
    ];
  }

  async edit(row?: any): Promise<void> {
    if (!row && !this.projectId) { alert('Select a project above before creating a change request.'); return; }
    const v = await openForm(this.dialog, { title: row ? 'Edit change request' : 'New change request', fields: this.fields(),
      value: row ?? { costImpact: 0, scheduleImpactDays: 0 } }, '680px');
    if (!v) return;
    const body = { ...v, projectId: row?.projectId ?? this.projectId, contractId: row?.contractId ?? null, budgetLineId: row?.budgetLineId ?? null };
    (row ? this.api.put(`change-requests/${row.id}`, body) : this.api.post('change-requests', body)).subscribe(() => this.load());
  }

  impact(row: any): void {
    this.api.get<any>(`change-requests/${row.id}/impact`).subscribe(i => this.impactText.set(
      `${row.number}: ${i.summary}` + (i.exceedsBudgetAvailability ? '\nWarning: exceeds available budget.' : '')));
  }

  submit(row: any): void {
    this.api.post(`change-requests/${row.id}/submit`).subscribe(() => { this.snack.open('Change request submitted.', 'OK', { duration: 3000 }); this.load(); });
  }
}
