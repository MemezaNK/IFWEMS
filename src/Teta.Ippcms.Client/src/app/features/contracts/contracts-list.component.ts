import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { fromAwardFields, nonBidFields } from './contract-forms';
import { ReferenceService } from '../../core/reference.service';

/** Contract register: from-award, non-bid and directly-created contracts (SRS §5.6). */
@Component({
  selector: 'teta-contracts-list',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatCheckboxModule, FormsModule, RouterLink, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Contracts</h1>
        <a mat-stroked-button routerLink="/contracts/dashboard"><mat-icon>dashboard</mat-icon> Dashboard</a>
        @if (canManage) {
          <button mat-stroked-button (click)="fromAward()"><mat-icon>gavel</mat-icon> From award</button>
          <button mat-flat-button color="primary" (click)="nonBid()"><mat-icon>add</mat-icon> Non-bid contract</button>
        }
      </div>
      <div class="toolbar-row">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="min-width: 260px">
          <mat-label>Search</mat-label>
          <input matInput [(ngModel)]="search" (ngModelChange)="load()" />
        </mat-form-field>
        <mat-checkbox [(ngModel)]="expiringOnly" (ngModelChange)="load()">Expiring within 90 days</mat-checkbox>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="open($event)" emptyText="No contracts found." exportName="contracts" [filterable]="false" />
    </div>
  `
})
export class ContractsListComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  readonly rows = signal<any[]>([]);
  search = '';
  expiringOnly = false;

  columns: Column[] = [
    { key: 'contractNumber', label: 'Number' }, { key: 'title', label: 'Title' }, { key: 'projectReference', label: 'Project' },
    { key: 'supplierName', label: 'Supplier' }, { key: 'revisedValue', label: 'Revised value', type: 'money' },
    { key: 'currentEndDate', label: 'End date', type: 'date' }, { key: 'daysToExpiry', label: 'Days to expiry', type: 'number' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'signatureStatus', label: 'Signature', type: 'status' },
    { key: 'openBreaches', label: 'Open breaches', type: 'number' }, { key: 'performanceRating', label: 'Rating', type: 'number' }
  ];

  get canManage(): boolean { return this.auth.has(P.contractManage); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any>('contracts', { search: this.search, expiringOnly: this.expiringOnly || null, pageSize: 500 }).subscribe(r => this.rows.set(r.items));
  }

  open(row: any): void {
    this.router.navigate(['/contracts', row.id]);
  }

  async fromAward(): Promise<void> {
    const awards = await new Promise<any[]>(r => this.api.get<any[]>('procurement/awards', { pendingContractOnly: true }).subscribe(r));
    if (!awards.length) { alert('No awards are ready for a contract to be created (conditions must be satisfied first).'); return; }
    const v = await openForm(this.dialog, { title: 'Create contract from award', fields: [
      { key: 'awardId', label: 'Award', type: 'select', required: true, options: awards.map(a => ({ value: a.id, label: `${a.number} – ${a.supplierName} (R ${a.amount})` })), wide: true },
      ...fromAwardFields()
    ] }, '780px');
    if (v) this.api.post('contracts/from-award', v).subscribe((c: any) => this.router.navigate(['/contracts', c.summary.id]));
  }

  async nonBid(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New non-bid contract', fields: nonBidFields(this.refs) }, '780px');
    if (v) this.api.post('contracts/non-bid', v).subscribe((c: any) => this.router.navigate(['/contracts', c.summary.id]));
  }
}
