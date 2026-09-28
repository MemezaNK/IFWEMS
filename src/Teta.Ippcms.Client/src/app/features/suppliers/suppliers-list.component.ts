import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { supplierFields } from './supplier-forms';

/** Supplier / implementing-partner master register with controlled duplicate detection (FR-SCM-018). */
@Component({
  selector: 'teta-suppliers-list',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, FormsModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Suppliers</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> New supplier</button> }
      </div>
      <div class="toolbar-row">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="min-width: 260px">
          <mat-label>Search</mat-label>
          <input matInput [(ngModel)]="search" (ngModelChange)="load()" placeholder="Name, registration or CSD number" />
        </mat-form-field>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="open($event)" emptyText="No suppliers found." exportName="suppliers" [filterable]="false" />
    </div>
  `
})
export class SuppliersListComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly router = inject(Router);
  readonly rows = signal<any[]>([]);
  search = '';

  columns: Column[] = [
    { key: 'supplierNumber', label: 'Number' }, { key: 'legalName', label: 'Legal name' }, { key: 'tradingName', label: 'Trading name' },
    { key: 'registrationNumber', label: 'Registration no.' }, { key: 'bbbeeLevel', label: 'B-BBEE', type: 'number' },
    { key: 'status', label: 'Status', type: 'status' }, { key: 'isImplementingPartner', label: 'Implementing partner', type: 'bool' },
    { key: 'csdVerified', label: 'CSD verified', type: 'bool' }, { key: 'taxClearanceExpiry', label: 'Tax clearance expiry', type: 'date' }
  ];

  get canManage(): boolean { return this.auth.has(P.supplierManage); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any>('suppliers', { search: this.search, pageSize: 500 }).subscribe(r => this.rows.set(r.items));
  }

  open(row: any): void {
    this.router.navigate(['/suppliers', row.id]);
  }

  async add(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New supplier', fields: supplierFields() }, '860px');
    if (v) await this.saveWithDuplicateCheck(undefined, v);
  }

  private async saveWithDuplicateCheck(id: string | undefined, request: any): Promise<void> {
    const duplicates = await new Promise<any[]>(r => this.api.post<any[]>('suppliers/duplicates', request, { excludeId: id }).subscribe(r));
    if (duplicates.length) {
      const list = duplicates.map(d => `${d.legalName} (${d.supplierNumber}) – ${d.reason}`).join('\n');
      if (!confirm(`Possible duplicate supplier(s) found:\n\n${list}\n\nSave anyway?`)) return;
      request = { ...request, confirmNotDuplicate: true };
    }
    const call = id ? this.api.put(`suppliers/${id}`, request) : this.api.post('suppliers', request);
    call.subscribe(() => {
      this.snack.open('Saved.', 'OK', { duration: 3000 });
      this.load();
    });
  }
}
