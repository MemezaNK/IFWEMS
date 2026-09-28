import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { projectFields } from './project-forms';

const STATUSES = ['Concept', 'BusinessCase', 'SubmittedForApproval', 'Approved', 'InExecution', 'OnHold', 'Closing', 'Closed', 'Cancelled', 'Rejected'];

/** Project register with governance filters (FR-POR-001/005). Only authorised projects are listed (row-level security). */
@Component({
  selector: 'teta-projects',
  standalone: true,
  imports: [DataTableComponent, FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Projects</h1>
        @if (canCreate) { <button mat-flat-button color="primary" (click)="register()"><mat-icon>add</mat-icon> Register project</button> }
      </div>
      <div class="toolbar-row">
        <mat-form-field subscriptSizing="dynamic"><mat-label>Status</mat-label>
          <mat-select [(ngModel)]="status" (ngModelChange)="load()"><mat-option [value]="null">All</mat-option>
            @for (s of statuses; track s) { <mat-option [value]="s">{{ s }}</mat-option> }</mat-select></mat-form-field>
        <mat-form-field subscriptSizing="dynamic"><mat-label>Health</mat-label>
          <mat-select [(ngModel)]="health" (ngModelChange)="load()"><mat-option [value]="null">All</mat-option>
            @for (h of ['Green', 'Amber', 'Red', 'NotAssessed']; track h) { <mat-option [value]="h">{{ h }}</mat-option> }</mat-select></mat-form-field>
        <mat-form-field subscriptSizing="dynamic"><mat-label>Search</mat-label><input matInput [(ngModel)]="search" (keyup.enter)="load()" /></mat-form-field>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="open($event)" exportName="projects" [filterable]="false" />
    </div>
  `
})
export class ProjectsListComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly rows = signal<any[]>([]);
  readonly statuses = STATUSES;
  status: string | null = null;
  health: string | null = null;
  search = '';

  columns: Column[] = [
    { key: 'reference', label: 'Reference' }, { key: 'name', label: 'Name' }, { key: 'programmeName', label: 'Programme' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'stage', label: 'Stage' }, { key: 'health', label: 'Health', type: 'status' },
    { key: 'managerName', label: 'Manager' }, { key: 'approvedBudget', label: 'Approved budget', type: 'money' },
    { key: 'plannedEnd', label: 'Planned end', type: 'date' }, { key: 'priorityScore', label: 'Priority', type: 'number' }, { key: 'province', label: 'Province' }
  ];

  get canCreate(): boolean {
    return this.auth.has(P.projectCreate);
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    this.status = q.get('status');
    this.health = q.get('health');
    this.load();
  }

  load(): void {
    this.api.get<any>('projects', { status: this.status, health: this.health, search: this.search, pageSize: 500 }).subscribe(r => this.rows.set(r.items));
  }

  open(row: any): void {
    this.router.navigate(['/projects', row.id]);
  }

  async register(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Register project (concept)', fields: projectFields(this.refs),
      intro: 'A draft reference (CON-…) is issued now; the permanent project number (PRJ-…) is issued when the business case is approved (BR-002).' });
    if (v) this.api.post<any>('projects', v).subscribe(p => { this.refs.invalidate('projects'); this.router.navigate(['/projects', p.id]); });
  }
}
