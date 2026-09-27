import { Component, EventEmitter, Input, OnChanges, Output, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { confirmAction, openForm } from '../../shared/form-dialog.component';
import { deliverableFields, variationFields } from './contract-forms';

/** Deliverables (payable milestones) and contract variations (scope/value/time), each with an approval workflow (FR-CON-005/006). */
@Component({
  selector: 'teta-contract-deliverables',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div style="padding-top: 12px">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Deliverables</h2>
          @if (canManage) { <button mat-stroked-button (click)="addDeliverable()"><mat-icon>add</mat-icon> New deliverable</button> }
        </div>
        <teta-data-table [columns]="deliverableColumns" [rows]="detail.deliverables" [actions]="canManage ? deliverableActions : undefined"
                         emptyText="No deliverables linked to this contract." exportName="deliverables" />
        <ng-template #deliverableActions let-row>
          @if (canManage && row.acceptanceStatus === 'Draft') { <button mat-icon-button (click)="editDeliverable(row)" title="Edit"><mat-icon>edit</mat-icon></button>
            <button mat-icon-button (click)="submitDeliverable(row)" title="Submit"><mat-icon>send</mat-icon></button> }
          @if (canManage && row.acceptanceStatus === 'Submitted') {
            <button mat-icon-button (click)="decide(row, true)" title="Accept"><mat-icon>check_circle</mat-icon></button>
            <button mat-icon-button (click)="decide(row, false)" title="Reject"><mat-icon>cancel</mat-icon></button>
          }
        </ng-template>
      </div>

      <div class="card" style="margin-top: 16px">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Variations</h2>
          @if (canManage) { <button mat-stroked-button (click)="addVariation()"><mat-icon>add</mat-icon> New variation</button> }
        </div>
        <teta-data-table [columns]="variationColumns" [rows]="detail.variations" [actions]="canManage ? variationActions : undefined"
                         emptyText="No variations recorded." exportName="variations" />
        <ng-template #variationActions let-row>
          @if (row.status === 'Draft') { <button mat-icon-button (click)="editVariation(row)" title="Edit"><mat-icon>edit</mat-icon></button>
            <button mat-icon-button (click)="submitVariation(row)" title="Submit for approval"><mat-icon>send</mat-icon></button> }
        </ng-template>
      </div>
    </div>
  `
})
export class ContractDeliverablesComponent implements OnChanges {
  @Input({ required: true }) detail!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);

  deliverableColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'name', label: 'Name' }, { key: 'dueDate', label: 'Due', type: 'date' },
    { key: 'payableAmount', label: 'Payable', type: 'money' }, { key: 'invoiced', label: 'Invoiced', type: 'money' },
    { key: 'acceptanceStatus', label: 'Status', type: 'status' }, { key: 'evidenceCount', label: 'Evidence', type: 'number' }, { key: 'isOverdue', label: 'Overdue', type: 'bool' }
  ];
  variationColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'type', label: 'Type' }, { key: 'description', label: 'Description' },
    { key: 'amount', label: 'Value change', type: 'money' }, { key: 'days', label: 'Days change', type: 'number' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'decidedAtUtc', label: 'Decided', type: 'datetime' }, { key: 'decidedBy', label: 'Decided by' }
  ];

  get canManage(): boolean { return this.auth.hasAny(P.contractManage, P.executionManage); }

  ngOnChanges(): void {}

  async addDeliverable(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New deliverable', fields: deliverableFields(this.refs), value: { contractId: this.detail.summary.id } }, '780px');
    if (v) this.api.post('contracts/deliverables', { ...v, contractId: this.detail.summary.id }).subscribe(() => this.changed.emit());
  }

  async editDeliverable(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit deliverable', fields: deliverableFields(this.refs), value: row }, '780px');
    if (v) this.api.put(`contracts/deliverables/${row.id}`, { ...v, contractId: this.detail.summary.id }).subscribe(() => this.changed.emit());
  }

  submitDeliverable(row: any): void {
    this.api.post(`contracts/deliverables/${row.id}/submit`).subscribe(() => this.changed.emit());
  }

  async decide(row: any, accept: boolean): Promise<void> {
    const reason = await confirmAction(this.dialog, accept ? 'Accept deliverable' : 'Reject deliverable', row.name, accept ? undefined : 'Reason');
    if (reason === undefined) return;
    this.api.post(`contracts/deliverables/${row.id}/decision`, { accept, reason }).subscribe(() => this.changed.emit());
  }

  async addVariation(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New variation', fields: variationFields() }, '780px');
    if (v) this.api.post(`contracts/${this.detail.summary.id}/variations`, v).subscribe(() => this.changed.emit());
  }

  async editVariation(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit variation', fields: variationFields(), value: row }, '780px');
    if (v) this.api.put(`contracts/${this.detail.summary.id}/variations/${row.id}`, v).subscribe(() => this.changed.emit());
  }

  submitVariation(row: any): void {
    this.api.post(`contracts/${this.detail.summary.id}/variations/${row.id}/submit`).subscribe(() => this.changed.emit());
  }
}
