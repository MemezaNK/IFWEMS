import { Component, OnInit, inject, signal } from '@angular/core';
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
import { stepFields } from './admin-forms';

/** Workflow definitions with their approval steps; new/changed definitions require a second administrator to activate (FR-ADM-001/010). */
@Component({
  selector: 'teta-admin-workflows',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent, StatusChipComponent],
  template: `
    <div style="padding-top: 12px" class="grid">
      <div class="card">
        <div class="toolbar-row"><h2 style="margin: 0; flex: 1">Workflow definitions</h2>
          @if (canWorkflow) { <button mat-stroked-button (click)="add()"><mat-icon>add</mat-icon> New workflow</button> }</div>
        <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="select($event)" emptyText="No workflow definitions." [paginate]="false" />
      </div>
      @if (selected(); as s) {
        <div class="card">
          <div class="toolbar-row">
            <h2 style="margin: 0; flex: 1">{{ s.code }} – {{ s.name }} <span class="small muted">v{{ s.definitionVersion }}</span></h2>
            <teta-status [value]="s.status" />
          </div>
          <p class="small muted">{{ s.entityType }} @if (s.description) { – {{ s.description }} }</p>
          <teta-data-table [columns]="stepColumns" [rows]="localSteps()" [actions]="canWorkflow ? stepActions : undefined" emptyText="No steps yet." [paginate]="false" />
          <ng-template #stepActions let-row><button mat-icon-button (click)="removeStep(row)" title="Remove"><mat-icon>delete</mat-icon></button></ng-template>
          @if (canWorkflow) {
            <div class="toolbar-row" style="margin-top: 8px">
              <button mat-stroked-button (click)="addStep()"><mat-icon>add</mat-icon> Add step</button>
              <button mat-flat-button color="primary" (click)="saveSteps()">Save steps</button>
              <span class="spacer"></span>
              @if (s.status === 'Draft' || s.status === 'Rejected') { <button mat-button (click)="submit()">Submit for activation</button> }
            </div>
          }
          @if (s.status === 'Submitted' && canApprove) {
            <div class="toolbar-row" style="margin-top: 8px">
              <span class="muted small">This definition needs a second administrator to activate it.</span>
              <span class="spacer"></span>
              <button mat-button color="warn" (click)="decide(false)">Reject</button>
              <button mat-flat-button color="primary" (click)="decide(true)">Activate</button>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class AdminWorkflowsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly rows = signal<any[]>([]);
  readonly selected = signal<any | null>(null);
  readonly localSteps = signal<any[]>([]);

  columns: Column[] = [
    { key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'entityType', label: 'Entity type' },
    { key: 'definitionVersion', label: 'Version', type: 'number' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'activatedAtUtc', label: 'Activated', type: 'datetime' }, { key: 'activatedBy', label: 'Activated by' }
  ];
  stepColumns: Column[] = [
    { key: 'stepOrder', label: '#', type: 'number' }, { key: 'code', label: 'Code' }, { key: 'name', label: 'Name' },
    { key: 'requiredRole', label: 'Required role' }, { key: 'authorityType', label: 'Authority type' },
    { key: 'minimumValue', label: 'From (R)', type: 'money' }, { key: 'maximumValue', label: 'Up to (R)', type: 'money' },
    { key: 'slaHours', label: 'SLA (h)', type: 'number' }, { key: 'escalationRole', label: 'Escalation role' }
  ];

  get canWorkflow(): boolean { return this.auth.has(P.adminWorkflow); }
  get canApprove(): boolean { return this.auth.has(P.adminConfigApprove); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('admin/workflows').subscribe(r => {
      this.rows.set(r);
      const sel = this.selected();
      if (sel) {
        const fresh = r.find(w => w.id === sel.id);
        this.select(fresh ?? null);
      }
    });
  }

  select(row: any | null): void {
    this.selected.set(row);
    this.localSteps.set(row ? row.steps.map((s: any) => ({ ...s })) : []);
  }

  async add(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New workflow definition', fields: [
      { key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Name', required: true, wide: true },
      { key: 'entityType', label: 'Entity type', required: true, hint: 'e.g. Procurement, Contract, Invoice, ChangeRequest' },
      { key: 'description', label: 'Description', type: 'textarea' }
    ] }, '620px');
    if (!v) return;
    this.api.post<any>('admin/workflows', { ...v, steps: [] }).subscribe(w => { this.load(); this.select(w); });
  }

  async addStep(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Add step', fields: stepFields(), value: { slaHours: 24 } }, '640px');
    if (!v) return;
    this.localSteps.update(steps => [...steps, { ...v, stepOrder: steps.length + 1 }]);
  }

  removeStep(row: any): void {
    this.localSteps.update(steps => steps.filter(s => s.stepOrder !== row.stepOrder).map((s, i) => ({ ...s, stepOrder: i + 1 })));
  }

  saveSteps(): void {
    const s = this.selected();
    if (!s) return;
    const body = { code: s.code, name: s.name, entityType: s.entityType, description: s.description, steps: this.localSteps() };
    this.api.put<any>(`admin/workflows/${s.id}`, body).subscribe(w => {
      this.snack.open('Workflow steps saved.', 'OK', { duration: 3000 });
      this.load();
      this.select(w);
    });
  }

  submit(): void {
    const s = this.selected();
    if (!s) return;
    this.api.post<any>(`admin/workflows/${s.id}/submit`).subscribe(w => { this.snack.open('Submitted for activation.', 'OK', { duration: 3000 }); this.load(); this.select(w); });
  }

  async decide(approve: boolean): Promise<void> {
    const s = this.selected();
    if (!s) return;
    const comment = await confirmAction(this.dialog, approve ? 'Activate workflow' : 'Reject workflow',
      approve ? `Activate ${s.code} v${s.definitionVersion}? This makes it the active definition for ${s.entityType}.` : `Reject ${s.code} v${s.definitionVersion}?`,
      approve ? undefined : 'Reason');
    if (comment === undefined) return;
    this.api.post<any>(`admin/workflows/${s.id}/activate`, { approve, comment: comment || undefined }).subscribe(w => {
      this.snack.open(approve ? 'Workflow activated.' : 'Workflow rejected.', 'OK', { duration: 3000 });
      this.load();
      this.select(w);
    });
  }
}
