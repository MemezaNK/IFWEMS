import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';

/** Corrective actions raised from monitoring visits, audit findings and risk treatments, plus the open findings register (FR-ME-005/006). */
@Component({
  selector: 'teta-findings-actions',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatCheckboxModule, MatTabsModule, FormsModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>Findings &amp; corrective actions</h1></div>
      <mat-tab-group animationDuration="0">
        <mat-tab label="Actions">
          <div style="padding-top: 12px">
            <div class="toolbar-row"><mat-checkbox [(ngModel)]="mineOnly" (ngModelChange)="loadActions()">My actions only</mat-checkbox>
              <mat-checkbox [(ngModel)]="openOnly" (ngModelChange)="loadActions()">Open only</mat-checkbox></div>
            <teta-data-table [columns]="actionColumns" [rows]="actions()" [actions]="actionActions" emptyText="No corrective actions." exportName="corrective-actions" />
            <ng-template #actionActions let-row>
              @if (row.status !== 'Completed') { <button mat-icon-button (click)="complete(row)" title="Complete"><mat-icon>check_circle</mat-icon></button> }
            </ng-template>
          </div>
        </mat-tab>
        @if (canReadFindings) {
          <mat-tab label="Findings">
            <div style="padding-top: 12px">
              <div class="toolbar-row"><mat-checkbox [(ngModel)]="openFindingsOnly" (ngModelChange)="loadFindings()">Open only</mat-checkbox></div>
              <teta-data-table [columns]="findingColumns" [rows]="findings()" emptyText="No findings." exportName="me-findings" />
            </div>
          </mat-tab>
        }
      </mat-tab-group>
    </div>
  `
})
export class FindingsActionsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  readonly actions = signal<any[]>([]);
  readonly findings = signal<any[]>([]);
  mineOnly = true;
  openOnly = true;
  openFindingsOnly = true;

  actionColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'parentType', label: 'Source' }, { key: 'description', label: 'Description' },
    { key: 'ownerName', label: 'Owner' }, { key: 'dueDate', label: 'Due', type: 'date' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'isOverdue', label: 'Overdue', type: 'bool' }, { key: 'escalationLevel', label: 'Escalation', type: 'number' }
  ];
  findingColumns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'projectReference', label: 'Project' }, { key: 'source', label: 'Source' },
    { key: 'description', label: 'Description' }, { key: 'severity', label: 'Severity', type: 'status' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'openActions', label: 'Open actions', type: 'number' }
  ];

  get canReadFindings(): boolean { return this.auth.has(P.meRead); }

  ngOnInit(): void {
    this.loadActions();
    if (this.canReadFindings) this.loadFindings();
  }

  loadActions(): void {
    this.api.get<any[]>('me/actions', { mineOnly: this.mineOnly, openOnly: this.openOnly }).subscribe(r => this.actions.set(r));
  }

  loadFindings(): void {
    this.api.get<any[]>('me/findings', { openOnly: this.openFindingsOnly }).subscribe(r => this.findings.set(r));
  }

  async complete(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Complete action', fields: [{ key: 'closureNotes', label: 'Closure notes', type: 'textarea', required: true, wide: true }] }, '620px');
    if (v) this.api.post(`me/actions/${row.id}/complete`, v).subscribe(() => this.loadActions());
  }
}
