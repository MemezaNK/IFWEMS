import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { AUTHORITY_TYPES, P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { confirmAction, openForm } from '../../shared/form-dialog.component';
import { delegationFields, substitutionFields } from './admin-forms';

/** Financial/approval delegations of authority and time-bound substitutions while a delegate is unavailable (FR-ADM-002/003). */
@Component({
  selector: 'teta-admin-access',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatCheckboxModule, MatFormFieldModule, MatSelectModule, MatTabsModule, DataTableComponent, DecimalPipe],
  template: `
    <div style="padding-top: 12px">
      <mat-tab-group animationDuration="0">
        <mat-tab label="Delegations of authority">
          <div style="padding-top: 12px" class="card">
            <div class="toolbar-row">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 260px">
                <mat-label>Check my limit for…</mat-label>
                <mat-select [(ngModel)]="checkType" (ngModelChange)="checkLimit()">
                  @for (t of authorityTypes; track t) { <mat-option [value]="t">{{ t }}</mat-option> }
                </mat-select>
              </mat-form-field>
              @if (myLimit() !== null) { <span class="small muted">My delegated limit: {{ myLimit() | number: '1.2-2' }}</span> }
              <span class="spacer"></span>
              @if (canManage) { <button mat-flat-button color="primary" (click)="addDelegation()"><mat-icon>add</mat-icon> New delegation</button> }
            </div>
            <teta-data-table [columns]="delegationColumns" [rows]="delegations()" [actions]="canManage ? delegationActions : undefined"
                              emptyText="No delegations configured." exportName="delegations" />
            <ng-template #delegationActions let-row><button mat-icon-button (click)="addDelegation(row)" title="Edit"><mat-icon>edit</mat-icon></button></ng-template>
          </div>
        </mat-tab>
        <mat-tab label="Substitutions">
          <div style="padding-top: 12px" class="card">
            <div class="toolbar-row">
              <mat-checkbox [(ngModel)]="mineOnly" (ngModelChange)="loadSubstitutions()">Mine only</mat-checkbox>
              <span class="spacer"></span>
              @if (canSubstitute) { <button mat-flat-button color="primary" (click)="addSubstitution()"><mat-icon>add</mat-icon> New substitution</button> }
            </div>
            <teta-data-table [columns]="substitutionColumns" [rows]="substitutions()" [actions]="canSubstitute ? substitutionActions : undefined"
                              emptyText="No substitutions." exportName="substitutions" />
            <ng-template #substitutionActions let-row>
              @if (!row.isRevoked) { <button mat-icon-button (click)="revoke(row)" title="Revoke"><mat-icon>block</mat-icon></button> }
            </ng-template>
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `
})
export class AdminAccessComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly delegations = signal<any[]>([]);
  readonly substitutions = signal<any[]>([]);
  readonly myLimit = signal<number | null>(null);
  mineOnly = true;
  checkType = AUTHORITY_TYPES[0];
  authorityTypes = AUTHORITY_TYPES;

  delegationColumns: Column[] = [
    { key: 'authorityType', label: 'Authority type' }, { key: 'roleCode', label: 'Role' }, { key: 'userName', label: 'User' },
    { key: 'maxAmount', label: 'Max amount', type: 'money' }, { key: 'effectiveFrom', label: 'From', type: 'date' }, { key: 'effectiveTo', label: 'To', type: 'date' },
    { key: 'isActive', label: 'Active', type: 'bool' }, { key: 'isEffective', label: 'Currently effective', type: 'bool' }, { key: 'policyReference', label: 'Policy ref.' }
  ];
  substitutionColumns: Column[] = [
    { key: 'principalName', label: 'Principal' }, { key: 'substituteName', label: 'Substitute' }, { key: 'fromUtc', label: 'From', type: 'datetime' },
    { key: 'toUtc', label: 'To', type: 'datetime' }, { key: 'reason', label: 'Reason' }, { key: 'isActive', label: 'Currently active', type: 'bool' },
    { key: 'isRevoked', label: 'Revoked', type: 'bool' }, { key: 'createdBy', label: 'Created by' }
  ];

  get canManage(): boolean { return this.auth.has(P.adminDelegations); }
  get canSubstitute(): boolean { return this.auth.hasAny(P.workflowDecide, P.adminDelegations); }

  ngOnInit(): void {
    this.loadDelegations();
    this.loadSubstitutions();
    this.checkLimit();
  }

  loadDelegations(): void {
    this.api.get<any[]>('admin/delegations').subscribe(r => this.delegations.set(r));
  }

  loadSubstitutions(): void {
    this.api.get<any[]>('admin/substitutions', { mineOnly: this.mineOnly }).subscribe(r => this.substitutions.set(r));
  }

  checkLimit(): void {
    this.api.get<{ limit: number | null }>('admin/delegations/my-limit', { authorityType: this.checkType }).subscribe(r => this.myLimit.set(r.limit));
  }

  async addDelegation(row?: any): Promise<void> {
    const v = await openForm(this.dialog, { title: row ? 'Edit delegation' : 'New delegation', fields: delegationFields(this.refs), value: row }, '680px');
    if (!v) return;
    (row ? this.api.put(`admin/delegations/${row.id}`, v) : this.api.post('admin/delegations', v)).subscribe(() => this.loadDelegations());
  }

  async addSubstitution(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New substitution', fields: substitutionFields(this.refs) }, '680px');
    if (v) this.api.post('admin/substitutions', v).subscribe(() => this.loadSubstitutions());
  }

  async revoke(row: any): Promise<void> {
    const ok = await confirmAction(this.dialog, 'Revoke substitution', `Revoke the substitution for ${row.substituteName}?`);
    if (ok !== undefined) this.api.post(`admin/substitutions/${row.id}/revoke`).subscribe(() => {
      this.snack.open('Substitution revoked.', 'OK', { duration: 3000 });
      this.loadSubstitutions();
    });
  }
}
