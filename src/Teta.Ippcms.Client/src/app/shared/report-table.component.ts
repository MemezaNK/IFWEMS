import { Component, Input, OnChanges } from '@angular/core';
import { ReportTable } from '../core/models';
import { Column, DataTableComponent } from './data-table.component';

/** Renders a server ReportTable (column list + row arrays) through the common data table. */
@Component({
  selector: 'teta-report-table',
  standalone: true,
  imports: [DataTableComponent],
  template: `<teta-data-table [columns]="columns" [rows]="rows" [exportName]="table?.code ?? 'report'" [pageSize]="50" />`
})
export class ReportTableComponent implements OnChanges {
  @Input() table: ReportTable | null = null;
  columns: Column[] = [];
  rows: any[] = [];

  ngOnChanges(): void {
    const t = this.table;
    if (!t) { this.columns = []; this.rows = []; return; }
    this.columns = t.columns.map((label, i) => ({
      key: 'c' + i,
      label,
      type: this.typeFor(label, t.rows.map(r => r[i])),
      value: (row: any) => row['c' + i]
    }));
    this.rows = t.rows.map(r => Object.fromEntries(r.map((v, i) => ['c' + i, v])));
  }

  private typeFor(label: string, values: unknown[]): Column['type'] {
    const sample = values.find(v => v !== null && v !== undefined);
    if (/status|health|rating|band|outcome/i.test(label) && typeof sample === 'string') return 'status';
    if (typeof sample === 'boolean') return 'bool';
    if (typeof sample === 'number') {
      return /value|budget|amount|committed|actual|accrued|available|invoiced|paid|variations|completion|variance$|vat|exposure/i.test(label) && !/%|days|score|count/i.test(label)
        ? 'money' : 'number';
    }
    if (typeof sample === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(sample)) return 'datetime';
    return 'text';
  }
}
