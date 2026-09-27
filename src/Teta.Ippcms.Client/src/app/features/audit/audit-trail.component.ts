import { DatePipe } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';

/** End-to-end reconstruction of a single transaction: its lifecycle steps plus the underlying sealed audit records (SRS §14, NFR-015). */
@Component({
  selector: 'teta-audit-trail',
  standalone: true,
  imports: [DataTableComponent, DatePipe, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Audit trail – {{ entityType }}</h1>
        <a mat-stroked-button routerLink="/audit"><mat-icon>arrow_back</mat-icon> Full audit search</a>
      </div>
      @if (trail(); as t) {
        <div class="card">
          <h2 style="margin-top: 0">Lifecycle</h2>
          @for (s of t.steps; track s.occurredAtUtc + s.source) {
            <div class="toolbar-row" style="border-bottom: 1px solid #eef1f4; padding: 6px 0; margin: 0; align-items: flex-start">
              <span class="small muted" style="width: 160px">{{ s.occurredAtUtc | date: 'yyyy-MM-dd HH:mm' }}</span>
              <span class="small muted" style="width: 100px">{{ s.source }}</span>
              <div style="flex: 1">
                <div>{{ s.description }}</div>
                @if (s.details) { <div class="small muted">{{ s.details }}</div> }
              </div>
              @if (s.actor) { <span class="small muted">{{ s.actor }}</span> }
            </div>
          } @empty { <p class="muted">No lifecycle steps recorded.</p> }
        </div>
        <div class="card">
          <h2 style="margin-top: 0">Underlying audit records</h2>
          <teta-data-table [columns]="columns" [rows]="t.auditRecords" emptyText="No audit records." exportName="entity-trail" />
        </div>
      }
    </div>
  `
})
export class AuditTrailComponent implements OnChanges {
  @Input() entityType!: string;
  @Input() entityId!: string;
  private readonly api = inject(ApiService);
  readonly trail = signal<any | null>(null);

  columns: Column[] = [
    { key: 'occurredAtUtc', label: 'When', type: 'datetime' }, { key: 'username', label: 'User' }, { key: 'action', label: 'Action' },
    { key: 'workflowStep', label: 'Step' }, { key: 'reason', label: 'Reason' }, { key: 'sealValid', label: 'Seal valid', type: 'bool' }
  ];

  ngOnChanges(): void {
    this.api.get(`audit/trail/${this.entityType}/${this.entityId}`).subscribe(t => this.trail.set(t));
  }
}
