import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { WorkflowPanelComponent } from '../../shared/workflow-panel.component';

/** Close-out checklist, lessons learned, handover, closure approval and post-implementation benefit reviews (FR-EXE-012/013, BR-010). */
@Component({
  selector: 'teta-project-closure',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent, StatusChipComponent, WorkflowPanelComponent],
  template: `
    <div style="padding-top: 12px" class="grid cols-2">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Project closure</h2>
          @if (closure(); as c) { <teta-status [value]="c.status" /> }
          @if (canManage) { <button mat-stroked-button (click)="edit()"><mat-icon>edit</mat-icon> Edit</button>
            <button mat-flat-button color="primary" (click)="submit()">Submit closure</button> }</div>
        @if (closure(); as c) {
          @if (c.openItems?.length) { <div class="warn-banner">Open items blocking closure: <ul>@for (i of c.openItems; track i) { <li>{{ i }}</li> }</ul></div> }
          <dl class="dl small">
            <dt>Lessons learned</dt><dd>{{ c.lessonsLearned }}</dd><dt>Handover</dt><dd>{{ c.handoverNotes }}</dd>
            <dt>Financial reconciliation</dt><dd>{{ c.financialReconciliationConfirmed ? 'Confirmed' : 'Not confirmed' }}</dd>
            <dt>Documentation complete</dt><dd>{{ c.documentationComplete ? 'Yes' : 'No' }}</dd>
            <dt>Approved exception</dt><dd>{{ c.approvedExceptionReference }}</dd>
          </dl>
          <teta-workflow entityType="ProjectClosure" [entityId]="c.id" />
        }
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Benefit reviews</h2>
          @if (canManage) { <button mat-stroked-button (click)="editReview()"><mat-icon>add</mat-icon> Schedule review</button> }</div>
        <teta-data-table [columns]="columns" [rows]="reviews()" [actions]="canManage ? actions : undefined" [paginate]="false" />
        <ng-template #actions let-row><button mat-icon-button (click)="editReview(row)" title="Record results"><mat-icon>edit</mat-icon></button></ng-template>
      </div>
    </div>
  `
})
export class ProjectClosureComponent implements OnChanges {
  @Input({ required: true }) project!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  readonly closure = signal<any | null>(null);
  readonly reviews = signal<any[]>([]);
  columns: Column[] = [{ key: 'scheduledDate', label: 'Scheduled', type: 'date' }, { key: 'expectedBenefit', label: 'Expected benefit' },
    { key: 'expectedValue', label: 'Expected', type: 'number' }, { key: 'actualValue', label: 'Actual', type: 'number' },
    { key: 'realisationPercent', label: 'Realised', type: 'percent' }, { key: 'status', label: 'Status', type: 'status' }];

  get canManage(): boolean { return this.auth.hasAny(P.projectManage, P.portfolioManage); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.get(`projects/${this.project.id}/closure`).subscribe(c => this.closure.set(c));
    this.api.get<any[]>(`projects/${this.project.id}/benefit-reviews`).subscribe(r => this.reviews.set(r));
  }

  async edit(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Project closure', fields: [
      { key: 'lessonsLearned', label: 'Lessons learned', type: 'textarea', required: true }, { key: 'handoverNotes', label: 'Handover notes', type: 'textarea', required: true },
      { key: 'financialReconciliationConfirmed', label: 'Financial reconciliation confirmed', type: 'checkbox' },
      { key: 'documentationComplete', label: 'Documentation complete', type: 'checkbox' },
      { key: 'approvedExceptionReference', label: 'Approved exception reference (if closing with open items)' }], value: this.closure() ?? {} });
    if (v) this.api.put(`projects/${this.project.id}/closure`, v).subscribe(() => this.load());
  }

  submit(): void {
    this.api.post(`projects/${this.project.id}/closure/submit`).subscribe(() => { this.load(); this.changed.emit(); });
  }

  async editReview(row?: any): Promise<void> {
    const v = await openForm(this.dialog, { title: row ? 'Benefit review results' : 'Schedule benefit review', fields: [
      { key: 'scheduledDate', label: 'Scheduled date', type: 'date', required: true }, { key: 'expectedBenefit', label: 'Expected benefit', required: true },
      { key: 'expectedValue', label: 'Expected value', type: 'number' }, { key: 'actualBenefit', label: 'Actual benefit' },
      { key: 'actualValue', label: 'Actual value', type: 'number' }, { key: 'findings', label: 'Findings', type: 'textarea' },
      { key: 'status', label: 'Status', type: 'select', options: ['Scheduled', 'Completed', 'Cancelled'].map(s => ({ value: s, label: s })) }],
      value: row ?? { status: 'Scheduled' } });
    if (!v) return;
    const path = `projects/${this.project.id}/benefit-reviews`;
    (row ? this.api.put(`${path}/${row.id}`, v) : this.api.post(path, v)).subscribe(() => this.load());
  }
}
