import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';

/** Role → permission matrix; changes to a privileged role's permissions require a second Security Administrator to approve (SEC-004). */
@Component({
  selector: 'teta-security-roles',
  standalone: true,
  imports: [MatButtonModule, MatCheckboxModule],
  template: `
    <div style="padding-top: 12px" class="card">
      <p class="small muted">Select a role to edit its permissions. Roles marked * have approval authority.</p>
      <div style="display: flex; gap: 8px; flex-wrap: wrap; margin: 8px 0 16px">
        @for (r of roles(); track r.code) {
          <button type="button" class="role-btn" [class.role-btn-selected]="selected()?.code === r.code" (click)="select(r)">
            {{ r.name }}@if (r.hasApprovalAuthority) { <span> *</span> }
          </button>
        }
      </div>
      @if (selected(); as r) {
        <h2>{{ r.name }}</h2>
        <p class="muted small">{{ r.description }}</p>
        <div class="grid cols-2">
          @for (p of allPermissions(); track p) {
            <mat-checkbox [checked]="draft().includes(p)" [disabled]="!canApprove" (change)="toggle(p)">{{ p }}</mat-checkbox>
          }
        </div>
        @if (canApprove) {
          <div class="toolbar-row" style="margin-top: 16px">
            <span class="spacer"></span>
            <button mat-flat-button color="primary" (click)="save()">Save permissions</button>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .role-btn { cursor: pointer; border: 1px solid #d7dbe0; background: #fff; border-radius: 6px; padding: 6px 12px; font-size: 13px; }
    .role-btn-selected { background: #e8f0fe; border-color: #2563eb; }
  `]
})
export class SecurityRolesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly snack = inject(MatSnackBar);
  readonly roles = signal<any[]>([]);
  readonly allPermissions = signal<string[]>([]);
  readonly selected = signal<any | null>(null);
  readonly draft = signal<string[]>([]);

  get canApprove(): boolean { return this.auth.has(P.securityRolesApprove); }

  ngOnInit(): void {
    this.api.get<any[]>('security/roles').subscribe(r => this.roles.set(r));
    this.api.get<string[]>('security/permissions').subscribe(p => this.allPermissions.set(p));
  }

  select(role: any): void {
    this.selected.set(role);
    this.draft.set([...role.permissions]);
  }

  toggle(permission: string): void {
    this.draft.update(list => (list.includes(permission) ? list.filter(p => p !== permission) : [...list, permission]));
  }

  save(): void {
    const role = this.selected();
    if (!role) return;
    this.api.put<any>(`security/roles/${role.code}/permissions`, { permissions: this.draft() }).subscribe(r => {
      this.snack.open('Role permissions saved.', 'OK', { duration: 3000 });
      this.roles.update(list => list.map(x => (x.code === r.code ? r : x)));
      this.select(r);
    });
  }
}
