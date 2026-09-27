import { DecimalPipe } from '@angular/common';
import { Component, Input, OnChanges, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { confirmAction, openForm } from '../../shared/form-dialog.component';

const WBS_TYPES = ['Workstream', 'Deliverable', 'Milestone', 'Activity', 'Task'];

/** WBS, dependencies, baselines, progress and a Gantt view with critical milestones (FR-EXE-001..005). */
@Component({
  selector: 'teta-project-schedule',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent, DecimalPipe],
  template: `
    <div style="padding-top: 12px" class="grid">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Work breakdown structure</h2>
          @if (canManage) {
            <button mat-stroked-button (click)="edit()"><mat-icon>add</mat-icon> Element</button>
            <button mat-stroked-button (click)="addDependency()"><mat-icon>link</mat-icon> Dependency</button>
          }
          @if (canBaseline) { <button mat-flat-button color="primary" (click)="baseline()">Approve baseline</button> }
        </div>
        <teta-data-table [columns]="columns" [rows]="wbs()" [actions]="canManage ? actions : undefined" [pageSize]="50" exportName="wbs" />
        <ng-template #actions let-row>
          <button mat-button (click)="progress(row)">Progress</button>
          <button mat-icon-button title="Edit" (click)="edit(row)"><mat-icon>edit</mat-icon></button>
        </ng-template>
      </div>
      @if (gantt(); as g) {
        <div class="card">
          <h2>Gantt ({{ g.start }} – {{ g.end }}) · {{ g.baselines.length }} baseline(s)</h2>
          @for (item of g.items; track item.id) {
            <div class="gantt-row">
              <div [style.padding-left.px]="item.depth * 14" class="small">{{ item.code }} {{ item.name }}</div>
              <div class="gantt-track">
                @if (bar(item); as b) {
                  <div class="gantt-bar" [class.critical]="item.isCritical" [class.milestone]="item.type === 'Milestone'" [style.left.%]="b.left" [style.width.%]="b.width"
                       [title]="item.plannedStart + ' → ' + item.plannedEnd + ' (' + (item.rolledUpPercent | number: '1.0-0') + '%)'"></div>
                  @if (item.type !== 'Milestone') { <div class="gantt-progress" [style.left.%]="b.left" [style.width.%]="b.width * item.rolledUpPercent / 100"></div> }
                }
              </div>
            </div>
          }
          <h2 style="margin-top: 12px">Dependencies</h2>
          <teta-data-table [columns]="depColumns" [rows]="dependencies()" [actions]="canManage ? depActions : undefined" [filterable]="false" [paginate]="false" [exportable]="false" />
          <ng-template #depActions let-row><button mat-icon-button (click)="removeDependency(row)" title="Remove"><mat-icon>delete</mat-icon></button></ng-template>
        </div>
      }
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Resources</h2>
          @if (canManage) { <button mat-stroked-button (click)="addResource()"><mat-icon>person_add</mat-icon> Assign resource</button> }</div>
        <teta-data-table [columns]="resourceColumns" [rows]="resources()" [paginate]="false" />
      </div>
    </div>
  `
})
export class ProjectScheduleComponent implements OnChanges {
  @Input({ required: true }) project!: any;
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  readonly wbs = signal<any[]>([]);
  readonly gantt = signal<any | null>(null);
  readonly resources = signal<any[]>([]);
  readonly dependencies = computed(() => {
    const g = this.gantt();
    if (!g) return [];
    const name = (id: string) => g.items.find((i: any) => i.id === id)?.code ?? id;
    return g.dependencies.map((d: any) => ({ ...d, from: name(d.predecessorId), to: name(d.successorId) }));
  });

  columns: Column[] = [
    { key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'type', label: 'Type' }, { key: 'ownerName', label: 'Owner' },
    { key: 'plannedStart', label: 'Start', type: 'date' }, { key: 'plannedEnd', label: 'End', type: 'date' },
    { key: 'baselineEnd', label: 'Baseline end', type: 'date' }, { key: 'forecastEnd', label: 'Forecast end', type: 'date' },
    { key: 'rolledUpPercent', label: '% complete', type: 'percent' }, { key: 'scheduleVarianceDays', label: 'Variance (days)', type: 'number' },
    { key: 'isCritical', label: 'Critical', type: 'bool' },
    { key: 'status', label: 'Status', type: 'status', value: r => (r.isOverdue ? 'Overdue' : r.status) }
  ];
  depColumns: Column[] = [{ key: 'from', label: 'Predecessor' }, { key: 'to', label: 'Successor' }, { key: 'type', label: 'Type' }, { key: 'lagDays', label: 'Lag', type: 'number' }];
  resourceColumns: Column[] = [{ key: 'resourceName', label: 'Resource' }, { key: 'role', label: 'Role' }, { key: 'wbsName', label: 'Work item' },
    { key: 'allocationPercent', label: 'Allocation', type: 'percent' }, { key: 'isExternal', label: 'External', type: 'bool' }];

