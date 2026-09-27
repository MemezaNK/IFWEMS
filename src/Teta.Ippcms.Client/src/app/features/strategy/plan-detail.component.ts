import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { confirmAction, openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { WorkflowPanelComponent } from '../../shared/workflow-panel.component';

@Component({
  selector: 'teta-plan-detail',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatExpansionModule, MatTabsModule, StatusChipComponent, WorkflowPanelComponent, DatePipe, DecimalPipe],
  template: `
    @if (tree(); as t) {
      <div class="page">
        <div class="page-header">
          <h1>{{ t.plan.code }} – {{ t.plan.name }}</h1>
          <teta-status [value]="t.plan.status" />
          @if (canManage) {
            @if (t.plan.status === 'Draft') {
              <button mat-stroked-button (click)="addOutcome()"><mat-icon>add</mat-icon> Outcome</button>
              <button mat-stroked-button (click)="addObjective()"><mat-icon>add</mat-icon> Objective</button>
              <button mat-flat-button color="primary" (click)="submit()">Submit for approval</button>
            }
            @if (t.plan.status === 'Approved') { <button mat-stroked-button (click)="revise()">Start revision</button> }
          }
          <div class="subtitle">{{ t.plan.periodStart }} to {{ t.plan.periodEnd }} · version {{ t.plan.versionNumber }}</div>
        </div>
        <mat-tab-group>
          <mat-tab label="Hierarchy">
            <div style="padding-top: 12px">
              @for (o of t.outcomes; track o.id) { <div class="small muted">Outcome {{ o.code }}: {{ o.description }}</div> }
              <mat-accordion multi>
                @for (obj of t.objectives; track obj.id) {
                  <mat-expansion-panel [expanded]="true">
                    <mat-expansion-panel-header>
                      <mat-panel-title>{{ obj.code }} – {{ obj.description }}</mat-panel-title>
                      <mat-panel-description>{{ obj.ownerName }} · {{ obj.indicators.length }} indicator(s)</mat-panel-description>
                    </mat-expansion-panel-header>
                    @if (canManage) { <button mat-stroked-button (click)="addIndicator(obj.id)"><mat-icon>add</mat-icon> Indicator</button> }
                    @for (ind of obj.indicators; track ind.id) {
                      <div class="card" style="margin-top: 12px">
                        <div class="toolbar-row">
                          <strong>{{ ind.code }} {{ ind.name }}</strong>
                          <span class="muted small">{{ ind.unitOfMeasure }} · {{ ind.measure }}</span>
                          <span class="spacer"></span>
                          @if (canManage) { <button mat-button (click)="addTarget(ind.id)"><mat-icon>add</mat-icon> Target</button> }
                        </div>
                        <div class="small muted">Evidence rule: {{ ind.evidenceRule }} · required evidence: {{ ind.requiredEvidenceTypes || 'any verified evidence' }}</div>
                        <table class="small" style="margin-top: 8px; width: 100%">
                          <tr class="muted"><td>FY</td><td>Quarter</td><td>Target</td><td>Forecast</td><td>Status</td><td></td></tr>
                          @for (tg of ind.targets; track tg.id) {
                            <tr>
                              <td>{{ tg.financialYear }}</td><td>Q{{ tg.quarter }}</td><td>{{ tg.targetValue | number }}</td>
                              <td>{{ tg.forecastValue | number }}</td><td><teta-status [value]="tg.status" /></td>
                              <td class="right">
                                @if (canManage && tg.status === 'Draft') { <button mat-button (click)="submitTarget(tg.id)">Submit</button> }
                                @if (tg.status !== 'Draft') { <button mat-button (click)="forecast(tg)">Forecast</button> }
                              </td>
                            </tr>
                          }
                        </table>
                      </div>
                    }
                  </mat-expansion-panel>
                }
              </mat-accordion>
            </div>
          </mat-tab>
          <mat-tab label="Approvals"><div style="padding-top: 12px"><teta-workflow entityType="StrategicPlan" [entityId]="id" [refresh]="refresh()" /></div></mat-tab>
          <mat-tab label="Versions">
            <div style="padding-top: 12px">
              @for (v of versions(); track v.id) {
                <div class="card" style="margin-bottom: 8px">Version {{ v.versionNumber }} approved {{ v.approvedAtUtc | date: 'yyyy-MM-dd' }} by {{ v.approvedBy }} — {{ v.changeSummary }}</div>
              } @empty { <p class="muted">No approved versions yet.</p> }
            </div>
          </mat-tab>
        </mat-tab-group>
      </div>
    }
  `
})
export class PlanDetailComponent implements OnChanges {
  @Input() id!: string;
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly tree = signal<any | null>(null);
  readonly versions = signal<any[]>([]);
  readonly refresh = signal(0);

