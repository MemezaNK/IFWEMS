import { DecimalPipe } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { obligationFields, paymentScheduleFields, updateContractFields } from './contract-forms';

/** Contract header, obligations register and payment schedule (FR-CON-002..004). */
@Component({
  selector: 'teta-contract-overview',
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div style="padding-top: 12px" class="grid cols-2">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Details</h2>
          @if (canManage) { <button mat-stroked-button (click)="edit()"><mat-icon>edit</mat-icon> Edit</button> }
          @if (canSign && detail.summary.signatureStatus !== 'Signed') { <button mat-flat-button color="primary" (click)="sign()">Record signature</button> }
        </div>
        <dl class="dl small">
          <dt>Source</dt><dd>{{ detail.summary.source }} @if (detail.procurementNumber) { ({{ detail.procurementNumber }}) }</dd>
          <dt>Original value</dt><dd>R {{ detail.summary.originalValue | number }}</dd>
          <dt>Approved variations</dt><dd>R {{ detail.summary.approvedVariations | number }}</dd>
          <dt>Revised value</dt><dd>R {{ detail.summary.revisedValue | number }}</dd>
          <dt>Committed / invoiced / paid</dt><dd>R {{ detail.summary.committed | number }} / R {{ detail.summary.invoiced | number }} / R {{ detail.summary.paid | number }}</dd>
          <dt>Period</dt><dd>{{ detail.summary.startDate }} → {{ detail.summary.currentEndDate }} ({{ detail.summary.daysToExpiry }} days to expiry)</dd>
          <dt>Contract manager</dt><dd>{{ detail.summary.contractManagerName ?? '—' }}</dd>
          <dt>PO reference</dt><dd>{{ detail.poReference ?? '—' }}</dd>
          <dt>Signed</dt><dd>{{ detail.signedDate ?? '—' }}</dd>
          <dt>Performance rating</dt><dd>{{ detail.summary.performanceRating ?? '—' }}</dd>
        </dl>
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Obligations</h2>
          @if (canManage) { <button mat-stroked-button (click)="addObligation()"><mat-icon>add</mat-icon> Add</button> }
        </div>
        <teta-data-table [columns]="obligationColumns" [rows]="detail.obligations" [actions]="canManage ? obligationActions : undefined"
                         [filterable]="false" [paginate]="false" [exportable]="false" emptyText="No obligations captured." />
        <ng-template #obligationActions let-row>
          <button mat-icon-button (click)="editObligation(row)" title="Edit"><mat-icon>edit</mat-icon></button>
        </ng-template>

        <div class="toolbar-row" style="margin-top: 16px"><h2 style="margin: 0; flex: 1">Payment schedule</h2>
          @if (canManage) { <button mat-stroked-button (click)="addPaymentItem()"><mat-icon>add</mat-icon> Add</button> }
        </div>
        <teta-data-table [columns]="paymentColumns" [rows]="detail.paymentSchedule" [actions]="canManage ? paymentActions : undefined"
                         [filterable]="false" [paginate]="false" [exportable]="false" emptyText="No payment schedule captured." />
        <ng-template #paymentActions let-row>
          <button mat-icon-button (click)="editPaymentItem(row)" title="Edit"><mat-icon>edit</mat-icon></button>
        </ng-template>
      </div>
    </div>
  `
})
export class ContractOverviewComponent implements OnChanges {
  @Input({ required: true }) detail!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  obligationColumns: Column[] = [
    { key: 'type', label: 'Type' }, { key: 'description', label: 'Description' }, { key: 'ownerName', label: 'Owner' },
    { key: 'dueDate', label: 'Due', type: 'date' }, { key: 'status', label: 'Status', type: 'status' }, { key: 'isOverdue', label: 'Overdue', type: 'bool' }
  ];
  paymentColumns: Column[] = [
    { key: 'description', label: 'Description' }, { key: 'amount', label: 'Amount', type: 'money' }, { key: 'plannedDate', label: 'Planned date', type: 'date' }
  ];

  get canManage(): boolean { return this.auth.has(P.contractManage); }
  get canSign(): boolean { return this.auth.hasAny(P.contractManage, P.contractApprove); }

  ngOnChanges(): void {}

  async edit(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit contract', fields: updateContractFields(), value: { title: this.detail.summary.title, contractManagerName: this.detail.summary.contractManagerName, poReference: this.detail.poReference } }, '760px');
    if (v) this.api.put(`contracts/${this.detail.summary.id}`, v).subscribe(() => this.changed.emit());
  }

  async sign(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Record signature', fields: [
      { key: 'signedDate', label: 'Signed date', type: 'date', required: true },
      { key: 'signedDocumentId', label: 'Signed document ID', required: true, hint: 'Upload the signed document first (Documents tab) and paste its ID here' }
    ] }, '620px');
    if (v) this.api.post(`contracts/${this.detail.summary.id}/sign`, v).subscribe(() => {
      this.snack.open('Signature recorded.', 'OK', { duration: 3000 });
      this.changed.emit();
    });
  }

  async addObligation(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Add obligation', fields: obligationFields(this.refs) }, '780px');
    if (v) this.api.post(`contracts/${this.detail.summary.id}/obligations`, v).subscribe(() => this.changed.emit());
  }

  async editObligation(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit obligation', fields: obligationFields(this.refs), value: row }, '780px');
    if (v) this.api.put(`contracts/${this.detail.summary.id}/obligations/${row.id}`, v).subscribe(() => this.changed.emit());
  }

  async addPaymentItem(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Add payment schedule item', fields: paymentScheduleFields() }, '620px');
    if (v) this.api.post(`contracts/${this.detail.summary.id}/payment-schedule`, v).subscribe(() => this.changed.emit());
  }

  async editPaymentItem(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit payment schedule item', fields: paymentScheduleFields(), value: row }, '620px');
    if (v) this.api.put(`contracts/${this.detail.summary.id}/payment-schedule/${row.id}`, v).subscribe(() => this.changed.emit());
  }
}
