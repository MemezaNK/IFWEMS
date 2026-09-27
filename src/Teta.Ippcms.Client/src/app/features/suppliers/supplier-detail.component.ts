import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { DocumentsPanelComponent } from '../../shared/documents-panel.component';
import { openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { supplierFields } from './supplier-forms';

/** Supplier record, contract/bid/review history and portal-user linkage (FR-CON-014, FR-SCM-018). */
@Component({
  selector: 'teta-supplier-detail',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatTabsModule, DataTableComponent, StatusChipComponent, DocumentsPanelComponent],
  template: `
    @if (history(); as h) {
      <div class="page">
        <div class="page-header">
          <h1>{{ h.supplier.supplierNumber }} – {{ h.supplier.legalName }}</h1>
          <teta-status [value]="h.supplier.status" />
          @if (canManage) { <button mat-stroked-button (click)="edit()"><mat-icon>edit</mat-icon> Edit</button>
            <button mat-stroked-button (click)="linkUser()"><mat-icon>link</mat-icon> Link portal user</button> }
          <div class="subtitle">{{ h.supplier.tradingName }} · Reg {{ h.supplier.registrationNumber }} · CSD {{ h.supplier.csdNumber }} · B-BBEE {{ h.supplier.bbbeeLevel }}</div>
        </div>
        <mat-tab-group animationDuration="0">
          <mat-tab label="Summary">
            <div style="padding-top: 12px" class="grid cols-2">
              <div class="card">
                <dl class="dl small">
                  <dt>Email</dt><dd>{{ h.supplier.email }}</dd><dt>Phone</dt><dd>{{ h.supplier.phone }}</dd>
                  <dt>Address</dt><dd>{{ h.supplier.address }}</dd><dt>Province</dt><dd>{{ h.supplier.province }}</dd>
                  <dt>Implementing partner</dt><dd>{{ h.supplier.isImplementingPartner ? 'Yes' : 'No' }}</dd>
                  <dt>CSD verified</dt><dd>{{ h.supplier.csdVerified ? 'Yes' : 'No' }}</dd>
                  <dt>Tax clearance expiry</dt><dd>{{ h.supplier.taxClearanceExpiry ?? '—' }}</dd>
                  <dt>ERP vendor code</dt><dd>{{ h.supplier.erpVendorCode ?? '—' }}</dd>
                </dl>
              </div>
              <div class="card">
                <dl class="dl small"><dt>Average performance score</dt><dd>{{ h.averageScore ?? '—' }}</dd>
                  <dt>Open breaches</dt><dd>{{ h.openBreaches }}</dd></dl>
              </div>
            </div>
          </mat-tab>
          <mat-tab label="Contracts">
            <div style="padding-top: 12px">
              <teta-data-table [columns]="contractColumns" [rows]="h.contracts" emptyText="No contracts yet." exportName="supplier-contracts" />
            </div>
          </mat-tab>
          <mat-tab label="Performance reviews">
            <div style="padding-top: 12px">
              <teta-data-table [columns]="reviewColumns" [rows]="h.reviews" emptyText="No performance reviews yet." exportName="supplier-reviews" />
            </div>
          </mat-tab>
          <mat-tab label="Bids">
            <div style="padding-top: 12px">
              <teta-data-table [columns]="bidColumns" [rows]="h.bids" emptyText="No bid history." exportName="supplier-bids" />
            </div>
          </mat-tab>
          <mat-tab label="Documents">
            <ng-template matTabContent>
              <div style="padding-top: 12px" class="card"><teta-documents parentType="Supplier" [parentId]="h.supplier.id" /></div>
            </ng-template>
          </mat-tab>
        </mat-tab-group>
      </div>
    }
  `
})
export class SupplierDetailComponent implements OnChanges {
  @Input() id!: string;
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly history = signal<any | null>(null);

  contractColumns: Column[] = [
    { key: 'contractNumber', label: 'Number' }, { key: 'title', label: 'Title' }, { key: 'revisedValue', label: 'Value', type: 'money' },
    { key: 'startDate', label: 'Start', type: 'date' }, { key: 'currentEndDate', label: 'End', type: 'date' }, { key: 'status', label: 'Status', type: 'status' }
  ];
  reviewColumns: Column[] = [
    { key: 'period', label: 'Period' }, { key: 'reviewDate', label: 'Review date', type: 'date' },
    { key: 'overallScore', label: 'Score', type: 'number' }, { key: 'rating', label: 'Rating', type: 'status' }
  ];
  bidColumns: Column[] = [
    { key: 'procurementNumber', label: 'Procurement' }, { key: 'title', label: 'Title' }, { key: 'bidAmount', label: 'Amount', type: 'money' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'receivedAtUtc', label: 'Received', type: 'datetime' }
  ];

  get canManage(): boolean { return this.auth.has(P.supplierManage); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.get(`suppliers/${this.id}/history`).subscribe(h => this.history.set(h));
  }

  async edit(): Promise<void> {
    const s = this.history()!.supplier;
    const v = await openForm(this.dialog, { title: 'Edit supplier', fields: supplierFields(), value: s }, '860px');
    if (!v) return;
    const duplicates = await new Promise<any[]>(r => this.api.post<any[]>('suppliers/duplicates', v, { excludeId: s.id }).subscribe(r));
    let body = v;
    if (duplicates.length) {
      const list = duplicates.map((d: any) => `${d.legalName} (${d.supplierNumber}) – ${d.reason}`).join('\n');
      if (!confirm(`Possible duplicate supplier(s) found:\n\n${list}\n\nSave anyway?`)) return;
      body = { ...v, confirmNotDuplicate: true };
    }
    this.api.put(`suppliers/${s.id}`, body).subscribe(() => this.load());
  }

  async linkUser(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Link supplier-portal user', fields: [
      { key: 'userId', label: 'User', type: 'select', required: true, options: this.refs.users(), wide: true }
    ] }, '620px');
    if (v) this.api.post(`suppliers/${this.id}/portal-users`, v).subscribe(() => {
      this.snack.open('Portal user linked.', 'OK', { duration: 3000 });
    });
  }
}
