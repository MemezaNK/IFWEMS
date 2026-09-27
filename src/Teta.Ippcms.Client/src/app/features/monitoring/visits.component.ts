import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { scheduleVisitFields } from './monitoring-forms';

/** Scheduled monitoring visits (desktop or site) linked to projects (FR-ME-002/003). */
@Component({
  selector: 'teta-visits',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Monitoring visits</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="schedule()"><mat-icon>add</mat-icon> Schedule visit</button> }
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="open($event)" emptyText="No visits scheduled." exportName="monitoring-visits" />
    </div>
  `
})
export class VisitsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  readonly rows = signal<any[]>([]);

  columns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'projectReference', label: 'Project' }, { key: 'type', label: 'Type' },
    { key: 'scheduledDate', label: 'Scheduled', type: 'date' }, { key: 'visitDate', label: 'Actual date', type: 'date' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'findingCount', label: 'Findings', type: 'number' }, { key: 'evidenceCount', label: 'Evidence', type: 'number' }
  ];

  get canManage(): boolean { return this.auth.has(P.meManage); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('me/visits').subscribe(r => this.rows.set(r));
  }

  open(row: any): void {
    this.router.navigate(['/me/visits', row.id]);
  }

  async schedule(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Schedule monitoring visit', fields: scheduleVisitFields(this.refs) }, '780px');
    if (v) this.api.post('me/visits', v).subscribe((visit: any) => this.router.navigate(['/me/visits', visit.id]));
  }
}
