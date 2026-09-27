import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { bidFields, publicationFields } from './procurement-forms';

/** Advertisement / publication and bid receipt and opening (FR-SCM-004..006). Bid detail is hidden until a committee member has declared (BR safeguard). */
@Component({
  selector: 'teta-procurement-sourcing',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div style="padding-top: 12px" class="grid cols-2">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Publications</h2>
          @if (canManage) { <button mat-stroked-button (click)="publish()"><mat-icon>campaign</mat-icon> Publish</button> }
        </div>
        <teta-data-table [columns]="pubColumns" [rows]="publications()" [filterable]="false" [paginate]="false" [exportable]="false"
                         emptyText="Not yet advertised." />
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Bids</h2>
          @if (canManage) { <button mat-stroked-button (click)="registerBid()"><mat-icon>add</mat-icon> Register bid</button>
            <button mat-stroked-button (click)="openBids()"><mat-icon>lock_open</mat-icon> Open bids</button> }
        </div>
        @if (!detail.canAccessBids) {
          <p class="muted">Bid detail is restricted to declared committee members and administrators.</p>
        } @else {
          <teta-data-table [columns]="bidColumns" [rows]="bids()" [actions]="canManage ? bidActions : undefined" [filterable]="false" [paginate]="false"
                           [exportable]="false" emptyText="No bids received yet." />
          <ng-template #bidActions let-row>
            @if (row.status !== 'Invalid') { <button mat-icon-button (click)="invalidate(row)" title="Invalidate"><mat-icon>block</mat-icon></button> }
          </ng-template>
        }
      </div>
    </div>
  `
})
export class ProcurementSourcingComponent implements OnChanges {
  @Input({ required: true }) detail!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  readonly publications = signal<any[]>([]);
  readonly bids = signal<any[]>([]);

  pubColumns: Column[] = [
    { key: 'channel', label: 'Channel' }, { key: 'reference', label: 'Reference' }, { key: 'publishedOn', label: 'Published', type: 'date' },
    { key: 'closingDateUtc', label: 'Closes', type: 'datetime' }, { key: 'url', label: 'URL' }
  ];
  bidColumns: Column[] = [
    { key: 'supplierName', label: 'Supplier' }, { key: 'bbbeeLevel', label: 'B-BBEE', type: 'number' }, { key: 'bidReference', label: 'Reference' },
    { key: 'receivedAtUtc', label: 'Received', type: 'datetime' }, { key: 'isLate', label: 'Late', type: 'bool' },
    { key: 'bidAmount', label: 'Amount', type: 'money' }, { key: 'status', label: 'Status', type: 'status' }, { key: 'rank', label: 'Rank', type: 'number' }
  ];

  get canManage(): boolean { return this.auth.has(P.procurementManage); }

  ngOnChanges(): void {
    this.publications.set(this.detail.publications ?? []);
    if (this.detail.canAccessBids) this.loadBids();
  }

  loadBids(): void {
    this.api.get<any[]>(`procurement/${this.detail.summary.id}/bids`).subscribe(r => this.bids.set(r));
  }

  async publish(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Publish advertisement', fields: publicationFields() }, '760px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/publications`, v).subscribe(() => this.changed.emit());
  }

  async registerBid(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Register bid', fields: bidFields(this.refs) }, '760px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/bids`, v).subscribe(() => { this.changed.emit(); this.loadBids(); });
  }

  openBids(): void {
    this.api.post(`procurement/${this.detail.summary.id}/bids/open`).subscribe(() => this.loadBids());
  }

  async invalidate(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Invalidate bid', fields: [{ key: 'reason', label: 'Reason', type: 'textarea', required: true }] }, '520px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/bids/${row.id}/invalidate`, { reason: v['reason'] }).subscribe(() => this.loadBids());
  }
}
