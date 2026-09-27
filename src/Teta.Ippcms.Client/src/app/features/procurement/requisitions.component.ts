import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
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
import { confirmAction, openForm } from '../../shared/form-dialog.component';
import { requisitionFields } from './procurement-forms';

/** Electronic procurement requisitions: draft, submit for approval and convert to a procurement (FR-SCM-001, BR-002). */
@Component({
  selector: 'teta-requisitions',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Requisitions</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> New requisition</button> }
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" [actions]="actions" emptyText="No requisitions." exportName="requisitions" />
      <ng-template #actions let-row>
        @if (canManage && row.status === 'Draft') {
          <button mat-icon-button matTooltip="Edit" (click)="edit(row)"><mat-icon>edit</mat-icon></button>
          <button mat-icon-button matTooltip="Submit for approval" (click)="submit(row)"><mat-icon>send</mat-icon></button>
        }
        @if (canConvert && row.status === 'Approved') {
          <button mat-icon-button matTooltip="Convert to procurement" (click)="convert(row)"><mat-icon>swap_horiz</mat-icon></button>
        }
      </ng-template>
    </div>
  `
})
export class RequisitionsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  readonly rows = signal<any[]>([]);

  columns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'projectReference', label: 'Project' }, { key: 'title', label: 'Title' },
    { key: 'estimatedValue', label: 'Estimated value', type: 'money' }, { key: 'requiredByDate', label: 'Required by', type: 'date' },
    { key: 'recommendedMethod', label: 'Recommended method' }, { key: 'selectedMethod', label: 'Selected method' },
    { key: 'budgetAvailable', label: 'Budget available', type: 'bool' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'createdAtUtc', label: 'Created', type: 'datetime' }
  ];

  get canManage(): boolean { return this.auth.hasAny(P.projectManage, P.procurementManage); }
  get canConvert(): boolean { return this.auth.has(P.procurementManage); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('procurement/requisitions').subscribe(r => this.rows.set(r));
  }

  async add(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New requisition', fields: requisitionFields(this.refs) }, '780px');
    if (v) this.api.post('procurement/requisitions', v).subscribe(() => this.load());
  }

  async edit(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit requisition', fields: requisitionFields(this.refs), value: row }, '780px');
    if (v) this.api.put(`procurement/requisitions/${row.id}`, v).subscribe(() => this.load());
  }

  submit(row: any): void {
    this.api.post(`procurement/requisitions/${row.id}/submit`).subscribe(() => {
      this.snack.open('Submitted for approval.', 'OK', { duration: 3000 });
      this.load();
    });
  }

  async convert(row: any): Promise<void> {
    const ok = await confirmAction(this.dialog, 'Convert to procurement', `Convert requisition ${row.number} into a procurement process?`);
    if (ok === undefined) return;
    this.api.post(`procurement/requisitions/${row.id}/convert`).subscribe((p: any) => {
      this.snack.open('Procurement created.', 'OK', { duration: 3000 });
      this.router.navigate(['/procurement', p.summary.id]);
    });
  }
}
