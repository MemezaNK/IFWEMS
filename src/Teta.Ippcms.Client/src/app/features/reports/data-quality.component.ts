import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';

/** Automatically-detected data-quality issues (missing evidence, stale reference data, unreconciled interfaces) with owner resolution (FR-REP-010). */
@Component({
  selector: 'teta-data-quality',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Data quality</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="scan()"><mat-icon>search</mat-icon> Run scan</button> }
      </div>
      @if (dashboard(); as d) {
        <p class="small muted">Open: {{ d.open }} · Resolved (30 days): {{ d.resolvedLast30Days }} · Interface errors: {{ d.interfaceErrors }} · Unbalanced batches: {{ d.unbalancedBatches }}</p>
      }
      <teta-data-table [columns]="columns" [rows]="issues()" [actions]="canManage ? actions : undefined" emptyText="No data-quality issues open." exportName="data-quality" />
      <ng-template #actions let-row>
        @if (row.status === 'Open') { <button mat-icon-button (click)="resolve(row)" title="Resolve"><mat-icon>check_circle</mat-icon></button> }
      </ng-template>
    </div>
  `
})
export class DataQualityComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly dashboard = signal<any | null>(null);
  readonly issues = signal<any[]>([]);

  columns: Column[] = [
    { key: 'ruleCode', label: 'Rule' }, { key: 'description', label: 'Description' }, { key: 'entityType', label: 'Entity type' },
    { key: 'entityReference', label: 'Reference' }, { key: 'severity', label: 'Severity', type: 'status' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'ownerName', label: 'Owner' }, { key: 'detectedAtUtc', label: 'Detected', type: 'datetime' }, { key: 'resolution', label: 'Resolution' }
  ];

  get canManage(): boolean { return this.auth.has(P.dataQualityManage); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get('reports/data-quality').subscribe((d: any) => {
      this.dashboard.set(d);
      this.issues.set(d.issues);
    });
  }

  scan(): void {
    this.api.post('reports/data-quality/scan').subscribe((r: any) => {
      this.snack.open(`${r.created} new issue(s) detected.`, 'OK', { duration: 3000 });
      this.load();
    });
  }

  async resolve(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Resolve issue', fields: [
      { key: 'status', label: 'Status', type: 'select', required: true, options: [{ value: 'Resolved', label: 'Resolved' }, { value: 'Closed', label: 'Closed' },
        { value: 'Ignored', label: 'Ignored' }] },
      { key: 'resolution', label: 'Resolution notes', type: 'textarea', required: true, wide: true }
    ] }, '620px');
    if (v) this.api.post(`reports/data-quality/${row.id}/resolve`, v).subscribe(() => this.load());
  }
}
