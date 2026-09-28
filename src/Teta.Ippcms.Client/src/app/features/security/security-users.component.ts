import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { AuthService } from '../../core/auth.service';
import { ApiService } from '../../core/api.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { confirmAction, openForm } from '../../shared/form-dialog.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { assignmentFields, createUserFields } from './security-forms';

/** User accounts and scoped, effective-dated role assignments; new/changed assignments require a second Security Administrator to approve (SRS §10, SEC-003/004/005). */
@Component({
  selector: 'teta-security-users',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatTabsModule, DataTableComponent, StatusChipComponent],
  template: `
    <div style="padding-top: 12px">
      <mat-tab-group animationDuration="0">
        <mat-tab label="Users">
          <div style="padding-top: 12px" class="card">
            <div class="toolbar-row">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" style="min-width: 260px">
                <mat-label>Search</mat-label>
                <input matInput [(ngModel)]="search" (ngModelChange)="loadUsers()" placeholder="Username, email or name" />
              </mat-form-field>
              <mat-checkbox [(ngModel)]="tetaOnly" (ngModelChange)="loadUsers()">TETA access only</mat-checkbox>
              <span class="spacer"></span>
              @if (canManage) { <button mat-flat-button color="primary" (click)="createUser()"><mat-icon>person_add</mat-icon> New user</button> }
            </div>
            <teta-data-table [columns]="userColumns" [rows]="users()" [actions]="canManage ? userActions : undefined" (rowClick)="select($event)"
                              emptyText="No users found." exportName="users" />
            <ng-template #userActions let-row>
              @if (canManage) {
                <button mat-icon-button (click)="requestAssignment(row, $event)" title="Assign role"><mat-icon>add_moderator</mat-icon></button>
                <button mat-icon-button (click)="toggleMfa(row, $event)" [title]="row.mfaEnabled ? 'Disable MFA' : 'Enable MFA'">
                  <mat-icon>{{ row.mfaEnabled ? 'security' : 'security' }}</mat-icon>
                </button>
                <button mat-icon-button (click)="toggleActive(row, $event)" [title]="row.isActive ? 'Deactivate' : 'Activate'">
                  <mat-icon>{{ row.isActive ? 'person_off' : 'person' }}</mat-icon>
                </button>
                @if (row.lockedOut) { <button mat-icon-button (click)="unlock(row, $event)" title="Unlock"><mat-icon>lock_open</mat-icon></button> }
              }
            </ng-template>
            @if (selected(); as u) {
              <div style="margin-top: 12px">
                <h2>{{ u.displayName }} – assignments</h2>
                <teta-data-table [columns]="assignmentColumns" [rows]="u.assignments" [actions]="canManage ? assignmentActions : undefined" [paginate]="false" />
                <ng-template #assignmentActions let-row>
                  @if (row.status === 'Approved' && canManage) { <button mat-icon-button (click)="revoke(row)" title="Revoke"><mat-icon>block</mat-icon></button> }
                </ng-template>
              </div>
            }
          </div>
        </mat-tab>
        <mat-tab [label]="'Pending approvals (' + pending().length + ')'">
          <div style="padding-top: 12px" class="card">
            <teta-data-table [columns]="pendingColumns" [rows]="pending()" [actions]="canApprove ? pendingActions : undefined" emptyText="No assignment requests are waiting." />
            <ng-template #pendingActions let-row>
              <button mat-button color="primary" (click)="decide(row, true)">Approve</button>
              <button mat-button color="warn" (click)="decide(row, false)">Reject</button>
            </ng-template>
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `
})
export class SecurityUsersComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly users = signal<any[]>([]);
  readonly pending = signal<any[]>([]);
  readonly selected = signal<any | null>(null);
  search = '';
  tetaOnly = false;

  userColumns: Column[] = [
    { key: 'username', label: 'Username' }, { key: 'displayName', label: 'Name' }, { key: 'email', label: 'Email' },
    { key: 'isActive', label: 'Active', type: 'bool' }, { key: 'lockedOut', label: 'Locked out', type: 'bool' }, { key: 'mfaEnabled', label: 'MFA', type: 'bool' },
    { key: 'hasTetaAccess', label: 'TETA access', type: 'bool' }, { key: 'lastLoginAtUtc', label: 'Last login', type: 'datetime' }
  ];
  assignmentColumns: Column[] = [
    { key: 'roleCode', label: 'Role' }, { key: 'scopeType', label: 'Scope' }, { key: 'scopeName', label: 'Scope record' },
    { key: 'effectiveFrom', label: 'From', type: 'date' }, { key: 'effectiveTo', label: 'To', type: 'date' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'isEffective', label: 'Currently effective', type: 'bool' }, { key: 'approvedBy', label: 'Approved by' }
  ];
  pendingColumns: Column[] = [
    { key: 'roleCode', label: 'Role' }, { key: 'scopeType', label: 'Scope' }, { key: 'scopeName', label: 'Scope record' },
    { key: 'effectiveFrom', label: 'From', type: 'date' }, { key: 'requestedBy', label: 'Requested by' }, { key: 'reason', label: 'Reason' }
  ];

  get canManage(): boolean { return this.auth.has(P.securityUsers); }
  get canApprove(): boolean { return this.auth.has(P.securityRolesApprove); }

  ngOnInit(): void {
    this.loadUsers();
    this.loadPending();
  }

  loadUsers(): void {
    this.api.get<any>('security/users', { search: this.search, tetaOnly: this.tetaOnly, pageSize: 200 }).subscribe(r => this.users.set(r.items));
  }

  loadPending(): void {
    this.api.get<any[]>('security/assignments/pending').subscribe(r => this.pending.set(r));
  }

  select(row: any): void {
    this.selected.set(row);
  }

  async createUser(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New user', fields: createUserFields() }, '620px');
    if (v) this.api.post('security/users', v).subscribe(() => { this.snack.open('User created.', 'OK', { duration: 3000 }); this.loadUsers(); });
  }

  async toggleMfa(row: any, e: Event): Promise<void> {
    e.stopPropagation();
    const action = row.mfaEnabled ? 'Disable MFA' : 'Enable MFA';
    const reason = await confirmAction(this.dialog, action, `${action} for ${row.displayName}?`, 'Reason');
    if (reason === undefined) return;
    this.api.post(`security/users/${row.id}/mfa`, { enabled: !row.mfaEnabled, reason }).subscribe(() => {
      this.snack.open(row.mfaEnabled ? 'MFA disabled.' : 'MFA enabled.', 'OK', { duration: 3000 });
      this.loadUsers();
      this.selected.set(null);
    });
  }

  async toggleActive(row: any, e: Event): Promise<void> {
    e.stopPropagation();
    const reason = await confirmAction(this.dialog, row.isActive ? 'Deactivate user' : 'Activate user',
      `${row.isActive ? 'Deactivate' : 'Activate'} ${row.displayName}?`, 'Reason');
    if (reason === undefined) return;
    this.api.post(`security/users/${row.id}/active`, { active: !row.isActive, reason }).subscribe(() => { this.loadUsers(); this.selected.set(null); });
  }

  unlock(row: any, e: Event): void {
    e.stopPropagation();
    this.api.post(`security/users/${row.id}/unlock`).subscribe(() => {
      this.snack.open('User unlocked.', 'OK', { duration: 3000 });
      this.loadUsers();
    });
  }

  async requestAssignment(row: any, e: Event): Promise<void> {
    e.stopPropagation();
    const v = await openForm(this.dialog, { title: `Assign role to ${row.displayName}`, fields: assignmentFields(this.refs, row.id) }, '680px');
    if (!v) return;
    this.api.post('security/assignments', { ...v, userId: row.id }).subscribe(() => {
      this.snack.open('Assignment requested; a second Security Administrator must approve it.', 'OK', { duration: 4000 });
      this.loadUsers();
      this.loadPending();
    });
  }

  async revoke(row: any): Promise<void> {
    const reason = await confirmAction(this.dialog, 'Revoke assignment', `Revoke the ${row.roleCode} assignment?`, 'Reason');
    if (reason !== undefined) this.api.post(`security/assignments/${row.id}/revoke`, { reason }).subscribe(() => { this.loadUsers(); this.selected.set(null); });
  }

  async decide(row: any, approve: boolean): Promise<void> {
    const comment = await confirmAction(this.dialog, approve ? 'Approve assignment' : 'Reject assignment',
      `${approve ? 'Approve' : 'Reject'} the ${row.roleCode} assignment requested by ${row.requestedBy}?`, approve ? undefined : 'Reason');
    if (comment === undefined) return;
    this.api.post(`security/assignments/${row.id}/decision`, { approve, comment: comment || undefined }).subscribe(() => {
      this.snack.open(approve ? 'Assignment approved.' : 'Assignment rejected.', 'OK', { duration: 3000 });
      this.loadPending();
      this.loadUsers();
    });
  }
}
