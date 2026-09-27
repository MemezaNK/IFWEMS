import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { ApiService } from '../../core/api.service';
import { ReportTable } from '../../core/models';
import { ReportTableComponent } from '../../shared/report-table.component';

/** Runs a single catalogue report with optional filters and exports it (CSV/XLSX/PDF) (SRS §11). */
@Component({
  selector: 'teta-report-run',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatMenuModule, ReportTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>{{ code }}</h1>
        <button mat-stroked-button [matMenuTriggerFor]="exportMenu"><mat-icon>download</mat-icon> Export</button>
        <mat-menu #exportMenu="matMenu">
          <button mat-menu-item (click)="exportAs('csv')">CSV</button>
          <button mat-menu-item (click)="exportAs('xlsx')">Excel (.xlsx)</button>
          <button mat-menu-item (click)="exportAs('pdf')">PDF</button>
        </mat-menu>
      </div>
      <div class="toolbar-row">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 140px">
          <mat-label>Financial year</mat-label>
          <input matInput [(ngModel)]="financialYear" (ngModelChange)="load()" placeholder="2026/27" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 100px">
          <mat-label>Quarter</mat-label>
          <input matInput type="number" min="1" max="4" [(ngModel)]="quarter" (ngModelChange)="load()" />
        </mat-form-field>
      </div>
      @if (table(); as t) { <teta-report-table [table]="t" /> }
    </div>
  `
})
export class ReportRunComponent implements OnChanges {
  @Input() code!: string;
  private readonly api = inject(ApiService);
  readonly table = signal<ReportTable | null>(null);
  financialYear: string | null = null;
  quarter: number | null = null;

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.get<ReportTable>(`reports/run/${this.code}`, { financialYear: this.financialYear, quarter: this.quarter }).subscribe(t => this.table.set(t));
  }

  exportAs(format: string): void {
    this.api.download(`reports/run/${this.code}/export`, { financialYear: this.financialYear, quarter: this.quarter, format })
      .subscribe(r => ApiService.saveBlob(r, `${this.code}.${format}`));
  }
}
