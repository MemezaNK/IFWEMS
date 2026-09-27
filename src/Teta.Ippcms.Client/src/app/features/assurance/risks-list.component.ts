import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { ReferenceService } from '../../core/reference.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { openForm } from '../../shared/form-dialog.component';
import { riskFields } from './assurance-forms';

/** Risk register with inherent/residual ratings, linked to projects, contracts or programmes (FR-RSK-001..003). */
@Component({
  selector: 'teta-risks-list',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Risk register</h1>
        @if (canManage) { <button mat-flat-button color="primary" (click)="add()"><mat-icon>add</mat-icon> New risk</button> }
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" (rowClick)="open($event)" emptyText="No risks recorded." exportName="risks" />
    </div>
  `
})
export class RisksListComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly refs = inject(ReferenceService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  readonly rows = signal<any[]>([]);

  columns: Column[] = [
    { key: 'number', label: 'Number' }, { key: 'title', label: 'Title' }, { key: 'parentType', label: 'Parent' }, { key: 'projectReference', label: 'Project' },
    { key: 'category', label: 'Category' }, { key: 'inherentRating', label: 'Inherent', type: 'status' }, { key: 'residualRating', label: 'Residual', type: 'status' },
    { key: 'ownerName', label: 'Owner' }, { key: 'reviewDate', label: 'Review date', type: 'date' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'reviewOverdue', label: 'Review overdue', type: 'bool' }, { key: 'openTreatments', label: 'Open treatments', type: 'number' }
  ];

  get canManage(): boolean { return this.auth.has(P.riskManage); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('assurance/risks', { openOnly: false }).subscribe(r => this.rows.set(r));
  }

  open(row: any): void {
    this.router.navigate(['/assurance/risks', row.id]);
  }

  async add(): Promise<void> {
    const v = await openForm(this.dialog, { title: 'New risk', fields: riskFields(this.refs) }, '860px');
    if (v) this.api.post('assurance/risks', v).subscribe((r: any) => this.router.navigate(['/assurance/risks', r.id]));
  }
}
