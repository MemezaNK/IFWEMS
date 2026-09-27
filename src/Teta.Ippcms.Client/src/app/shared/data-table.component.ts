import { CurrencyPipe, DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { AfterViewInit, Component, EventEmitter, Input, OnChanges, Output, TemplateRef, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { StatusChipComponent } from './status-chip.component';

export type ColumnType = 'text' | 'date' | 'datetime' | 'money' | 'number' | 'percent' | 'status' | 'bool';

export interface Column {
  key: string;
  label: string;
  type?: ColumnType;
  /** Optional value accessor for nested or computed values. */
  value?: (row: any) => unknown;
}

/**
 * Reusable table: sorting, text filter, pagination and CSV export of the visible data (SRS §13:
 * "Tables shall support sorting, filtering, pagination and authorised export").
 */
@Component({
  selector: 'teta-data-table',
  standalone: true,
  imports: [MatTableModule, MatSortModule, MatPaginatorModule, MatFormFieldModule, MatInputModule, MatIconModule, MatButtonModule,
    MatTooltipModule, FormsModule, DatePipe, CurrencyPipe, DecimalPipe, NgTemplateOutlet, StatusChipComponent],
  template: `
    @if (filterable || exportable) {
      <div class="toolbar-row">
        @if (filterable) {
          <mat-form-field appearance="outline" subscriptSizing="dynamic" style="min-width: 260px">
            <mat-label>Filter</mat-label>
            <input matInput [(ngModel)]="filter" (ngModelChange)="applyFilter()" placeholder="Type to filter" />
            <mat-icon matSuffix>search</mat-icon>
          </mat-form-field>
        }
        <span class="spacer"></span>
        @if (exportable) {
          <button mat-stroked-button type="button" (click)="exportCsv()" matTooltip="Download the rows shown as CSV">
            <mat-icon>download</mat-icon> CSV
          </button>
        }
      </div>
    }
    <div class="table-wrap">
      <table mat-table [dataSource]="source" matSort>
        @for (col of columns; track col.key) {
          <ng-container [matColumnDef]="col.key">
            <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ col.label }}</th>
            <td mat-cell *matCellDef="let row" [class.right]="col.type === 'money' || col.type === 'number' || col.type === 'percent'">
              @switch (col.type) {
                @case ('date') { {{ cell(row, col) | date: 'yyyy-MM-dd' }} }
                @case ('datetime') { {{ cell(row, col) | date: 'yyyy-MM-dd HH:mm' }} }
                @case ('money') { {{ cell(row, col) | currency: 'ZAR' : 'R ' : '1.2-2' }} }
                @case ('number') { {{ cell(row, col) | number }} }
                @case ('percent') { {{ cell(row, col) | number: '1.0-1' }}% }
                @case ('status') { <teta-status [value]="cell(row, col)" /> }
                @case ('bool') { {{ cell(row, col) === true ? 'Yes' : cell(row, col) === false ? 'No' : '' }} }
                @default { {{ cell(row, col) }} }
              }
            </td>
          </ng-container>
        }
        @if (actions) {
          <ng-container matColumnDef="__actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let row" class="right" (click)="$event.stopPropagation()">
              <ng-container *ngTemplateOutlet="actions; context: { $implicit: row }" />
            </td>
          </ng-container>
        }
        <tr mat-header-row *matHeaderRowDef="displayed"></tr>
        <tr mat-row *matRowDef="let row; columns: displayed" [class.clickable]="rowClick.observed" (click)="rowClick.emit(row)"></tr>
      </table>
      @if (!source.filteredData.length) {
        <div class="empty">{{ emptyText }}</div>
      }
    </div>
    @if (paginate) {
      <mat-paginator [pageSizeOptions]="[10, 25, 50, 100]" [pageSize]="pageSize" showFirstLastButtons />
    }
  `
})
export class DataTableComponent implements OnChanges, AfterViewInit {
  @Input() columns: Column[] = [];
  @Input() rows: any[] | null = [];
  @Input() actions?: TemplateRef<unknown>;
  @Input() filterable = true;
  @Input() exportable = true;
  @Input() paginate = true;
  @Input() pageSize = 25;
  @Input() emptyText = 'No records.';
  @Input() exportName = 'export';
  @Output() rowClick = new EventEmitter<any>();

  @ViewChild(MatSort) sort?: MatSort;
  @ViewChild(MatPaginator) paginator?: MatPaginator;

  source = new MatTableDataSource<any>([]);
  filter = '';
  displayed: string[] = [];

  ngOnChanges(): void {
    this.source.data = this.rows ?? [];
    this.displayed = [...this.columns.map(c => c.key), ...(this.actions ? ['__actions'] : [])];
    this.source.sortingDataAccessor = (row, key) => {
      const col = this.columns.find(c => c.key === key);
      const v = col ? this.cell(row, col) : row[key];
      return typeof v === 'number' ? v : String(v ?? '').toLowerCase();
    };
    this.source.filterPredicate = (row, filter) =>
      this.columns.some(c => String(this.cell(row, c) ?? '').toLowerCase().includes(filter));
  }

  ngAfterViewInit(): void {
    if (this.sort) this.source.sort = this.sort;
    if (this.paginator) this.source.paginator = this.paginator;
  }

  cell(row: any, col: Column): any {
    return col.value ? col.value(row) : row?.[col.key];
  }

  applyFilter(): void {
    this.source.filter = this.filter.trim().toLowerCase();
  }

  exportCsv(): void {
    const escape = (v: unknown) => {
      let s = v === null || v === undefined ? '' : String(v);
      if (/^[=+\-@]/.test(s)) s = "'" + s;
      return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
    };
    const lines = [this.columns.map(c => escape(c.label)).join(',')];
    for (const row of this.source.filteredData) lines.push(this.columns.map(c => escape(this.cell(row, c))).join(','));
    const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `${this.exportName}.csv`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  }
}
