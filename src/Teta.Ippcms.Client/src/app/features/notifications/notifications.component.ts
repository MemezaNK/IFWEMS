import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';

/** Full notifications list (alerts, deadline and SLA reminders) with mark-as-read and click-through (NFR-016). */
@Component({
  selector: 'teta-notifications',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatCheckboxModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Notifications</h1>
        <mat-checkbox [(ngModel)]="unreadOnly" (ngModelChange)="load()">Unread only</mat-checkbox>
        <span class="spacer"></span>
        <button mat-stroked-button (click)="markAllRead()"><mat-icon>done_all</mat-icon> Mark all read</button>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="open($event)" emptyText="No notifications." />
    </div>
  `
})
export class NotificationsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly rows = signal<any[]>([]);
  unreadOnly = false;

  columns: Column[] = [
    { key: 'isRead', label: 'Read', type: 'bool' }, { key: 'title', label: 'Title' }, { key: 'message', label: 'Message' },
    { key: 'category', label: 'Category' }, { key: 'createdAtUtc', label: 'Received', type: 'datetime' }, { key: 'emailStatus', label: 'Email' }
  ];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any>('notifications', { unreadOnly: this.unreadOnly, pageSize: 200 }).subscribe(r => this.rows.set(r.items));
  }

  open(row: any): void {
    if (!row.isRead) this.api.post('notifications/read', {}, { id: row.id }).subscribe(() => this.load());
    if (row.link) this.router.navigateByUrl(row.link);
  }

  markAllRead(): void {
    this.api.post('notifications/read').subscribe(() => this.load());
  }
}
