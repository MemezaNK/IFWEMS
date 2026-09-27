import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';

/** Filtered, seal-verified audit trail search across every module (SRS §14, NFR-015; SEC-010 tamper evidence). */
@Component({
  selector: 'teta-audit-search',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>Audit trail</h1>
        <button mat-stroked-button (click)="verify()"><mat-icon>verified</mat-icon> Verify integrity</button>
      </div>
      <div class="card">
        <div class="grid cols-2">
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Module</mat-label><input matInput [(ngModel)]="query.module" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Entity type</mat-label><input matInput [(ngModel)]="query.entityType" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Entity ID</mat-label><input matInput [(ngModel)]="query.entityId" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Username</mat-label><input matInput [(ngModel)]="query.username" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Action</mat-label><input matInput [(ngModel)]="query.action" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Correlation ID</mat-label><input matInput [(ngModel)]="query.correlationId" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>From</mat-label><input matInput type="date" [(ngModel)]="query.fromUtc" /></mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>To</mat-label><input matInput type="date" [(ngModel)]="query.toUtc" /></mat-form-field>
        </div>
        <div class="toolbar-row"><span class="spacer"></span><button mat-flat-button color="primary" (click)="search()"><mat-icon>search</mat-icon> Search</button></div>
      </div>
      @if (integrity(); as i) {
        <div class="card" [class.info-banner]="i.intact">
          <p [style.color]="i.intact ? '#166534' : '#b91c1c'">
            {{ i.intact ? 'Integrity intact' : 'INTEGRITY ISSUE DETECTED' }}: checked {{ i.checked }} record(s) from #{{ i.fromId }} to #{{ i.toId }},
            {{ i.invalid }} invalid@if (i.invalidIds.length) { ({{ i.invalidIds.join(', ') }}) }.
          </p>
        </div>
      }
      <teta-data-table [columns]="columns" [rows]="rows()" emptyText="No audit records match this search." exportName="audit-log" />
    </div>
  `
})
export class AuditSearchComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly snack = inject(MatSnackBar);
  readonly rows = signal<any[]>([]);
  readonly integrity = signal<any | null>(null);
  query: any = {};

  columns: Column[] = [
    { key: 'occurredAtUtc', label: 'When', type: 'datetime' }, { key: 'username', label: 'User' }, { key: 'module', label: 'Module' },
    { key: 'entityType', label: 'Entity type' }, { key: 'entityId', label: 'Entity ID' }, { key: 'action', label: 'Action' },
    { key: 'workflowStep', label: 'Step' }, { key: 'reason', label: 'Reason' }, { key: 'sourceIp', label: 'Source IP' },
    { key: 'sealValid', label: 'Seal valid', type: 'bool' }
  ];

  ngOnInit(): void { this.search(); }

  search(): void {
    this.api.get<any>('audit', { ...this.query, pageSize: 500 }).subscribe(r => this.rows.set(r.items));
  }

  verify(): void {
    this.api.post<any>('audit/verify', {}, { fromUtc: this.query.fromUtc, toUtc: this.query.toUtc }).subscribe(r => {
      this.integrity.set(r);
      this.snack.open(r.intact ? 'Audit log integrity verified: intact.' : 'Audit log integrity issue detected — see banner.', 'OK', { duration: 5000 });
    });
  }
}
