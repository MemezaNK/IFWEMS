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
import { criterionFields } from './procurement-forms';

/** Compliance/technical criteria, evaluator scoring, consolidation and the evaluation report (FR-SCM-007..009, 80/20 or 90/10 preference points). */
@Component({
  selector: 'teta-procurement-evaluation',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div style="padding-top: 12px">
      @if (!detail.canAccessBids) {
        <p class="muted">Evaluation is restricted to declared committee members and administrators.</p>
      } @else {
        <div class="card">
          <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Evaluation criteria</h2>
            @if (canManage) { <button mat-stroked-button (click)="addCriterion()"><mat-icon>add</mat-icon> Add criterion</button>
              <button mat-stroked-button (click)="setup()"><mat-icon>tune</mat-icon> Setup</button> }
          </div>
          <p class="small muted">Technical threshold: {{ detail.technicalThreshold ?? '—' }} · Price system: {{ detail.priceSystemCode ?? '—' }}</p>
          <teta-data-table [columns]="criterionColumns" [rows]="detail.criteria ?? []" [actions]="canManage ? critActions : undefined"
                           [filterable]="false" [paginate]="false" [exportable]="false" emptyText="No evaluation criteria configured." />
          <ng-template #critActions let-row>
            <button mat-icon-button (click)="editCriterion(row)" title="Edit"><mat-icon>edit</mat-icon></button>
            <button mat-icon-button (click)="deleteCriterion(row)" title="Delete"><mat-icon>delete</mat-icon></button>
          </ng-template>
        </div>

        @if (canEvaluate) {
          <div class="card" style="margin-top: 16px">
            <div class="toolbar-row"><h2 style="margin: 0; flex: 1">My scores</h2>
              <button mat-flat-button color="primary" (click)="score()"><mat-icon>edit_note</mat-icon> Enter / update score</button></div>
            <teta-data-table [columns]="scoreColumns" [rows]="myScores()" [filterable]="false" [paginate]="false" [exportable]="false"
                             emptyText="You have not submitted any scores yet." />
          </div>
        }

        <div class="card" style="margin-top: 16px">
          <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Consolidated evaluation report</h2>
            @if (canManage) { <button mat-stroked-button (click)="consolidate()"><mat-icon>calculate</mat-icon> Consolidate</button> }
          </div>
          @if (report(); as r) {
            <teta-data-table [columns]="bidColumns" [rows]="r.bids" [filterable]="false" [paginate]="false" [exportable]="false" exportName="evaluation-report" />
            <p class="small muted">{{ r.notes }}</p>
          } @else { <p class="muted">No evaluation report available yet.</p> }
        </div>
      }
    </div>
  `
})
export class ProcurementEvaluationComponent implements OnChanges {
  @Input({ required: true }) detail!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly myScores = signal<any[]>([]);
  readonly report = signal<any | null>(null);

  criterionColumns: Column[] = [
    { key: 'stage', label: 'Stage' }, { key: 'name', label: 'Name' }, { key: 'weight', label: 'Weight', type: 'number' },
    { key: 'maxScore', label: 'Max score', type: 'number' }, { key: 'isMandatory', label: 'Mandatory', type: 'bool' }, { key: 'sortOrder', label: 'Order', type: 'number' }
  ];
  scoreColumns: Column[] = [
    { key: 'evaluatorName', label: 'Evaluator' }, { key: 'score', label: 'Score', type: 'number' }, { key: 'passed', label: 'Passed', type: 'bool' },
    { key: 'comment', label: 'Comment' }, { key: 'scoredAtUtc', label: 'When', type: 'datetime' }
  ];
  bidColumns: Column[] = [
    { key: 'supplierName', label: 'Supplier' }, { key: 'complianceMet', label: 'Compliant', type: 'bool' }, { key: 'technicalScore', label: 'Technical', type: 'number' },
    { key: 'bidAmount', label: 'Amount', type: 'money' }, { key: 'pricePoints', label: 'Price pts', type: 'number' },
    { key: 'preferencePoints', label: 'Preference pts', type: 'number' }, { key: 'totalPoints', label: 'Total', type: 'number' }, { key: 'rank', label: 'Rank', type: 'number' }
  ];

  get canManage(): boolean { return this.auth.has(P.procurementManage); }
  get canEvaluate(): boolean { return this.auth.has(P.procurementEvaluate); }

  ngOnChanges(): void {
    if (!this.detail.canAccessBids) return;
    if (this.canEvaluate) this.api.get<any[]>(`procurement/${this.detail.summary.id}/my-scores`).subscribe(r => this.myScores.set(r));
    this.api.get(`procurement/${this.detail.summary.id}/evaluation-report`).subscribe({ next: r => this.report.set(r), error: () => this.report.set(null) });
  }

  async addCriterion(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Add criterion', fields: criterionFields() }, '760px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/criteria`, v).subscribe(() => this.changed.emit());
  }

  async editCriterion(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit criterion', fields: criterionFields(), value: row }, '760px');
    if (v) this.api.put(`procurement/${this.detail.summary.id}/criteria/${row.id}`, v).subscribe(() => this.changed.emit());
  }

  deleteCriterion(row: any): void {
    this.api.delete(`procurement/${this.detail.summary.id}/criteria/${row.id}`).subscribe(() => this.changed.emit());
  }

  async setup(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Evaluation setup', fields: [
      { key: 'technicalThreshold', label: 'Technical threshold (0-100)', type: 'number', min: 0, max: 100 },
      { key: 'priceSystemCode', label: 'Price preference system', type: 'select',
        options: [{ value: '80/20', label: '80/20 (up to R50m)' }, { value: '90/10', label: '90/10 (above R50m)' }] }
    ], value: { technicalThreshold: this.detail.technicalThreshold, priceSystemCode: this.detail.priceSystemCode } }, '560px');
    if (v) this.api.put(`procurement/${this.detail.summary.id}/evaluation-setup`, v).subscribe(() => this.changed.emit());
  }

  async score(): Promise<void> {
    const bids = await new Promise<any[]>(r => this.api.get<any[]>(`procurement/${this.detail.summary.id}/bids`).subscribe(r));
    const v = await openForm(this.dialog, { title: 'Submit score', fields: [
      { key: 'bidId', label: 'Bid', type: 'select', required: true, options: bids.map(b => ({ value: b.id, label: `${b.supplierName} (${b.bidReference})` })), wide: true },
      { key: 'criterionId', label: 'Criterion', type: 'select', required: true, options: (this.detail.criteria ?? []).map((c: any) => ({ value: c.id, label: c.name })), wide: true },
      { key: 'score', label: 'Score', type: 'number', min: 0 }, { key: 'passed', label: 'Passed (for pass/fail criteria)', type: 'checkbox' },
      { key: 'comment', label: 'Comment', type: 'textarea', required: true }, { key: 'evidenceReference', label: 'Evidence reference' }
    ] }, '760px');
    if (v) this.api.post(`procurement/${this.detail.summary.id}/scores`, v).subscribe(() => this.ngOnChanges());
  }

  consolidate(): void {
    this.api.post(`procurement/${this.detail.summary.id}/consolidate`).subscribe(r => {
      this.report.set(r);
      this.snack.open('Evaluation consolidated.', 'OK', { duration: 3000 });
    });
  }
}
