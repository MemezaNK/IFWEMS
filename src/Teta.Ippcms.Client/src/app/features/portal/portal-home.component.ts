import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { KpiCardsComponent } from '../../shared/kpi-cards.component';
import { registerInvoiceFields } from '../finance/finance-forms';

/** Supplier / implementing-partner self-service portal: own bids, contracts, deliverables and invoices only (SRS §13). */
@Component({
  selector: 'teta-portal-home',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatTabsModule, DataTableComponent, KpiCardsComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>My supplier portal</h1></div>
      @if (summary(); as s) {
        <teta-kpis [kpis]="[
          { label: 'Active contracts', value: s.activeContracts, status: 'Info' },
          { label: 'Deliverables due', value: s.deliverablesDue, status: s.deliverablesDue > 0 ? 'Amber' : 'Green' },
          { label: 'Deliverables rejected', value: s.deliverablesRejected, status: s.deliverablesRejected > 0 ? 'Red' : 'Green' },
          { label: 'Invoices in progress', value: s.invoicesInProgress, status: 'Info' },
          { label: 'Paid this year', value: s.paidThisYear, unit: 'ZAR', status: 'Green' }
        ]" />
      }
      <mat-tab-group animationDuration="0">
        <mat-tab label="My bids">
          <div style="padding-top: 12px"><teta-data-table [columns]="bidColumns" [rows]="bids()" emptyText="No bids submitted yet." /></div>
        </mat-tab>
        <mat-tab label="My contracts">
          <div style="padding-top: 12px"><teta-data-table [columns]="contractColumns" [rows]="contracts()" emptyText="No contracts." /></div>
        </mat-tab>
        <mat-tab label="Deliverables">
          <div style="padding-top: 12px">
            <teta-data-table [columns]="deliverableColumns" [rows]="deliverables()" [actions]="deliverableActions" emptyText="No deliverables." />
            <ng-template #deliverableActions let-row>
              @if (row.acceptanceStatus === 'Pending' || row.acceptanceStatus === 'Rejected') {
                <button mat-button color="primary" (click)="submitDeliverable(row)">Submit</button>
              }
            </ng-template>
          </div>
        </mat-tab>
        <mat-tab label="Invoices">
          <div style="padding-top: 12px">
            <div class="toolbar-row"><span class="spacer"></span><button mat-flat-button color="primary" (click)="submitInvoice()"><mat-icon>add</mat-icon> Submit invoice</button></div>
            <teta-data-table [columns]="invoiceColumns" [rows]="invoices()" emptyText="No invoices submitted yet." />
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `
})
export class PortalHomeComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly summary = signal<any | null>(null);
  readonly bids = signal<any[]>([]);
  readonly contracts = signal<any[]>([]);
  readonly deliverables = signal<any[]>([]);
  readonly invoices = signal<any[]>([]);

  bidColumns: Column[] = [
    { key: 'procurementNumber', label: 'Procurement' }, { key: 'procurementTitle', label: 'Title' }, { key: 'bidReference', label: 'Bid ref.' },
    { key: 'receivedAtUtc', label: 'Received', type: 'datetime' }, { key: 'bidAmount', label: 'Amount', type: 'money' },
    { key: 'procurementStatus', label: 'Procurement status', type: 'status' }, { key: 'outcome', label: 'Outcome', type: 'status' }
  ];
  contractColumns: Column[] = [
    { key: 'contractNumber', label: 'Contract' }, { key: 'title', label: 'Title' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'originalValue', label: 'Original value', type: 'money' }, { key: 'revisedValue', label: 'Revised value', type: 'money' },
    { key: 'currentEndDate', label: 'End date', type: 'date' }, { key: 'contractManager', label: 'Contract manager' },
    { key: 'openDeliverables', label: 'Open deliverables', type: 'number' }, { key: 'invoiced', label: 'Invoiced', type: 'money' }, { key: 'paid', label: 'Paid', type: 'money' }
  ];
  deliverableColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'contractNumber', label: 'Contract' }, { key: 'name', label: 'Name' }, { key: 'dueDate', label: 'Due', type: 'date' },
    { key: 'payableAmount', label: 'Payable', type: 'money' }, { key: 'acceptanceStatus', label: 'Status', type: 'status' },
    { key: 'submittedAtUtc', label: 'Submitted', type: 'datetime' }, { key: 'rejectionReason', label: 'Rejection reason' }
  ];
  invoiceColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'supplierInvoiceNumber', label: 'Your invoice no.' }, { key: 'contractNumber', label: 'Contract' },
    { key: 'invoiceDate', label: 'Date', type: 'date' }, { key: 'amount', label: 'Amount', type: 'money' }, { key: 'vatAmount', label: 'VAT', type: 'money' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'rejectionReason', label: 'Rejection reason' }, { key: 'paid', label: 'Paid', type: 'money' }, { key: 'paidOn', label: 'Paid on', type: 'date' }
  ];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get('portal/summary').subscribe(s => this.summary.set(s));
    this.api.get<any[]>('portal/bids').subscribe(r => this.bids.set(r));
    this.api.get<any[]>('portal/contracts').subscribe(r => this.contracts.set(r));
    this.api.get<any[]>('portal/deliverables').subscribe(r => this.deliverables.set(r));
    this.api.get<any[]>('portal/invoices').subscribe(r => this.invoices.set(r));
  }

  async submitDeliverable(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: `Submit ${row.number}`, fields: [{ key: 'note', label: 'Note (optional)', type: 'textarea' }] }, '560px');
    if (v === undefined) return;
    this.api.post(`portal/deliverables/${row.id}/submit`, v).subscribe(() => {
      this.snack.open('Deliverable submitted for acceptance.', 'OK', { duration: 3000 });
      this.load();
    });
  }

  async submitInvoice(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Submit invoice', fields: registerInvoiceFields() }, '640px');
    if (v) this.api.post('portal/invoices', v).subscribe(() => {
      this.snack.open('Invoice submitted.', 'OK', { duration: 3000 });
      this.load();
    });
  }
}