  get canManage(): boolean { return this.auth.has(P.executionManage); }
  get canBaseline(): boolean { return this.auth.hasAny(P.projectManage, P.projectGate); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    const id = this.project.id;
    this.api.get<any[]>(`projects/${id}/wbs`).subscribe(w => this.wbs.set(w));
    this.api.get(`projects/${id}/gantt`).subscribe(g => this.gantt.set(g));
    this.api.get<any[]>(`projects/${id}/resources`).subscribe(r => this.resources.set(r));
  }

  bar(item: any): { left: number; width: number } | null {
    const g = this.gantt();
    if (!g?.start || !g?.end || !item.plannedStart || !item.plannedEnd) return null;
    const start = Date.parse(g.start), end = Date.parse(g.end), span = Math.max(1, end - start);
    const s = Date.parse(item.plannedStart), e = Date.parse(item.plannedEnd);
    return { left: ((s - start) / span) * 100, width: Math.max(0.8, ((e - s) / span) * 100) };
  }

  async edit(row?: any): Promise<void> {
    const parents = this.wbs().filter(w => !row || w.id !== row.id).map(w => ({ value: w.id, label: `${w.code} ${w.name}` }));
    const v = await openForm(this.dialog, { title: row ? 'Edit WBS element' : 'New WBS element', fields: [
      { key: 'parentId', label: 'Parent', type: 'select', options: parents },
      { key: 'type', label: 'Type', type: 'select', required: true, options: WBS_TYPES.map(t => ({ value: t, label: t })) },
      { key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Name', required: true },
      { key: 'ownerUserId', label: 'Owner', type: 'select', options: this.refs.users() }, { key: 'ownerName', label: 'Owner name' },
      { key: 'plannedStart', label: 'Planned start', type: 'date' }, { key: 'plannedEnd', label: 'Planned end', type: 'date' },
      { key: 'weight', label: 'Weight', type: 'number', min: 0 }, { key: 'sortOrder', label: 'Sort order', type: 'number' },
      { key: 'isCritical', label: 'Critical milestone', type: 'checkbox' }, { key: 'evidenceRequired', label: 'Evidence required for completion', type: 'checkbox' },
      { key: 'acceptanceCriteria', label: 'Acceptance criteria', type: 'textarea' }],
      value: row ?? { type: 'Activity', weight: 1, sortOrder: this.wbs().length + 1 } });
    if (!v) return;
    const call = row ? this.api.put(`projects/${this.project.id}/wbs/${row.id}`, v) : this.api.post(`projects/${this.project.id}/wbs`, v);
    call.subscribe(() => this.load());
  }

  async progress(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: `Progress – ${row.code} ${row.name}`, fields: [
      { key: 'percentComplete', label: '% complete', type: 'number', required: true, min: 0, max: 100 },
      { key: 'forecastEnd', label: 'Forecast end', type: 'date' }, { key: 'actualStart', label: 'Actual start', type: 'date' },
      { key: 'actualEnd', label: 'Actual end', type: 'date' }, { key: 'comment', label: 'Comment', type: 'textarea' }],
      value: { percentComplete: row.percentComplete, forecastEnd: row.forecastEnd, actualStart: row.actualStart, actualEnd: row.actualEnd } }, '560px');
    if (v) this.api.post(`projects/${this.project.id}/wbs/${row.id}/progress`, v).subscribe(() => this.load());
  }

  async addDependency(): Promise<void> {
    const opts = this.wbs().map(w => ({ value: w.id, label: `${w.code} ${w.name}` }));
    const v = await openForm(this.dialog, { title: 'Add dependency', fields: [
      { key: 'predecessorId', label: 'Predecessor', type: 'select', required: true, options: opts },
      { key: 'successorId', label: 'Successor', type: 'select', required: true, options: opts },
      { key: 'type', label: 'Type', type: 'select', required: true, options: ['FS', 'SS', 'FF', 'SF'].map(t => ({ value: t, label: t })) },
      { key: 'lagDays', label: 'Lag (days)', type: 'number' }], value: { type: 'FS', lagDays: 0 } }, '560px');
    if (v) this.api.post(`projects/${this.project.id}/dependencies`, v).subscribe(() => this.load());
  }

  removeDependency(row: any): void {
    this.api.delete(`projects/${this.project.id}/dependencies/${row.id}`).subscribe(() => this.load());
  }

  async baseline(): Promise<void> {
    const reason = await confirmAction(this.dialog, 'Approve schedule baseline', 'Current planned dates become the approved baseline.', 'Reason');
    if (reason !== undefined) this.api.post(`projects/${this.project.id}/baseline`, { reason }).subscribe(() => this.load());
  }

  async addResource(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Assign resource', fields: [
      { key: 'wbsElementId', label: 'Work item', type: 'select', required: true, options: this.wbs().map(w => ({ value: w.id, label: `${w.code} ${w.name}` })) },
      { key: 'userId', label: 'Internal user', type: 'select', options: this.refs.users() }, { key: 'resourceName', label: 'Resource name', required: true },
      { key: 'isExternal', label: 'External resource', type: 'checkbox' }, { key: 'role', label: 'Role', required: true },
      { key: 'allocationPercent', label: 'Allocation %', type: 'number', required: true, min: 0, max: 100 }], value: { allocationPercent: 50 } });
    if (v) this.api.post(`projects/${this.project.id}/resources`, v).subscribe(() => this.load());
  }
}
