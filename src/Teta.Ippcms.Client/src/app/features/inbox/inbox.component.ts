import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { WorkflowPanelComponent } from '../../shared/workflow-panel.component';
import { entityLink } from '../common/links';

/** Approvals inbox: tasks assigned to the user's roles (or as a substitute), with SLA, escalation and decision capture. */
@Component({
  selector: 'teta-inbox',
  standalone: true,
  imports: [DataTableComponent, WorkflowPanelComponent, MatButtonModule, MatIconModule, RouterLink, DatePipe, CurrencyPipe],
  template: `
    <div class="page">
      <div class="page-header"><h1>Approvals inbox</h1><button mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button></div>
      <teta-data-table [columns]="columns" [rows]="items()" [actions]="actions" (rowClick)="select($event)" emptyText="No approvals are waiting for you." />
      <ng-template #actions let-row>
        <a mat-icon-button [routerLink]="link(row.entityType, row.entityId, row.projectId)" title="Open record"><mat-icon>open_in_new</mat-icon></a>
        <button mat-flat-button color="primary" (click)="decide(row, 'Approved')">Approve</button>
        <button mat-button (click)="decide(row, 'Returned')">Return</button>
        <button mat-button color="warn" (click)="decide(row, 'Rejected')">Reject</button>
      </ng-template>
      @if (selected(); as s) {
        <div class="card" style="margin-top: 16px">
          <h2>{{ s.entityReference }} – {{ s.title }}</h2>
          <dl class="dl">
            <dt>Workflow</dt><dd>{{ s.definitionCode }}</dd>
            <dt>Current step</dt><dd>{{ s.stepName }} ({{ s.assignedRole }})</dd>
            <dt>Value</dt><dd>{{ s.transactionValue | currency: 'ZAR' : 'R ' }}</dd>
            <dt>Submitted</dt><dd>{{ s.startedAtUtc | date: 'yyyy-MM-dd HH:mm' }} by {{ s.startedBy }}</dd>
            <dt>Due</dt><dd>{{ s.dueAtUtc | date: 'yyyy-MM-dd HH:mm' }}</dd>
            @if (s.onBehalfOf) { <dt>Acting for</dt><dd>{{ s.onBehalfOf }}</dd> }
          </dl>
          <h2 style="margin-top: 16px">History</h2>
          <teta-workflow [entityType]="s.entityType" [entityId]="s.entityId" />
        </div>
      }
    </div>
  `
})
export class InboxComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly items = signal<any[]>([]);
  readonly selected = signal<any | null>(null);
  readonly link = entityLink;

  columns: Column[] = [
    { key: 'entityReference', label: 'Reference' }, { key: 'title', label: 'Title' }, { key: 'definitionCode', label: 'Workflow' },
    { key: 'stepName', label: 'Step' }, { key: 'transactionValue', label: 'Value', type: 'money' }, { key: 'startedBy', label: 'Submitted by' },
    { key: 'dueAtUtc', label: 'Due', type: 'datetime' },
    { key: 'sla', label: 'SLA', type: 'status', value: r => (r.isOverdue ? 'Overdue' : 'Pending') },
    { key: 'onBehalfOf', label: 'Acting for' }
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.api.get<any[]>('inbox').subscribe(r => this.items.set(r));
  }

  select(row: any): void {
    this.selected.set(row);
  }

  async decide(row: any, decision: 'Approved' | 'Rejected' | 'Returned'): Promise<void> {
    const result = await openForm(this.dialog, {
      title: `${decision === 'Approved' ? 'Approve' : decision === 'Rejected' ? 'Reject' : 'Return'} ${row.entityReference}`,
      intro: `${row.stepName} – ${row.title}`,
      fields: [{ key: 'comment', label: decision === 'Approved' ? 'Comment (optional)' : 'Reason', type: 'textarea', required: decision !== 'Approved', maxLength: 2000 }],
      submitLabel: decision === 'Approved' ? 'Approve' : decision === 'Rejected' ? 'Reject' : 'Return'
    }, '560px');
    if (!result) return;
    this.api.post<any>(`inbox/${row.taskId}/decision`, { decision, comment: result['comment'] }).subscribe(r => {
      this.snack.open(r.message ?? 'Decision recorded.', 'OK', { duration: 4000 });
      this.selected.set(null);
      this.load();
    });
  }
}
