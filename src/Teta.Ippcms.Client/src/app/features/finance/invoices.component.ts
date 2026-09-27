import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { registerInvoiceFields } from './finance-forms';

/** Invoice register: three-way check (order/receipt/invoice), validation and certification for payment (FR-FIN-003..005). */
@Component({
  selector: 'teta-invoices',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Invoices</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="register()"><mat-icon>add</mat-icon> Register invoice</button> }
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" [actions]="actions" (rowClick)="open($event)" emptyText="No invoices found." exportName="invoices" />
      <ng-template #actions let-row>
        @if (canManage && (row.status === 'Registered' || row.status === 'ValidationFailed')) {
          <button mat-icon-button matTooltip="Validate (three-way check)" (click)="validate(row, $event)"><mat-icon>fact_check</mat-icon></button>
        }
        @if (canManage && row.status === 'PendingCertification' && !row.workflowInstanceId) {
          <button mat-icon-button matTooltip="Submit for certification" (click)="submit(row, $event)"><mat-icon>send</mat-icon></button>
        }
        @if (canManage && row.status !== 'Certified' && row.status !== 'SubmittedToErp' && row.status !== 'Paid' && row.status !== 'Rejected' && row.status !== 'Cancelled') {
          <button mat-icon-button matTooltip="Reject" (click)="reject(row, $event)"><mat-icon>cancel</mat-icon></button>
        }
      </ng-template>
    </div>
  `
})
export class InvoicesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly rows = signal<any[]>([]);
  status: string | null = null;

  columns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'supplierInvoiceNumber', label: 'Supplier ref' }, { key: 'supplierName', label: 'Supplier' },
    { key: 'contractNumber', label: 'Contract' }, { key: 'projectReference', label: 'Project' }, { key: 'invoiceDate', label: 'Invoice date', type: 'date' },
    { key: 'amount', label: 'Amount', type: 'money' }, { key: 'vatAmount', label: 'VAT', type: 'money' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'potentialDuplicate', label: 'Possible duplicate', type: 'bool' }
  ];

  get canManage(): boolean { return this.auth.has(P.financeManage); }

  ngOnInit(): void {
    this.status = this.route.snapshot.queryParamMap.get('status');
    this.load();
  }

  load(): void {
    this.api.get<any>('finance/invoices', { status: this.status, pageSize: 500 }).subscribe(r => this.rows.set(r.items));
  }

  open(row: any): void {
    this.router.navigate(['/finance/invoices', row.id]);
  }

  async register(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Register invoice', fields: registerInvoiceFields() }, '780px');
    if (v) this.api.post('finance/invoices', v).subscribe(() => this.load());
  }

  validate(row: any, e: Event): void {
    e.stopPropagation();
    this.api.post(`finance/invoices/${row.id}/validate`).subscribe(() => this.load());
  }

  submit(row: any, e: Event): void {
    e.stopPropagation();
    this.api.post(`finance/invoices/${row.id}/submit`).subscribe(() => {
      this.snack.open('Submitted for certification.', 'OK', { duration: 3000 });
      this.load();
    });
  }

  async reject(row: any, e: Event): Promise<void> {
    e.stopPropagation();
    const v = await openForm(this.dialog, { title: 'Reject invoice', fields: [{ key: 'reason', label: 'Reason', type: 'textarea', required: true }] }, '520px');
    if (v) this.api.post(`finance/invoices/${row.id}/reject`, { reason: v['reason'] }).subscribe(() => this.load());
  }
}
