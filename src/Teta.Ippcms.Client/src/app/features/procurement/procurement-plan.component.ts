import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { methodRuleFields, planItemFields } from './procurement-forms';

/** Annual procurement / demand plan, method rules and commitment forecast (FR-BUD-003/004/009, FR-SCM regulation thresholds). */
@Component({
  selector: 'teta-procurement-plan',
  standalone: true,
  imports: [MatTabsModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, FormsModule, DataTableComponent, StatusChipComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>Procurement plan</h1></div>
      <mat-tab-group animationDuration="0">
        <mat-tab label="Demand plan">
          <div style="padding-top: 12px">
            <div class="toolbar-row">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 160px">
                <mat-label>Financial year</mat-label>
                <input matInput [(ngModel)]="financialYear" (ngModelChange)="loadPlan()" placeholder="2026/27" />
              </mat-form-field>
              <span class="spacer"></span>
              @if (canManage) {
                <button mat-stroked-button (click)="generate()"><mat-icon>auto_awesome</mat-icon> Generate from approved projects</button>
                <button mat-flat-button color="primary" (click)="addPlanItem()"><mat-icon>add</mat-icon> New plan item</button>
              }
            </div>
            <teta-data-table [columns]="planColumns" [rows]="planItems()" [actions]="canManage ? planActions : undefined"
                             emptyText="No demand plan items for this financial year." exportName="procurement-plan" />
            <ng-template #planActions let-row>
              <button mat-icon-button (click)="editPlanItem(row)" title="Edit"><mat-icon>edit</mat-icon></button>
            </ng-template>

            <h2 style="margin-top: 24px">Commitment forecast</h2>
            <teta-data-table [columns]="forecastColumns" [rows]="forecast()" [filterable]="false" [paginate]="false"
                             emptyText="No forecast data." exportName="commitment-forecast" />
          </div>
        </mat-tab>
        <mat-tab label="Method rules">
          <div style="padding-top: 12px">
            <div class="toolbar-row"><span class="spacer"></span>
              @if (canManage) { <button mat-flat-button color="primary" (click)="addRule()"><mat-icon>add</mat-icon> New rule</button> }
            </div>
            <teta-data-table [columns]="ruleColumns" [rows]="rules()" [actions]="ruleActions" emptyText="No procurement method rules configured."
                             exportName="method-rules" />
            <ng-template #ruleActions let-row>
              @if (canManage && row.status === 'Draft') {
                <button mat-icon-button (click)="editRule(row)" title="Edit"><mat-icon>edit</mat-icon></button>
                <button mat-icon-button (click)="submitRule(row)" title="Submit for approval"><mat-icon>send</mat-icon></button>
              }
              @if (canApproveConfig && row.status === 'PendingApproval') {
                <button mat-icon-button (click)="decideRule(row, true)" title="Approve"><mat-icon>check_circle</mat-icon></button>
                <button mat-icon-button (click)="decideRule(row, false)" title="Reject"><mat-icon>cancel</mat-icon></button>
              }
            </ng-template>
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `
})
export class ProcurementPlanComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);

  financialYear = `${new Date().getFullYear()}/${String((new Date().getFullYear() + 1) % 100).padStart(2, '0')}`;
  readonly planItems = signal<any[]>([]);
  readonly forecast = signal<any[]>([]);
  readonly rules = signal<any[]>([]);

  planColumns: Column[] = [
    { key: 'projectReference', label: 'Project' }, { key: 'description', label: 'Description' },
    { key: 'estimatedValue', label: 'Estimated value', type: 'money' }, { key: 'plannedMethod', label: 'Method' },
    { key: 'plannedRequisitionDate', label: 'Planned requisition', type: 'date' }, { key: 'plannedAdvertDate', label: 'Planned advert', type: 'date' },
    { key: 'plannedAwardDate', label: 'Planned award', type: 'date' }, { key: 'actualAwardDate', label: 'Actual award', type: 'date' },
    { key: 'awardVarianceDays', label: 'Award variance (days)', type: 'number' }, { key: 'status', label: 'Status', type: 'status' }
  ];
  forecastColumns: Column[] = [
    { key: 'period', label: 'Period' }, { key: 'contractPayments', label: 'Contract payments', type: 'money' },
    { key: 'plannedProcurement', label: 'Planned procurement', type: 'money' }, { key: 'total', label: 'Total', type: 'money' }
  ];
  ruleColumns: Column[] = [
    { key: 'methodCode', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'minValue', label: 'Min value', type: 'money' },
    { key: 'maxValue', label: 'Max value', type: 'money' }, { key: 'effectiveFrom', label: 'From', type: 'date' },
    { key: 'ruleVersion', label: 'Version', type: 'number' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'requiresPublication', label: 'Publication', type: 'bool' }, { key: 'approvalAuthority', label: 'Authority' }
  ];

  get canManage(): boolean { return this.auth.hasAny(P.procurementManage, P.budgetManage); }
  get canApproveConfig(): boolean { return this.auth.has(P.adminConfigApprove); }

  ngOnInit(): void {
    this.loadPlan();
    this.loadRules();
  }

  loadPlan(): void {
    this.api.get<any[]>('procurement/plan', { financialYear: this.financialYear }).subscribe(r => this.planItems.set(r));
    this.api.get<any[]>('procurement/plan/commitment-forecast', { financialYear: this.financialYear }).subscribe(r => this.forecast.set(r));
  }

  loadRules(): void {
    this.api.get<any[]>('procurement/method-rules').subscribe(r => this.rules.set(r));
  }

  generate(): void {
    this.api.post('procurement/plan/generate', {}, { financialYear: this.financialYear }).subscribe((r: any) => {
      this.snack.open(`${r.created} plan item(s) generated.`, 'OK', { duration: 3000 });
      this.loadPlan();
    });
  }

  async addPlanItem(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New demand plan item', fields: planItemFields(this.refs), value: { financialYear: this.financialYear } }, '780px');
    if (v) this.api.post('procurement/plan', v).subscribe(() => this.loadPlan());
  }

  async editPlanItem(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit plan item', fields: planItemFields(this.refs, true), value: row }, '780px');
    if (v) this.api.put(`procurement/plan/${row.id}`, v).subscribe(() => this.loadPlan());
  }

  async addRule(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New method rule', fields: methodRuleFields() }, '780px');
    if (v) this.api.post('procurement/method-rules', v).subscribe(() => this.loadRules());
  }

  async editRule(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit method rule', fields: methodRuleFields(), value: row }, '780px');
    if (v) this.api.put(`procurement/method-rules/${row.id}`, v).subscribe(() => this.loadRules());
  }

  submitRule(row: any): void {
    this.api.post(`procurement/method-rules/${row.id}/submit`).subscribe(() => {
      this.snack.open('Submitted for approval.', 'OK', { duration: 3000 });
      this.loadRules();
    });
  }

  async decideRule(row: any, approve: boolean): Promise<void> {
    const v = await openForm(this.dialog, { title: approve ? 'Approve method rule' : 'Reject method rule',
      fields: [{ key: 'comment', label: 'Comment', type: 'textarea', required: !approve }] }, '520px');
    if (!v) return;
    this.api.post(`procurement/method-rules/${row.id}/decision`, { approve, comment: v['comment'] }).subscribe(() => this.loadRules());
  }
}
