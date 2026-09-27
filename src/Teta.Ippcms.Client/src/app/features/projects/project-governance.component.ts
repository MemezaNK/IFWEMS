import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { Field, openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { WorkflowPanelComponent } from '../../shared/workflow-panel.component';
import { businessCaseFields } from './project-forms';

/** Business case, APP alignment, prioritisation, charter, stakeholders, workstreams and stage gates (FR-POR-002..008). */
@Component({
  selector: 'teta-project-governance',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent, StatusChipComponent, WorkflowPanelComponent],
  template: `
    <div style="padding-top: 12px" class="grid cols-2">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Business case</h2>
          @if (bc(); as b) { <teta-status [value]="b.status" /> }
          @if (canEdit) { <button mat-stroked-button (click)="editBusinessCase()"><mat-icon>edit</mat-icon> Edit</button>
            <button mat-flat-button color="primary" (click)="submitBusinessCase()">Submit for approval</button> }
        </div>
        @if (bc(); as b) {
          @if (b.missingFields?.length) { <div class="warn-banner">Missing before submission: {{ b.missingFields.join(', ') }}</div> }
          <dl class="dl small">
            <dt>Problem</dt><dd>{{ b.problem }}</dd><dt>Objectives</dt><dd>{{ b.objectives }}</dd><dt>Options</dt><dd>{{ b.options }}</dd>
            <dt>Scope</dt><dd>{{ b.scope }}</dd><dt>Benefits</dt><dd>{{ b.benefits }}</dd><dt>Estimated cost</dt><dd>R {{ b.estimatedCost }}</dd>
            <dt>Risks</dt><dd>{{ b.risks }}</dd><dt>Delivery model</dt><dd>{{ b.deliveryModel }}</dd>
            <dt>Decision</dt><dd>{{ b.decisionComment }}</dd>
          </dl>
        }
        <h2 style="margin-top: 16px">Approvals</h2>
        @if (bc(); as b) { <teta-workflow entityType="BusinessCase" [entityId]="b.id" /> }
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">APP alignment</h2>
          @if (canEdit) { <button mat-stroked-button (click)="link()"><mat-icon>add_link</mat-icon> Link indicator</button> }</div>
        <teta-data-table [columns]="linkColumns" [rows]="links()" [actions]="canEdit ? unlinkTpl : undefined" [filterable]="false" [paginate]="false" [exportable]="false"
                         emptyText="Projects must be linked to at least one APP indicator before approval (FR-STR-004)." />
        <ng-template #unlinkTpl let-row><button mat-icon-button (click)="unlink(row)" title="Remove"><mat-icon>link_off</mat-icon></button></ng-template>

        <div class="toolbar-row" style="margin-top: 16px"><h2 style="margin: 0; flex: 1">Prioritisation ({{ priority()?.totalScore ?? '—' }})</h2>
          @if (canScore) { <button mat-stroked-button (click)="score()">Score</button> }</div>
        <teta-data-table [columns]="scoreColumns" [rows]="priority()?.scores ?? []" [filterable]="false" [paginate]="false" [exportable]="false" />
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Project charter</h2>
          @if (charter(); as c) { <teta-status [value]="c.status" /> }
          @if (canEdit) { <button mat-stroked-button (click)="editCharter()"><mat-icon>edit</mat-icon> Edit</button> }
          @if (canApprove && charter()?.status === 'Draft') { <button mat-flat-button color="primary" (click)="approveCharter()">Approve</button> }
        </div>
        @if (charter(); as c) {
          <dl class="dl small"><dt>Purpose</dt><dd>{{ c.purpose }}</dd><dt>Scope</dt><dd>{{ c.scope }}</dd>
            <dt>Governance</dt><dd>{{ c.governanceStructure }}</dd><dt>Milestones</dt><dd>{{ c.keyMilestones }}</dd>
            <dt>Assumptions</dt><dd>{{ c.assumptions }}</dd><dt>Approved</dt><dd>{{ c.approvedBy }}</dd></dl>
        } @else { <p class="muted">No charter yet.</p> }
      </div>
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Stakeholders & workstreams</h2>
          @if (canEdit) { <button mat-stroked-button (click)="addStakeholder()"><mat-icon>person_add</mat-icon> Stakeholder</button>
            <button mat-stroked-button (click)="addWorkstream()"><mat-icon>add</mat-icon> Workstream</button> }</div>
        <teta-data-table [columns]="stakeholderColumns" [rows]="stakeholders()" [filterable]="false" [paginate]="false" [exportable]="false" />
        <teta-data-table [columns]="workstreamColumns" [rows]="workstreams()" [filterable]="false" [paginate]="false" [exportable]="false" />
      </div>
      <div class="card" style="grid-column: 1 / -1">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Stage gates ({{ project.stage }})</h2>
          @if (canEdit || canGate) { <button mat-flat-button color="primary" (click)="evaluateGate()">Evaluate next gate</button> }</div>
        @for (g of gates(); track g.id) {
          <div class="card" style="margin-bottom: 8px">
            <div class="toolbar-row"><strong>{{ g.fromStage }} → {{ g.toStage }}</strong> <teta-status [value]="g.decision" />
              <span class="muted small">{{ g.decidedBy }} {{ g.comment }}</span><span class="spacer"></span>
              @if (canGate && g.decision === 'Pending') {
                <button mat-flat-button color="primary" [disabled]="!g.canPass" (click)="decideGate(g, true)">Pass gate</button>
                <button mat-button color="warn" (click)="decideGate(g, false)">Fail</button>
              }
            </div>
            @for (c of g.checks; track c.criterionId) {
              <div class="small">{{ c.isMet ? '✔' : '✖' }} {{ c.code }} {{ c.description }} @if (c.isMandatory) { <span class="muted">(mandatory)</span> }
                <span class="muted">{{ c.evidence }}</span></div>
            }
          </div>
        } @empty { <p class="muted">No gate reviews yet.</p> }
      </div>
    </div>
  `
})
export class ProjectGovernanceComponent implements OnChanges {
  @Input({ required: true }) project!: any;
  @Output() changed = new EventEmitter<void>();
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly bc = signal<any | null>(null);
  readonly links = signal<any[]>([]);
  readonly priority = signal<any | null>(null);
  readonly charter = signal<any | null>(null);
  readonly stakeholders = signal<any[]>([]);
  readonly workstreams = signal<any[]>([]);
  readonly gates = signal<any[]>([]);

