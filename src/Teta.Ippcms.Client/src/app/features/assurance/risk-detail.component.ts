import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { EFFECTIVENESS, controlFields, riskFields, treatmentFields } from './assurance-forms';

/** A single risk: rating, mitigating controls (with assessment history) and treatment plan (FR-RSK-001..004). */
@Component({
  selector: 'teta-risk-detail',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent, StatusChipComponent],
  template: `
    @if (risk(); as r) {
      <div class="page">
        <div class="page-header">
          <h1>{{ r.number }} – {{ r.title }}</h1>
          <teta-status [value]="r.residualRating" /> <teta-status [value]="r.status" />
          @if (canManage) { <button mat-stroked-button (click)="edit()"><mat-icon>edit</mat-icon> Edit</button> }
        </div>
        <div class="grid cols-2">
          <div class="card">
            <dl class="dl small">
              <dt>Cause</dt><dd>{{ r.cause }}</dd><dt>Event</dt><dd>{{ r.event }}</dd><dt>Consequence</dt><dd>{{ r.consequence }}</dd>
              <dt>Category</dt><dd>{{ r.category ?? '—' }}</dd>
              <dt>Inherent (L/I/Score)</dt><dd>{{ r.inherentLikelihood }} / {{ r.inherentImpact }} / {{ r.inherentScore }} ({{ r.inherentRating }})</dd>
              <dt>Residual (L/I/Score)</dt><dd>{{ r.residualLikelihood }} / {{ r.residualImpact }} / {{ r.residualScore }} ({{ r.residualRating }})</dd>
              <dt>Owner</dt><dd>{{ r.ownerName }}</dd><dt>Review date</dt><dd>{{ r.reviewDate }} @if (r.reviewOverdue) { <span class="warn">(overdue)</span> }</dd>
            </dl>
          </div>
          <div class="card">
            <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Controls</h2>
              @if (canManage) { <button mat-stroked-button (click)="addControl()"><mat-icon>add</mat-icon> Add</button> }
            </div>
            <teta-data-table [columns]="controlColumns" [rows]="controls()" [actions]="canManage ? controlActions : undefined"
                             [filterable]="false" [paginate]="false" [exportable]="false" emptyText="No controls recorded." />
            <ng-template #controlActions let-row>
              <button mat-icon-button (click)="editControl(row)" title="Edit"><mat-icon>edit</mat-icon></button>
              <button mat-icon-button (click)="assessControl(row)" title="Assess"><mat-icon>fact_check</mat-icon></button>
            </ng-template>
          </div>
          <div class="card" style="grid-column: 1 / -1">
            <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Treatment plan</h2>
              @if (canManage) { <button mat-stroked-button (click)="addTreatment()"><mat-icon>add</mat-icon> Add treatment</button> }
            </div>
            <teta-data-table [columns]="treatmentColumns" [rows]="treatments()" [actions]="canManage ? treatmentActions : undefined"
                             emptyText="No treatments planned." exportName="risk-treatments" />
            <ng-template #treatmentActions let-row>
              <button mat-icon-button (click)="editTreatment(row)" title="Edit"><mat-icon>edit</mat-icon></button>
            </ng-template>
          </div>
        </div>
      </div>
    }
  `
})
export class RiskDetailComponent implements OnChanges {
  @Input() id!: string;
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  readonly risk = signal<any | null>(null);
  readonly controls = signal<any[]>([]);
  readonly treatments = signal<any[]>([]);

  controlColumns: Column[] = [
    { key: 'name', label: 'Name' }, { key: 'ownerName', label: 'Owner' }, { key: 'effectiveness', label: 'Effectiveness', type: 'status' },
    { key: 'lastAssessedOn', label: 'Last assessed', type: 'date' }
  ];
  treatmentColumns: Column[] = [
    { key: 'description', label: 'Description' }, { key: 'ownerName', label: 'Owner' }, { key: 'dueDate', label: 'Due', type: 'date' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'isOverdue', label: 'Overdue', type: 'bool' }, { key: 'escalationLevel', label: 'Escalation', type: 'number' }
  ];

  get canManage(): boolean { return this.auth.has(P.riskManage); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.get(`assurance/risks/${this.id}`).subscribe(r => this.risk.set(r));
    this.api.get<any[]>(`assurance/risks/${this.id}/controls`).subscribe(r => this.controls.set(r));
    this.api.get<any[]>(`assurance/risks/${this.id}/treatments`).subscribe(r => this.treatments.set(r));
  }

  async edit(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit risk', fields: riskFields(this.refs, true), value: this.risk() }, '860px');
    if (v) this.api.put(`assurance/risks/${this.id}`, v).subscribe(() => this.load());
  }

  async addControl(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Add control', fields: controlFields(this.refs) }, '760px');
    if (v) this.api.post(`assurance/risks/${this.id}/controls`, v).subscribe(() => this.load());
  }

  async editControl(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit control', fields: controlFields(this.refs), value: row }, '760px');
    if (v) this.api.put(`assurance/risks/${this.id}/controls/${row.id}`, v).subscribe(() => this.load());
  }

  async assessControl(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Assess control', fields: [
      { key: 'effectiveness', label: 'Effectiveness', type: 'select', required: true, options: EFFECTIVENESS }, { key: 'comment', label: 'Comment', type: 'textarea' }
    ] }, '620px');
    if (v) this.api.post(`assurance/risks/${this.id}/controls/${row.id}/assess`, v).subscribe(() => this.load());
  }

  async addTreatment(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Add treatment', fields: treatmentFields(this.refs) }, '760px');
    if (v) this.api.post(`assurance/risks/${this.id}/treatments`, v).subscribe(() => this.load());
  }

  async editTreatment(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit treatment', fields: [...treatmentFields(this.refs), { key: 'status', label: 'Status', type: 'select',
      options: [{ value: 'Open', label: 'Open' }, { value: 'InProgress', label: 'In progress' }, { value: 'Completed', label: 'Completed' }, { value: 'Cancelled', label: 'Cancelled' }] }],
      value: row }, '760px');
    if (v) this.api.put(`assurance/risks/${this.id}/treatments/${row.id}`, v).subscribe(() => this.load());
  }
}
