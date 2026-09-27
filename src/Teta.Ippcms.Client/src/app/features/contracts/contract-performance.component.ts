import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { breachFields, reviewFields } from './contract-forms';

/** Performance reviews, breach register and close-out (FR-CON-007..010). */
@Component({
  selector: 'teta-contract-performance',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div style="padding-top: 12px" class="grid cols-2">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Performance reviews</h2>
          @if (canManage) { <button mat-stroked-button (click)="addReview()"><mat-icon>add</mat-icon> New review</button> }
        </div>
        <teta-data-table [columns]="reviewColumns" [rows]="detail.reviews" [filterable]="false" [paginate]="false" [exportable]="false"
                         emptyText="No performance reviews yet." />
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Breaches</h2>
          @if (canManage) { <button mat-stroked-button (click)="addBreach()"><mat-icon>add</mat-icon> Log breach</button> }
        </div>
        <teta-data-table [columns]="breachColumns" [rows]="detail.breaches" [actions]="canManage ? breachActions : undefined"
                         [filterable]="false" [paginate]="false" [exportable]="false" emptyText="No breaches recorded." />
        <ng-template #breachActions let-row>
          @if (row.isOpen) { <button mat-icon-button (click)="editBreach(row)" title="Update"><mat-icon>edit</mat-icon></button> }
        </ng-template>
      </div>
      <div class="card" style="grid-column: 1 / -1">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Close-out</h2>
          @if (canManage) { <button mat-stroked-button (click)="checkCloseOut()"><mat-icon>fact_check</mat-icon> Check readiness</button>
            <button mat-flat-button color="primary" (click)="close()">Close contract</button> }
        </div>
        @if (closeOut(); as co) {
          @for (c of co.checks; track c.item) {
            <div class="small">{{ c.met ? '✔' : '✖' }} {{ c.item }} <span class="muted">{{ c.detail }}</span></div>
          }
          <p [class.warn-banner]="!co.canClose" class="small" style="margin-top:8px">{{ co.canClose ? 'Ready to close.' : 'Not yet ready to close — resolve the items above.' }}</p>
        }
        @if (detail.closureNotes) { <p class="small muted">Closure notes: {{ detail.closureNotes }}</p> }
      </div>
    </div>
  `
})
export class ContractPerformanceComponent implements OnChanges {
  @Input({ required: true }) detail!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly closeOut = signal<any | null>(null);

  reviewColumns: Column[] = [
    { key: 'period', label: 'Period' }, { key: 'reviewDate', label: 'Review date', type: 'date' }, { key: 'qualityScore', label: 'Quality', type: 'number' },
    { key: 'timelinessScore', label: 'Timeliness', type: 'number' }, { key: 'complianceScore', label: 'Compliance', type: 'number' },
    { key: 'overallScore', label: 'Overall', type: 'number' }, { key: 'rating', label: 'Rating', type: 'status' }, { key: 'reviewedBy', label: 'Reviewed by' }
  ];
  breachColumns: Column[] = [
    { key: 'description', label: 'Description' }, { key: 'severity', label: 'Severity', type: 'status' }, { key: 'identifiedOn', label: 'Identified', type: 'date' },
    { key: 'remedyDueDate', label: 'Remedy due', type: 'date' }, { key: 'penaltyAmount', label: 'Penalty', type: 'money' }, { key: 'status', label: 'Status', type: 'status' }
  ];

  get canManage(): boolean { return this.auth.has(P.contractManage); }

  ngOnChanges(): void { this.closeOut.set(null); }

  async addReview(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New performance review', fields: reviewFields() }, '780px');
    if (v) this.api.post(`contracts/${this.detail.summary.id}/reviews`, v).subscribe(() => this.changed.emit());
  }

  async addBreach(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Log breach', fields: breachFields() }, '780px');
    if (v) this.api.post(`contracts/${this.detail.summary.id}/breaches`, v).subscribe(() => this.changed.emit());
  }

  async editBreach(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Update breach', fields: breachFields(), value: row }, '780px');
    if (v) this.api.put(`contracts/${this.detail.summary.id}/breaches/${row.id}`, v).subscribe(() => this.changed.emit());
  }

  checkCloseOut(): void {
    this.api.get(`contracts/${this.detail.summary.id}/close-out-checks`).subscribe(r => this.closeOut.set(r));
  }

  async close(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Close contract', fields: [
      { key: 'closureNotes', label: 'Closure notes', type: 'textarea', required: true, wide: true },
      { key: 'approvedExceptionReference', label: 'Approved exception reference (if closing with open items)' }
    ] }, '760px');
    if (v) this.api.post(`contracts/${this.detail.summary.id}/close`, v).subscribe(() => {
      this.snack.open('Contract closed.', 'OK', { duration: 3000 });
      this.changed.emit();
    });
  }
}