  linkColumns: Column[] = [{ key: 'indicatorCode', label: 'Indicator' }, { key: 'indicatorName', label: 'Name' }, { key: 'method', label: 'Method' },
    { key: 'weight', label: 'Weight', type: 'number' }, { key: 'plannedContribution', label: 'Planned', type: 'number' }];
  scoreColumns: Column[] = [{ key: 'criterionName', label: 'Criterion' }, { key: 'weight', label: 'Weight', type: 'number' },
    { key: 'score', label: 'Score', type: 'number' }, { key: 'maxScore', label: 'Max', type: 'number' }, { key: 'rationale', label: 'Rationale' }];
  stakeholderColumns: Column[] = [{ key: 'name', label: 'Stakeholder' }, { key: 'role', label: 'Role' }, { key: 'organisation', label: 'Organisation' },
    { key: 'effectiveFrom', label: 'From', type: 'date' }, { key: 'isCurrent', label: 'Current', type: 'bool' }];
  workstreamColumns: Column[] = [{ key: 'code', label: 'Workstream' }, { key: 'name', label: 'Name' }, { key: 'lead', label: 'Lead' }];

  get canEdit(): boolean { return this.auth.hasAny(P.projectManage, P.projectCreate); }
  get canApprove(): boolean { return this.auth.has(P.projectApprove); }
  get canGate(): boolean { return this.auth.has(P.projectGate); }
  get canScore(): boolean { return this.auth.has(P.portfolioManage); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    const id = this.project.id;
    this.api.get(`projects/${id}/business-case`).subscribe(b => this.bc.set(b));
    this.api.get<any[]>(`strategy/projects/${id}/links`).subscribe(l => this.links.set(l));
    this.api.get(`projects/${id}/prioritisation`).subscribe(p => this.priority.set(p));
    this.api.get(`projects/${id}/charter`).subscribe(c => this.charter.set(c));
    this.api.get<any[]>(`projects/${id}/stakeholders`).subscribe(s => this.stakeholders.set(s));
    this.api.get<any[]>(`projects/${id}/workstreams`).subscribe(w => this.workstreams.set(w));
    this.api.get<any[]>(`projects/${id}/gates`).subscribe(g => this.gates.set(g));
  }

  async editBusinessCase(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Business case', fields: businessCaseFields(), value: this.bc() ?? {} }, '860px');
    if (v) this.api.put(`projects/${this.project.id}/business-case`, v).subscribe(() => this.load());
  }

  submitBusinessCase(): void {
    this.api.post(`projects/${this.project.id}/business-case/submit`).subscribe(() => {
      this.snack.open('Business case submitted for approval.', 'OK', { duration: 3000 });
      this.changed.emit();
      this.load();
    });
  }

