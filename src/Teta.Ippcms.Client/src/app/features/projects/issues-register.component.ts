import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { issueFields } from '../common/forms';

/** Cross-project issue register (FR-EXE-008): open items, owners, actions and escalation, filterable by project. */
@Component({
  selector: 'teta-issues-register',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatCheckboxModule, MatFormFieldModule, MatSelectModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Issues</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="edit()"><mat-icon>add</mat-icon> New issue</button> }
      </div>
      <div class="toolbar-row">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="min-width: 260px">
          <mat-label>Project</mat-label>
          <mat-select [(ngModel)]="projectId" (ngModelChange)="load()">
            <mat-option [value]="undefined">All projects</mat-option>
            @for (p of projects(); track p.value) { <mat-option [value]="p.value">{{ p.label }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-checkbox [(ngModel)]="openOnly" (ngModelChange)="load()">Open only</mat-checkbox>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" [actions]="canManage ? actions : undefined" (rowClick)="openProject($event)"
                        emptyText="No issues found." exportName="issues" />
      <ng-template #actions let-row>
        <button mat-icon-button (click)="edit(row, $event)" title="Edit"><mat-icon>edit</mat-icon></button>
      </ng-template>
    </div>
  `
})
export class IssuesRegisterComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  readonly rows = signal<any[]>([]);
  readonly projects = signal<{ value: any; label: string }[]>([]);
  projectId: string | undefined;
  openOnly = true;

  columns: Column[] = [
    { key: 'number', label: 'Issue' }, { key: 'projectReference', label: 'Project' }, { key: 'title', label: 'Title' },
    { key: 'severity', label: 'Severity', type: 'status' }, { key: 'ownerName', label: 'Owner' }, { key: 'dueDate', label: 'Due', type: 'date' },
    { key: 'status', label: 'Status', type: 'status', value: r => (r.isOverdue ? 'Overdue' : r.status) }, { key: 'escalationLevel', label: 'Escalation', type: 'number' }
  ];

  get canManage(): boolean { return this.auth.has(P.executionManage); }

  ngOnInit(): void {
    this.refs.projects().subscribe(p => this.projects.set(p));
    this.load();
  }

  load(): void {
    this.api.get<any[]>('issues', { projectId: this.projectId, openOnly: this.openOnly }).subscribe(r => this.rows.set(r));
  }

  openProject(row: any): void {
    this.router.navigate(['/projects', row.projectId]);
  }

  async edit(row?: any, e?: Event): Promise<void> {
    e?.stopPropagation();
    if (!row && !this.projectId) { alert('Select a project above before creating an issue.'); return; }
    const v = await openForm(this.dialog, { title: row ? 'Edit issue' : 'New issue', fields: issueFields(this.refs, !!row), value: row });
    if (!v) return;
    const body = { ...v, projectId: row?.projectId ?? this.projectId };
    (row ? this.api.put(`issues/${row.id}`, body) : this.api.post('issues', body)).subscribe(() => this.load());
  }
}
