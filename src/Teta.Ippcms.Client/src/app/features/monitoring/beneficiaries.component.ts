import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { beneficiaryFields } from './monitoring-forms';

/** Beneficiary register with masked PII, gated reveal and controlled duplicate resolution (FR-ME-007/008). */
@Component({
  selector: 'teta-beneficiaries',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatTooltipModule, FormsModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Beneficiaries</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="register()"><mat-icon>add</mat-icon> Register beneficiary</button> }
      </div>
      <div class="toolbar-row">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="min-width: 260px">
          <mat-label>Search</mat-label>
          <input matInput [(ngModel)]="search" (ngModelChange)="load()" />
        </mat-form-field>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" [actions]="actions" emptyText="No beneficiaries registered." exportName="beneficiaries" [filterable]="false" />
      <ng-template #actions let-row>
        @if (canReveal) { <button mat-icon-button matTooltip="Reveal identifier" (click)="reveal(row)"><mat-icon>visibility</mat-icon></button> }
        @if (canManage && row.potentialDuplicate) { <button mat-icon-button matTooltip="Resolve duplicate" (click)="resolveDuplicate(row)"><mat-icon>content_copy</mat-icon></button> }
      </ng-template>
    </div>
  `
})
export class BeneficiariesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  readonly rows = signal<any[]>([]);
  search = '';

  columns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'projectReference', label: 'Project' }, { key: 'identifierMasked', label: 'Identifier' },
    { key: 'firstName', label: 'First name' }, { key: 'lastName', label: 'Last name' }, { key: 'gender', label: 'Gender' },
    { key: 'province', label: 'Province' }, { key: 'intervention', label: 'Intervention' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'potentialDuplicate', label: 'Possible duplicate', type: 'bool' }, { key: 'consentObtained', label: 'Consent', type: 'bool' }
  ];

  get canManage(): boolean { return this.auth.has(P.beneficiaryManage); }
  get canReveal(): boolean { return this.auth.has(P.beneficiaryPii); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any>('me/beneficiaries', { search: this.search, pageSize: 500 }).subscribe(r => this.rows.set(r.items));
  }

  async register(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Register beneficiary', fields: beneficiaryFields(this.refs) }, '860px');
    if (v) this.api.post('me/beneficiaries', v).subscribe(() => this.load());
  }

  async reveal(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Reveal identifier', fields: [{ key: 'reason', label: 'Reason for access', type: 'textarea', required: true }] }, '520px');
    if (!v) return;
    this.api.post(`me/beneficiaries/${row.id}/reveal`, v).subscribe((r: any) => {
      alert(`Identifier: ${r.identifier}`);
      this.snack.open('Access has been logged in the audit trail.', 'OK', { duration: 4000 });
    });
  }

  async resolveDuplicate(row: any): Promise<void> {
    const v = await openForm(this.dialog, { title: 'Resolve potential duplicate', fields: [
      { key: 'isDuplicate', label: 'This is a confirmed duplicate', type: 'checkbox' },
      { key: 'note', label: 'Note', type: 'textarea', required: true, wide: true }
    ] }, '620px');
    if (v) this.api.post(`me/beneficiaries/${row.id}/duplicate-decision`, v).subscribe(() => this.load());
  }
}
