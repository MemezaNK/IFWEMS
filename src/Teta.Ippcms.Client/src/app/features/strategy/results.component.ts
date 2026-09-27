import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { DocumentsPanelComponent } from '../../shared/documents-panel.component';
import { openForm } from '../../shared/form-dialog.component';

/** Project performance results against APP indicators with evidence and independent verification (FR-STR-005/006, BR-009). */
@Component({
  selector: 'teta-results',
  standalone: true,
  imports: [DataTableComponent, DocumentsPanelComponent, MatButtonModule, MatIconModule],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Performance results</h1>
        @if (canCapture) { <button mat-flat-button color="primary" (click)="capture()"><mat-icon>add</mat-icon> Capture result</button> }
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" [actions]="actions" (rowClick)="selected.set($event)" />
      <ng-template #actions let-row>
        @if (row.status === 'Captured' && canCapture) { <button mat-button (click)="submit(row)">Submit</button> }
        @if (row.status === 'Submitted' && canVerify) {
          <button mat-button color="primary" (click)="verify(row, true)">Verify</button>
          <button mat-button color="warn" (click)="verify(row, false)">Reject</button>
        }
      </ng-template>
      @if (selected(); as s) {
        <div class="card" style="margin-top: 16px">
          <h2>{{ s.indicatorCode }} · {{ s.projectReference }} · {{ s.financialYear }} Q{{ s.quarter }}</h2>
          @if (s.missingEvidenceTypes?.length) { <div class="warn-banner">Missing verified evidence: {{ s.missingEvidenceTypes.join(', ') }}</div> }
          <teta-documents parentType="PerformanceResult" [parentId]="s.id" [evidenceMode]="true" />
        </div>
      }
    </div>
  `
})
export class ResultsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly rows = signal<any[]>([]);
  readonly selected = signal<any | null>(null);

  columns: Column[] = [
    { key: 'indicatorCode', label: 'Indicator' }, { key: 'projectReference', label: 'Project' }, { key: 'financialYear', label: 'FY' },
    { key: 'quarter', label: 'Q', type: 'number' }, { key: 'value', label: 'Value', type: 'number' }, { key: 'contribution', label: 'Contribution', type: 'number' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'evidenceCount', label: 'Evidence', type: 'number' },
    { key: 'verifiedEvidenceCount', label: 'Verified evidence', type: 'number' }, { key: 'verifiedBy', label: 'Verified by' }
  ];

  get canCapture(): boolean { return this.auth.has(P.performanceCapture); }
  get canVerify(): boolean { return this.auth.has(P.performanceVerify); }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.get<any[]>('strategy/results').subscribe(r => this.rows.set(r));
  }

  async capture(): Promise<void> {
    const plans = await new Promise<any[]>(resolve => this.api.get<any[]>('strategy/plans').subscribe(resolve));
    const trees = await Promise.all(plans.map(p => new Promise<any>(resolve => this.api.get(`strategy/plans/${p.id}`).subscribe(resolve))));
    const options = trees.flatMap(t => t.objectives.flatMap((o: any) => o.indicators.map((i: any) => ({ value: i.id, label: `${t.plan.code} ${i.code} – ${i.name}` }))));
    const v = await openForm(this.dialog, { title: 'Capture performance result', fields: [
      { key: 'indicatorId', label: 'APP indicator', type: 'select', required: true, options, wide: true },
      { key: 'projectId', label: 'Project', type: 'select', required: true, options: this.refs.projects(), wide: true },
      { key: 'financialYear', label: 'Financial year', required: true },
      { key: 'quarter', label: 'Quarter', type: 'select', required: true, options: [1, 2, 3, 4].map(q => ({ value: q, label: 'Q' + q })) },
      { key: 'value', label: 'Result value', type: 'number', required: true, min: 0 },
      { key: 'narrative', label: 'Narrative', type: 'textarea' }], value: { financialYear: '2026/27' } });
    if (v) this.api.post('strategy/results', v).subscribe(() => this.load());
  }

  submit(row: any): void {
    this.api.post(`strategy/results/${row.id}/submit`).subscribe(() => this.load());
  }

  async verify(row: any, approve: boolean): Promise<void> {
    const v = await openForm(this.dialog, { title: approve ? 'Verify result' : 'Reject result', fields: [
      { key: 'comment', label: 'Comment', type: 'textarea', required: !approve }] }, '520px');
    if (!v) return;
    this.api.post(`strategy/results/${row.id}/verify`, { approve, comment: v['comment'] }).subscribe(() => {
      this.snack.open(approve ? 'Result verified.' : 'Result rejected.', 'OK', { duration: 3000 });
      this.load();
    });
  }
}
