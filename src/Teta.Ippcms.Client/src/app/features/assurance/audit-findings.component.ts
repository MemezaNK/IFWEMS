import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { auditFindingFields } from './assurance-forms';

/** Internal/external audit findings with management response and corrective-action tracking (FR-RSK-006). */
@Component({
  selector: 'teta-audit-findings',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatCheckboxModule, FormsModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Audit findings</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> New finding</button> }
      </div>
      <div class="toolbar-row"><mat-checkbox [(ngModel)]="openOnly" (ngModelChange)="load()">Open only</mat-checkbox></div>
      <teta-data-table [columns]="columns" [rows]="rows()" [actions]="canManage ? actions : undefined" emptyText="No audit findings recorded." exportName="audit-findings" />
      <ng-template #actions let-row>
        @if (row.status !== 'Closed') { <button mat-icon-button (click)="edit(row)" title="Edit"><mat-icon>edit</mat-icon></button> }
      </ng-template>
    </div>
  `
})
export class AuditFindingsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  readonly rows = signal<any[]>([]);
  openOnly = false;

  columns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'projectReference', label: 'Project' }, { key: 'process', label: 'Process' }, { key: 'source', label: 'Source' },
    { key: 'title', label: 'Title' }, { key: 'rating', label: 'Rating', type: 'status' }, { key: 'actionOwnerName', label: 'Action owner' },
    { key: 'dueDate', label: 'Due', type: 'date' }, { key: 'status', label: 'Status', type: 'status' }, { key: 'isOverdue', label: 'Overdue', type: 'bool' },
    { key: 'openActions', label: 'Open actions', type: 'number' }
  ];

  get canManage(): boolean { return this.auth.has(P.assuranceManage); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('assurance/audit-findings', { openOnly: this.openOnly }).subscribe(r => this.rows.set(r));
  }

  async add(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New audit finding', fields: auditFindingFields(this.refs) }, '860px');
    if (v) this.api.post('assurance/audit-findings', v).subscribe(() => this.load());
  }

  async edit(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit audit finding', fields: auditFindingFields(this.refs), value: row }, '860px');
    if (v) this.api.put(`assurance/audit-findings/${row.id}`, v).subscribe(() => this.load());
  }
}