  async link(): Promise<void> {
    const plans = await new Promise<any[]>(r => this.api.get<any[]>('strategy/plans').subscribe(r));
    const trees = await Promise.all(plans.map(p => new Promise<any>(r => this.api.get(`strategy/plans/${p.id}`).subscribe(r))));
    const options = trees.flatMap(t => t.objectives.flatMap((o: any) => o.indicators.map((i: any) => ({ value: i.id, label: `${t.plan.code} ${i.code} – ${i.name}` }))));
    const v = await openForm(this.dialog, { title: 'Link APP indicator', fields: [
      { key: 'indicatorId', label: 'Indicator', type: 'select', required: true, options, wide: true },
      { key: 'method', label: 'Contribution method', type: 'select', required: true, options: ['Direct', 'Weighted', 'Count'].map(m => ({ value: m, label: m })) },
      { key: 'weight', label: 'Weight', type: 'number', required: true, min: 0 }, { key: 'plannedContribution', label: 'Planned contribution', type: 'number' }],
      value: { method: 'Direct', weight: 1 } });
    if (v) this.api.post(`strategy/projects/${this.project.id}/links`, v).subscribe(() => this.load());
  }

  unlink(row: any): void {
    this.api.delete(`strategy/projects/${this.project.id}/links/${row.indicatorId}`).subscribe(() => this.load());
  }

  async score(): Promise<void> {
    const criteria = await new Promise<any[]>(r => this.api.get<any[]>('prioritisation/criteria').subscribe(r));
    const fields: Field[] = criteria.filter(c => c.isActive).map(c => ({ key: c.id, label: `${c.name} (weight ${c.weight}, 0–${c.maxScore})`, type: 'number', required: true, min: 0, max: c.maxScore }));
    const current = Object.fromEntries((this.priority()?.scores ?? []).map((s: any) => [s.criterionId, s.score]));
    const v = await openForm<Record<string, number>>(this.dialog, { title: 'Prioritisation scores', fields, value: current });
    if (v) this.api.put(`projects/${this.project.id}/prioritisation`, Object.entries(v).map(([criterionId, score]) => ({ criterionId, score, rationale: null })))
      .subscribe(() => { this.load(); this.changed.emit(); });
  }

  async editCharter(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Project charter', fields: [
      { key: 'purpose', label: 'Purpose', type: 'textarea', required: true }, { key: 'scope', label: 'Scope', type: 'textarea', required: true },
      { key: 'governanceStructure', label: 'Governance structure', type: 'textarea', required: true },
      { key: 'keyMilestones', label: 'Key milestones', type: 'textarea' }, { key: 'assumptions', label: 'Assumptions', type: 'textarea' }],
      value: this.charter() ?? {} }, '800px');
    if (v) this.api.put(`projects/${this.project.id}/charter`, v).subscribe(() => this.load());
  }

  approveCharter(): void {
    this.api.post(`projects/${this.project.id}/charter/approve`).subscribe(() => this.load());
  }

  async addStakeholder(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Add stakeholder', fields: [
      { key: 'userId', label: 'Platform user (optional)', type: 'select', options: this.refs.users() }, { key: 'name', label: 'Name', required: true },
      { key: 'organisation', label: 'Organisation' },
      { key: 'role', label: 'Role', type: 'select', required: true, options: ['Sponsor', 'ProjectManager', 'TeamMember', 'BusinessOwner', 'SteeringCommittee', 'Stakeholder'].map(r => ({ value: r, label: r })) },
      { key: 'responsibility', label: 'Responsibility' }, { key: 'effectiveFrom', label: 'Effective from', type: 'date', required: true },
      { key: 'effectiveTo', label: 'Effective to', type: 'date' }], value: { effectiveFrom: new Date().toISOString().substring(0, 10) } });
    if (v) this.api.post(`projects/${this.project.id}/stakeholders`, v).subscribe(() => this.load());
  }

  async addWorkstream(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Add workstream', fields: [
      { key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Name', required: true }, { key: 'lead', label: 'Lead' }] }, '520px');
    if (v) this.api.post(`projects/${this.project.id}/workstreams`, v).subscribe(() => this.load());
  }

  evaluateGate(): void {
    this.api.post(`projects/${this.project.id}/gates/evaluate`).subscribe(() => this.load());
  }

  async decideGate(gate: any, pass: boolean): Promise<void> {
    const v = await openForm(this.dialog, { title: pass ? 'Pass stage gate' : 'Fail stage gate', fields: [
      { key: 'comment', label: 'Comment', type: 'textarea', required: !pass }] }, '520px');
    if (!v) return;
    this.api.post(`projects/${this.project.id}/gates/${gate.id}/decision`, { pass, comment: v['comment'], manualChecks: [] })
      .subscribe(() => { this.load(); this.changed.emit(); });
  }
}
