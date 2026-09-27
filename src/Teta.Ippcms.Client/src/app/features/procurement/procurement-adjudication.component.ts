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
import { StatusChipComponent } from '../../shared/status-chip.component';
import { WorkflowPanelComponent } from '../../shared/workflow-panel.component';
import { communicationFields } from './procurement-forms';
import { ReferenceService } from '../../core/reference.service';

/** Due diligence, adjudication recommendation, award and bidder communications (FR-SCM-010..013). */
@Component({
  selector: 'teta-procurement-adjudication',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent, StatusChipComponent, WorkflowPanelComponent],
  template: `
    <div style="padding-top: 12px">
      @if (!detail.canAccessBids) {
        <p class="muted">Adjudication is restricted to declared committee members and administrators.</p>
      } @else {
        <div class="card">
          <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Due diligence</h2>
            @if (canManage) { <button mat-stroked-button (click)="addDueDiligence()"><mat-icon>fact_check</mat-icon> Capture</button> }
          </div>
          <teta-data-table [columns]="ddColumns" [rows]="dueDiligence()" [filterable]="false" [paginate]="false" [exportable]="false"
                           emptyText="No due diligence captured yet." />
        </div>

        <div class="card" style="margin-top: 16px">
          <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Adjudication</h2>
            @if (detail.adjudication) { <teta-status [value]="detail.adjudication.decision" /> }
            @if (canManage && !detail.adjudication) { <button mat-flat-button color="primary" (click)="submitAdjudication()">Submit recommendation</button> }
          </div>
          @if (detail.adjudication; as a) {
            <dl class="dl small">
              <dt>Recommended supplier</dt><dd>{{ a.recommendedSupplier }}</dd><dt>Amount</dt><dd>R {{ a.recommendedAmount }}</dd>
              <dt>Recommendation</dt><dd>{{ a.recommendation }}</dd><dt>Conditions</dt><dd>{{ a.conditions }}</dd>
              <dt>Decision</dt><dd>{{ a.decision }}</dd><dt>Reasons</dt><dd>{{ a.reasons }}</dd>
            </dl>
            <teta-workflow entityType="Procurement" [entityId]="detail.summary.id" />
          } @else { <p class="muted">No adjudication submitted yet.</p> }
        </div>

        @if (detail.award; as aw) {
          <div class="card" style="margin-top: 16px">
            <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Award {{ aw.number }}</h2><teta-status [value]="aw.status" />
              @if (canAward && !aw.conditionsSatisfied) { <button mat-flat-button color="primary" (click)="satisfyConditions(aw)">Mark conditions satisfied</button> }
            </div>
            <dl class="dl small">
              <dt>Supplier</dt><dd>{{ aw.supplierName }}</dd><dt>Amount</dt><dd>R {{ aw.amount }}</dd><dt>Award date</dt><dd>{{ aw.awardDate }}</dd>
              <dt>Conditions</dt><dd>{{ aw.conditions }}</dd><dt>Conditions satisfied</dt><dd>{{ aw.conditionsSatisfied ? 'Yes' : 'No' }}</dd>
              @if (aw.contractId) { <dt>Contract</dt><dd>Created</dd> }
            </dl>
          </div>
        }

        <div class="card" style="margin-top: 16px">
          <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Bidder communications</h2>
            @if (canManage) { <button mat-stroked-button (click)="addCommunication()"><mat-icon>mail</mat-icon> Log communication</button> }
          </div>
          <teta-data-table [columns]="commColumns" [rows]="communications()" [filterable]="false" [paginate]="false" [exportable]="false"
                           emptyText="No communications logged yet." />
        </div>
      }
    </div>
  `
})
export class ProcurementAdjudicationComponent implements OnChanges {
  @Input({ required: true }) detail!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly dueDiligence = signal<any[]>([]);
  readonly communications = signal<any[]>([]);