  get canManage(): boolean {
    return this.auth.has(P.strategyManage);
  }

  ngOnChanges(): void {
    this.load();
  }

  load(): void {
    this.api.get(`strategy/plans/${this.id}`).subscribe(t => this.tree.set(t));
    this.api.get<any[]>(`strategy/plans/${this.id}/versions`).subscribe(v => this.versions.set(v));
    this.refresh.update(x => x + 1);
  }

  async addOutcome(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New outcome', fields: [
      { key: 'code', label: 'Code', required: true }, { key: 'description', label: 'Description', type: 'textarea', required: true }] });
    if (v) this.api.post(`strategy/plans/${this.id}/outcomes`, v).subscribe(() => this.load());
  }

  async addObjective(): Promise<void> {
    const outcomes = (this.tree()?.outcomes ?? []).map((o: any) => ({ value: o.id, label: `${o.code} ${o.description}` }));
    const v = await openForm(this.dialog, { title: 'New strategic objective', fields: [
      { key: 'outcomeId', label: 'Outcome', type: 'select', options: outcomes }, { key: 'code', label: 'Code', required: true },
      { key: 'description', label: 'Description', type: 'textarea', required: true }, { key: 'ownerName', label: 'Owner' },
      { key: 'programmeName', label: 'Budget programme' }] });
    if (v) this.api.post(`strategy/plans/${this.id}/objectives`, v).subscribe(() => this.load());
  }

  async addIndicator(objectiveId: string): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New APP indicator', fields: [
      { key: 'code', label: 'Code', required: true }, { key: 'name', label: 'Indicator name', required: true, wide: true },
      { key: 'unitOfMeasure', label: 'Unit of measure', required: true }, { key: 'measure', label: 'Calculation method', required: true },
      { key: 'evidenceRule', label: 'Evidence rule', type: 'textarea', required: true },
      { key: 'requiredEvidenceTypes', label: 'Required evidence types (comma separated)', wide: true },
      { key: 'responsibleExecutive', label: 'Responsible executive' }, { key: 'isCumulative', label: 'Cumulative over quarters', type: 'checkbox' }],
      value: { unitOfMeasure: 'Number', measure: 'Sum of verified project results', isCumulative: true } });
    if (v) this.api.post('strategy/indicators', { ...v, objectiveId }).subscribe(() => this.load());
  }

  async addTarget(indicatorId: string): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Quarterly target', fields: [
      { key: 'financialYear', label: 'Financial year (e.g. 2026/27)', required: true },
      { key: 'quarter', label: 'Quarter', type: 'select', required: true, options: [1, 2, 3, 4].map(q => ({ value: q, label: 'Q' + q })) },
      { key: 'targetValue', label: 'Target', type: 'number', required: true, min: 0 }], value: { financialYear: '2026/27' } });
    if (v) this.api.post('strategy/targets', { ...v, indicatorId }).subscribe(() => this.load());
  }

  submitTarget(targetId: string): void {
    this.api.post(`strategy/targets/${targetId}/submit`).subscribe(() => { this.snack.open('Target submitted for approval.', 'OK', { duration: 3000 }); this.load(); });
  }

  async forecast(target: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Forecast and commentary', fields: [
      { key: 'forecastValue', label: 'Forecast', type: 'number' }, { key: 'forecastCommentary', label: 'Commentary', type: 'textarea' }],
      value: { forecastValue: target.forecastValue, forecastCommentary: target.forecastCommentary } });
    if (v) this.api.put(`strategy/targets/${target.id}/forecast`, v).subscribe(() => this.load());
  }

  submit(): void {
    this.api.post(`strategy/plans/${this.id}/submit`).subscribe(() => { this.snack.open('Plan submitted for approval.', 'OK', { duration: 3000 }); this.load(); });
  }

  async revise(): Promise<void> {
    const reason = await confirmAction(this.dialog, 'Revise approved plan', 'A new draft version is created; the approved version stays on record.', 'Reason for revision');
    if (reason !== undefined) this.api.post(`strategy/plans/${this.id}/revise`, { reason }).subscribe(() => this.load());
  }
}
