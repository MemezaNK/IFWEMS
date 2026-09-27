import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { METHOD_CODES } from './procurement-forms';

/** Procurement register: sourcing processes from specification through award (SRS §5.4). */
@Component({
  selector: 'teta-procurement-list',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatFormFieldModule, MatSelectModule, FormsModule, RouterLink, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Procurements</h1>
        <a mat-stroked-button routerLink="/procurement/dashboard"><mat-icon>dashboard</mat-icon> Dashboard</a>
      </div>
      <div class="toolbar-row">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 200px">
          <mat-label>Status</mat-label>
          <mat-select [(ngModel)]="status" (ngModelChange)="load()">
            <mat-option [value]="null">All</mat-option>
            @for (s of statuses; track s) { <mat-option [value]="s">{{ s }}</mat-option> }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 200px">
          <mat-label>Method</mat-label>
          <mat-select [(ngModel)]="method" (ngModelChange)="load()">
            <mat-option [value]="null">All</mat-option>
            @for (m of methods; track m) { <mat-option [value]="m">{{ m }}</mat-option> }
          </mat-select>
        </mat-form-field>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="open($event)" emptyText="No procurements found." exportName="procurements" />
    </div>
  `
})
export class ProcurementListComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly rows = signal<any[]>([]);
  status: string | null = null;
  method: string | null = null;
  statuses = ['Planning', 'SpecificationApproved', 'Published', 'BidsClosed', 'Evaluation', 'Adjudication', 'Awarded', 'Cancelled'];
  methods = METHOD_CODES;

  columns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'title', label: 'Title' }, { key: 'projectReference', label: 'Project' },
    { key: 'method', label: 'Method' }, { key: 'estimatedValue', label: 'Estimated value', type: 'money' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'closingDateUtc', label: 'Closing', type: 'datetime' },
    { key: 'bidCount', label: 'Bids', type: 'number' }, { key: 'daysOpen', label: 'Days open', type: 'number' },
    { key: 'plannedAwardDate', label: 'Planned award', type: 'date' }, { key: 'actualAwardDate', label: 'Actual award', type: 'date' }
  ];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any>('procurement', { status: this.status, method: this.method, pageSize: 500 })
      .subscribe(r => this.rows.set(r.items));
  }

  open(row: any): void {
    this.router.navigate(['/procurement', row.id]);
  }
}