  ddColumns: Column[] = [
    { key: 'csdVerified', label: 'CSD verified', type: 'bool' }, { key: 'taxCompliant', label: 'Tax compliant', type: 'bool' },
    { key: 'notRestricted', label: 'Not restricted', type: 'bool' }, { key: 'notOnDefaultersList', label: 'Not defaulting', type: 'bool' },
    { key: 'referencesChecked', label: 'References checked', type: 'bool' }, { key: 'capacityConfirmed', label: 'Capacity confirmed', type: 'bool' },
    { key: 'outcome', label: 'Outcome', type: 'status' }, { key: 'completedBy', label: 'Completed by' }
  ];
  commColumns: Column[] = [
    { key: 'type', label: 'Type' }, { key: 'supplierName', label: 'Supplier' }, { key: 'date', label: 'Date', type: 'date' },
    { key: 'subject', label: 'Subject' }, { key: 'outcome', label: 'Outcome' }
  ];

  get canManage(): boolean { return this.auth.has(P.procurementManage); }
  get canAward(): boolean { return this.auth.hasAny(P.procurementAward, P.procurementManage); }

  ngOnChanges(): void {
    if (!this.detail.canAccessBids) return;
    this.api.get<any[]>(`procurement/${this.detail.summary.id}/due-diligence`).subscribe(r => this.dueDiligence.set(r));
    this.api.get<any[]>(`procurement/${this.detail.summary.id}/communications`).subscribe(r => this.communications.set(r));
  }

  async addDueDiligence(): Promise<void> {
    const bids = await new Promise<any[]>(r => this.api.get<any[]>(`procurement/${this.detail.summary.id}/bids`).subscribe(r));
    const v = await openForm(this.dialog, { title: 'Capture due diligence', fields: [
      { key: 'bidId', label: 'Bid', type: 'select', required: true, options: bids.map(b => ({ value: b.id, label: `${b.supplierName} (${b.bidReference})` })), wide: true },
      { key: 'csdVerified', label: 'CSD verified', type: 'checkbox' }, { key: 'taxCompliant', label: 'Tax compliant', type: 'checkbox' },
      { key: 'notRestricted', label: 'Not on restricted supplier list', type: 'checkbox' }, { key: 'notOnDefaultersList', label: 'Not on defaulters list', type: 'checkbox' },
      { key: 'referencesChecked', label: 'References checked', type: 'checkbox' }, { key: 'capacityConfirmed', label: 'Capacity confirmed', type: 'checkbox' },
      { key: 'notes', label: 'Notes', type: 'textarea' }
    ] }, '760px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/due-diligence`, v).subscribe(() => this.ngOnChanges());
  }

  async submitAdjudication(): Promise<void> {
    const bids = await new Promise<any[]>(r => this.api.get<any[]>(`procurement/${this.detail.summary.id}/bids`).subscribe(r));
    const v = await openForm(this.dialog, { title: 'Submit adjudication recommendation', fields: [
      { key: 'recommendedBidId', label: 'Recommended bid', type: 'select', required: true, options: bids.map(b => ({ value: b.id, label: `${b.supplierName} (${b.bidReference})` })), wide: true },
      { key: 'recommendation', label: 'Recommendation', type: 'textarea', required: true, wide: true },
      { key: 'conditions', label: 'Conditions', type: 'textarea' }
    ] }, '760px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/adjudication`, v).subscribe(() => {
      this.snack.open('Adjudication submitted for approval.', 'OK', { duration: 3000 });
      this.changed.emit();
    });
  }

  async satisfyConditions(award: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Award conditions satisfied', fields: [{ key: 'note', label: 'Note', type: 'textarea', required: true }] }, '520px');
    if (v) this.api.post(`procurement/awards/${award.id}/conditions-satisfied`, { note: v['note'] }).subscribe(() => this.changed.emit());
  }

  async addCommunication(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Log bidder communication', fields: communicationFields(this.refs) }, '780px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/communications`, v).subscribe(() => this.ngOnChanges());
  }
}
