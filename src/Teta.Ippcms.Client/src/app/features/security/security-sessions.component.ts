import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { confirmAction } from '../../shared/form-dialog.component';

/** Active sign-in sessions, with the ability to revoke a session immediately (SEC-009). */
@Component({
  selector: 'teta-security-sessions',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div style="padding-top: 12px" class="card">
      <teta-data-table [columns]="columns" [rows]="rows()" [actions]="actions" emptyText="No active sessions." exportName="sessions" />
      <ng-template #actions let-row>
        @if (row.active) { <button mat-icon-button (click)="revoke(row)" title="Revoke"><mat-icon>logout</mat-icon></button> }
      </ng-template>
    </div>
  `
})
export class SecuritySessionsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly rows = signal<any[]>([]);

  columns: Column[] = [
    { key: 'username', label: 'User' }, { key: 'issuedAtUtc', label: 'Signed in', type: 'datetime' }, { key: 'lastSeenAtUtc', label: 'Last seen', type: 'datetime' },
    { key: 'expiresAtUtc', label: 'Expires', type: 'datetime' }, { key: 'ipAddress', label: 'IP address' }, { key: 'mfaSatisfied', label: 'MFA satisfied', type: 'bool' },
    { key: 'active', label: 'Active', type: 'bool' }
  ];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('security/sessions').subscribe(r => this.rows.set(r));
  }

  async revoke(row: any): Promise<void> {
    const reason = await confirmAction(this.dialog, 'Revoke session', `Revoke the session for ${row.username ?? 'this user'}?`, 'Reason');
    if (reason === undefined) return;
    this.api.post(`security/sessions/${row.id}/revoke`, { reason }).subscribe(() => {
      this.snack.open('Session revoked.', 'OK', { duration: 3000 });
      this.load();
    });
  }
}
