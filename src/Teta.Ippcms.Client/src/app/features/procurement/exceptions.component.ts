import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { exceptionFields } from './procurement-forms';

/** Deviations, emergency and single-source procurement exceptions with a full audit trail (FR-SCM-015, BR-011). */
@Component({
  selector: 'teta-procurement-exceptions',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Deviations & exceptions</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> New deviation</button> }
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" [actions]="actions" emptyText="No deviations recorded." exportName="deviations" />
      <ng-template #actions let-row>
        @if (canManage && row.status === 'Draft') {
          <button mat-icon-button matTooltip="Edit" (click)="edit(row)"><mat-icon>edit</mat-icon></button>
          <button mat-icon-button matTooltip="Submit for approval" (click)="submit(row)"><mat-icon>send</mat-icon></button>
        }
      </ng-template>
    </div>
  `
})
export class ExceptionsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly rows = signal<any[]>([]);

  columns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'type', label: 'Type' }, { key: 'motivation', label: 'Motivation' },
    { key: 'authority', label: 'Authority' }, { key: 'value', label: 'Value', type: 'money' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'decidedAtUtc', label: 'Decided', type: 'datetime' }, { key: 'decidedBy', label: 'Decided by' }, { key: 'decisionReason', label: 'Decision reason' }
  ];

  get canManage(): boolean { return this.auth.has(P.procurementManage); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('procurement/exceptions').subscribe(r => this.rows.set(r));
  }

  async add(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New deviation / exception', fields: exceptionFields(this.refs) }, '780px');
    if (v) this.api.post('procurement/exceptions', v).subscribe(() => this.load());
  }

  async edit(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit deviation / exception', fields: exceptionFields(this.refs), value: row }, '780px');
    if (v) this.api.put(`procurement/exceptions/${row.id}`, v).subscribe(() => this.load());
  }

  submit(row: any): void {
    this.api.post(`procurement/exceptions/${row.id}/submit`).subscribe(() => {
      this.snack.open('Submitted for approval.', 'OK', { duration: 3000 });
      this.load();
    });
  }
}
