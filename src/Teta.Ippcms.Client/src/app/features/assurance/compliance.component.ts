import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { coverageFields, obligationFields } from './assurance-forms';

/** Compliance obligations, periodic attestations and the combined-assurance coverage map (FR-RSK-005..007). */
@Component({
  selector: 'teta-compliance',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, MatFormFieldModule, MatInputModule, MatTabsModule, FormsModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>Compliance</h1></div>
      <mat-tab-group animationDuration="0">
        <mat-tab label="Obligations & attestations">
          <div style="padding-top: 12px">
            <div class="toolbar-row"><span class="spacer"></span>
              @if (canManage) { <button mat-stroked-button (click)="addObligation()"><mat-icon>add</mat-icon> New obligation</button> }
            </div>
            <teta-data-table [columns]="obligationColumns" [rows]="obligations()" [actions]="canAttest ? obligationActions : undefined"
                             emptyText="No compliance obligations configured." exportName="obligations" />
            <ng-template #obligationActions let-row>
              <button mat-icon-button matTooltip="Attest" (click)="attest(row)"><mat-icon>fact_check</mat-icon></button>
              @if (canManage) { <button mat-icon-button matTooltip="Edit" (click)="editObligation(row)"><mat-icon>edit</mat-icon></button> }
            </ng-template>
          </div>
        </mat-tab>
        <mat-tab label="Assurance coverage">
          <div style="padding-top: 12px">
            <div class="toolbar-row">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 160px">
                <mat-label>Period</mat-label>
                <input matInput [(ngModel)]="period" (ngModelChange)="loadCoverage()" placeholder="2026 Q2" />
              </mat-form-field>
              <span class="spacer"></span>
              @if (canManage) { <button mat-stroked-button (click)="addCoverage()"><mat-icon>add</mat-icon> Add coverage record</button> }
            </div>
            @if (coverage(); as c) {
              <p class="small muted">Gaps: {{ c.gaps }}</p>
              <table class="full-width small">
                <tr class="muted"><td>Area</td>@for (l of c.lines; track l) { <td>{{ l }}</td> }<td>Gap</td></tr>
                @for (row of c.rows; track row.area) {
                  <tr><td>{{ row.area }}</td>@for (l of c.lines; track l) { <td>{{ row.coveredByLine[l] ? '✔' : '✖' }}</td> }
                    <td>{{ row.hasGap ? '✖' : '✔' }}</td></tr>
                }
              </table>
            }
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `
})
export class ComplianceComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  readonly obligations = signal<any[]>([]);
  readonly coverage = signal<any | null>(null);
  period = `${new Date().getFullYear()} Q${Math.ceil((new Date().getMonth() + 1) / 3)}`;

  obligationColumns: Column[] = [
    { key: 'code', label: 'Code' }, { key: 'title', label: 'Title' }, { key: 'source', label: 'Source' }, { key: 'frequency', label: 'Frequency' },
    { key: 'ownerName', label: 'Owner' }, { key: 'isActive', label: 'Active', type: 'bool' }, { key: 'lastPeriod', label: 'Last period' },
    { key: 'lastResult', label: 'Last result', type: 'status' }
  ];

  get canManage(): boolean { return this.auth.has(P.assuranceManage); }
  get canAttest(): boolean { return this.auth.has(P.complianceAttest); }

  ngOnInit(): void {
    this.load();
    this.loadCoverage();
  }

  load(): void {
    this.api.get<any[]>('assurance/obligations').subscribe(r => this.obligations.set(r));
  }

  loadCoverage(): void {
    this.api.get('assurance/coverage', { period: this.period }).subscribe(r => this.coverage.set(r));
  }

  async addObligation(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New compliance obligation', fields: obligationFields(this.refs), value: { isActive: true } }, '780px');
    if (v) this.api.post('assurance/obligations', v).subscribe(() => this.load());
  }

  async editObligation(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Edit compliance obligation', fields: obligationFields(this.refs), value: row }, '780px');
    if (v) this.api.put(`assurance/obligations/${row.id}`, v).subscribe(() => this.load());
  }

  async attest(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: `Attest – ${row.title}`, fields: [
      { key: 'period', label: 'Period', required: true, hint: 'e.g. 2026 Q2' },
      { key: 'result', label: 'Result', type: 'select', required: true, options: [{ value: 'Compliant', label: 'Compliant' },
        { value: 'PartiallyCompliant', label: 'Partially compliant' }, { value: 'NonCompliant', label: 'Non-compliant' }, { value: 'NotApplicable', label: 'Not applicable' }] },
      { key: 'comment', label: 'Comment', type: 'textarea' }
    ], value: { period: this.period } }, '620px');
    if (v) this.api.post(`assurance/obligations/${row.id}/attest`, v).subscribe(() => this.load());
  }

  async addCoverage(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Add coverage record', fields: coverageFields(this.refs), value: { period: this.period } }, '780px');
    if (v) this.api.post('assurance/coverage', v).subscribe(() => this.loadCoverage());
  }
}
