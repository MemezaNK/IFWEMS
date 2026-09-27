import { DecimalPipe } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { confirmAction, openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { specificationFields } from './procurement-forms';

/** Procurement summary and versioned specification / terms of reference (FR-SCM-002). */
@Component({
  selector: 'teta-procurement-overview',
  standalone: true,
  imports: [DecimalPipe, MatButtonModule, MatIconModule, DataTableComponent, StatusChipComponent],
  template: `
    <div style="padding-top: 12px" class="grid cols-2">
      <div class="card">
        <h2 style="margin-top: 0">Details</h2>
        <dl class="dl small">
          <dt>Requisition</dt><dd>{{ detail.requisitionNumber ?? '—' }}</dd>
          <dt>Method</dt><dd>{{ detail.summary.method }} <span class="muted">{{ detail.methodRuleInfo }}</span></dd>
          <dt>Estimated value</dt><dd>R {{ detail.summary.estimatedValue | number }}</dd>
          <dt>Closing date</dt><dd>{{ detail.summary.closingDateUtc ?? '—' }}</dd>
          <dt>Technical threshold</dt><dd>{{ detail.technicalThreshold ?? '—' }}</dd>
          <dt>Price system</dt><dd>{{ detail.priceSystemCode ?? '—' }}</dd>
          @if (detail.cancellationReason) { <dt>Cancellation reason</dt><dd>{{ detail.cancellationReason }}</dd> }
        </dl>
        @if (canManage && !isTerminal) {
          <div class="toolbar-row">
            <button mat-stroked-button color="warn" (click)="cancel()"><mat-icon>cancel</mat-icon> Cancel</button>
            <button mat-stroked-button (click)="reAdvertise()"><mat-icon>replay</mat-icon> Re-advertise</button>
          </div>
        }
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Specification / terms of reference</h2>
          @if (canManage) { <button mat-stroked-button (click)="newVersion()"><mat-icon>add</mat-icon> New version</button> }
        </div>
        <teta-data-table [columns]="specColumns" [rows]="specs()" [actions]="specActions" [filterable]="false" [paginate]="false" [exportable]="false"
                         emptyText="No specification captured yet." />
        <ng-template #specActions let-row>
          @if (canManage && row.status === 'Draft') {
            <button mat-icon-button (click)="submitSpec(row)" title="Submit for review"><mat-icon>send</mat-icon></button>
          }
          @if (canManage && row.status === 'UnderReview') {
            <button mat-icon-button (click)="approveSpec(row)" title="Approve"><mat-icon>check_circle</mat-icon></button>
          }
        </ng-template>
      </div>
    </div>
  `
})
export class ProcurementOverviewComponent implements OnChanges {
  @Input({ required: true }) detail!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly specs = signal<any[]>([]);

  specColumns: Column[] = [
    { key: 'versionNumber', label: 'Version', type: 'number' }, { key: 'title', label: 'Title' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'approvedBy', label: 'Approved by' }, { key: 'approvedAtUtc', label: 'Approved', type: 'datetime' }
  ];

  get canManage(): boolean { return this.auth.hasAny(P.procurementManage, P.projectManage); }
  get isTerminal(): boolean { return this.detail.summary.status === 'Awarded' || this.detail.summary.status === 'Cancelled'; }

  ngOnChanges(): void { this.specs.set(this.detail.specifications ?? []); }

  async newVersion(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New specification version', fields: specificationFields() }, '860px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/specifications`, v).subscribe(() => this.changed.emit());
  }

  submitSpec(row: any): void {
    this.api.post(`procurement/${this.detail.summary.id}/specifications/${row.id}/submit`).subscribe(() => this.changed.emit());
  }

  approveSpec(row: any): void {
    this.api.post(`procurement/${this.detail.summary.id}/specifications/${row.id}/approve`).subscribe(() => this.changed.emit());
  }

  async cancel(): Promise<void> {
    const reason = await confirmAction(this.dialog, 'Cancel procurement', 'This process will be closed and cannot be resumed.', 'Reason');
    if (reason === undefined) return;
    this.api.post(`procurement/${this.detail.summary.id}/cancel`, { reason }).subscribe(() => {
      this.snack.open('Procurement cancelled.', 'OK', { duration: 3000 });
      this.changed.emit();
    });
  }

  async reAdvertise(): Promise<void> {
    const reason = await confirmAction(this.dialog, 'Re-advertise', 'This process will be closed and a new copy created for re-advertisement.', 'Reason');
    if (reason === undefined) return;
    this.api.post(`procurement/${this.detail.summary.id}/re-advertise`, { reason }).subscribe(() => {
      this.snack.open('Re-advertised as a new procurement.', 'OK', { duration: 3000 });
      this.changed.emit();
    });
  }
}
