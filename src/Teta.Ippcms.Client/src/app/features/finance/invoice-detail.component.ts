import { DecimalPipe } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { DocumentsPanelComponent } from '../../shared/documents-panel.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { WorkflowPanelComponent } from '../../shared/workflow-panel.component';

/** A single invoice: three-way validation result, certification workflow and payment history (FR-FIN-003..007). */
@Component({
  selector: 'teta-invoice-detail',
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatIconModule, DataTableComponent, DocumentsPanelComponent, StatusChipComponent, WorkflowPanelComponent],
  template: `
    @if (invoice(); as i) {
      <div class="page">
        <div class="page-header">
          <h1>{{ i.number }} – {{ i.supplierInvoiceNumber }}</h1>
          <teta-status [value]="i.status" />
          <div class="subtitle">{{ i.supplierName }} · {{ i.contractNumber }} · {{ i.projectReference }}</div>
        </div>
        <div class="grid cols-2">
          <div class="card">
            <dl class="dl small">
              <dt>Amount</dt><dd>R {{ i.amount | number }} + VAT R {{ i.vatAmount | number }}</dd>
              <dt>Invoice date</dt><dd>{{ i.invoiceDate }}</dd><dt>Received</dt><dd>{{ i.receivedDate }}</dd>
              <dt>PO reference</dt><dd>{{ i.poReference ?? '—' }}</dd><dt>Deliverable</dt><dd>{{ i.deliverableName ?? '—' }}</dd>
              <dt>Possible duplicate</dt><dd>{{ i.potentialDuplicate ? 'Yes' : 'No' }}</dd>
              <dt>Certified</dt><dd>{{ i.certifiedAtUtc ?? '—' }} {{ i.certifiedBy }}</dd>
              @if (i.rejectionReason) { <dt>Rejection reason</dt><dd>{{ i.rejectionReason }}</dd> }
            </dl>
            @if (i.validationMessages?.length) {
              <div class="warn-banner">
                @for (m of i.validationMessages; track m) { <div>{{ m }}</div> }
              </div>
            }
          </div>
          <div class="card">
            <h2 style="margin-top: 0">Payments</h2>
            <teta-data-table [columns]="paymentColumns" [rows]="i.payments" [filterable]="false" [paginate]="false" [exportable]="false"
                             emptyText="No payments recorded via the ERP interface yet." />
          </div>
        </div>
        <div class="card" style="margin-top: 16px">
          <h2 style="margin-top: 0">Certification approvals</h2>
          <teta-workflow entityType="Invoice" [entityId]="i.id" />
        </div>
        <div class="card" style="margin-top: 16px"><teta-documents parentType="Invoice" [parentId]="i.id" /></div>
      </div>
    }
  `
})
export class InvoiceDetailComponent implements OnChanges {
  @Input() id!: string;
  private readonly api = inject(ApiService);
  readonly invoice = signal<any | null>(null);

  paymentColumns: Column[] = [
    { key: 'erpReference', label: 'ERP reference' }, { key: 'amount', label: 'Amount', type: 'money' },
    { key: 'paymentDate', label: 'Date', type: 'date' }, { key: 'status', label: 'Status', type: 'status' }, { key: 'bankReference', label: 'Bank reference' }
  ];

  ngOnChanges(): void {
    this.api.get(`finance/invoices/${this.id}`).subscribe(i => this.invoice.set(i));
  }
}
