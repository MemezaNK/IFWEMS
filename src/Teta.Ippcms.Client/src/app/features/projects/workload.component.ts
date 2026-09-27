import { Component, OnInit, inject, signal } from '@angular/core';
import { Column, DataTableComponent } from '../../shared/data-table.component';
import { ApiService } from '../../core/api.service';

/** Cross-project resource allocation and over-allocation warnings (FR-EXE-006). */
@Component({
  selector: 'teta-workload',
  standalone: true,
  imports: [DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>Resource workload</h1></div>
      <teta-data-table [columns]="columns" [rows]="rows()" emptyText="No resources assigned to any project." exportName="workload" />
    </div>
  `
})
export class WorkloadComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly rows = signal<any[]>([]);

  columns: Column[] = [
    { key: 'resourceName', label: 'Resource' }, { key: 'isExternal', label: 'External', type: 'bool' }, { key: 'assignments', label: 'Assignments', type: 'number' },
    { key: 'totalAllocation', label: 'Total allocation %', type: 'percent' }, { key: 'overAllocated', label: 'Over-allocated', type: 'bool' },
    { key: 'projects', label: 'Projects', value: r => (r.projects as string[]).join(', ') }
  ];

  ngOnInit(): void {
    this.api.get<any[]>('resources/workload').subscribe(r => this.rows.set(r));
  }
}
