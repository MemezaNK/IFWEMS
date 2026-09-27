import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';

/** Validated bulk import of approved master/legacy data; every run (including validate-only) produces an error report (FR-ADM-009). */
@Component({
  selector: 'teta-admin-imports',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatCheckboxModule, MatFormFieldModule, MatSelectModule, DataTableComponent],
  template: `
    <div style="padding-top: 12px" class="grid">
      <div class="card">
        <h2 style="margin-top: 0">Run an import</h2>
        <div class="toolbar-row">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 220px">
            <mat-label>Import type</mat-label>
            <mat-select [(ngModel)]="importType">
              @for (t of templates(); track t.importType) { <mat-option [value]="t.importType">{{ t.importType }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-checkbox [(ngModel)]="validateOnly">Validate only (no rows saved)</mat-checkbox>
          <span class="spacer"></span>
          <input #file type="file" hidden accept=".csv" (change)="run(file.files); file.value = ''" />
          <button mat-flat-button color="primary" type="button" [disabled]="!importType" (click)="file.click()"><mat-icon>upload_file</mat-icon> Choose CSV…</button>
        </div>
        @if (currentTemplate(); as t) {
          <p class="small muted">Required columns: {{ t.required.join(', ') }}. All columns: {{ t.columns.join(', ') }}.</p>
        }
      </div>
      @if (lastResult(); as r) {
        <div class="card">
          <h2 style="margin-top: 0">{{ r.fileName }} – {{ r.status }} @if (r.validateOnly) { (validate only) }</h2>
          <p class="small muted">{{ r.succeededRows }} of {{ r.totalRows }} row(s) succeeded, {{ r.failedRows }} failed.</p>
          @if (r.errors.length) {
            <teta-data-table [columns]="errorColumns" [rows]="r.errors" emptyText="No errors." [paginate]="false" />
          }
        </div>
      }
      <div class="card">
        <h2 style="margin-top: 0">Import history</h2>
        <teta-data-table [columns]="jobColumns" [rows]="jobs()" (rowClick)="viewJob($event)" emptyText="No imports have been run yet." />
      </div>
    </div>
  `
})
export class AdminImportsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly snack = inject(MatSnackBar);
  readonly templates = signal<any[]>([]);
  readonly jobs = signal<any[]>([]);
  readonly lastResult = signal<any | null>(null);
  importType = '';
  validateOnly = true;

  jobColumns: Column[] = [
    { key: 'importType', label: 'Type' }, { key: 'fileName', label: 'File' }, { key: 'validateOnly', label: 'Validate only', type: 'bool' },
    { key: 'totalRows', label: 'Rows', type: 'number' }, { key: 'succeededRows', label: 'Succeeded', type: 'number' },
    { key: 'failedRows', label: 'Failed', type: 'number' }, { key: 'status', label: 'Status', type: 'status' },
    { key: 'createdAtUtc', label: 'Run at', type: 'datetime' }, { key: 'createdBy', label: 'Run by' }
  ];
  errorColumns: Column[] = [{ key: 'row', label: 'Row', type: 'number' }, { key: 'field', label: 'Field' }, { key: 'message', label: 'Message' }];

  currentTemplate(): any | undefined {
    return this.templates().find(t => t.importType === this.importType);
  }

  ngOnInit(): void {
    this.api.get<any[]>('admin/imports/templates').subscribe(t => {
      this.templates.set(t);
      if (!this.importType && t.length) this.importType = t[0].importType;
    });
    this.loadJobs();
  }

  loadJobs(): void {
    this.api.get<any[]>('admin/imports').subscribe(r => this.jobs.set(r));
  }

  run(files: FileList | null): void {
    const file = files?.[0];
    if (!file || !this.importType) return;
    const form = new FormData();
    form.append('file', file, file.name);
    this.api.upload<any>(`admin/imports/${this.importType}`, form, { validateOnly: this.validateOnly }).subscribe(r => {
      this.lastResult.set(r);
      this.snack.open(r.status === 'Succeeded' ? 'Import completed.' : `Import ${r.status.toLowerCase()}.`, 'OK', { duration: 4000 });
      this.loadJobs();
    });
  }

  viewJob(row: any): void {
    this.api.get<any>(`admin/imports/${row.id}`).subscribe(r => this.lastResult.set(r));
  }
}
