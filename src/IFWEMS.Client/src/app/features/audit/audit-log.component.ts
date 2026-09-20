import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { AuditService } from '../../core/audit/audit.service';
import { AuditLogDto } from '../../core/models/audit.models';

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatExpansionModule
  ],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
  readonly logs = signal<AuditLogDto[]>([]);
  readonly totalCount = signal(0);
  readonly entityNames = signal<string[]>([]);
  readonly isLoading = signal(true);
  readonly displayedColumns = ['timestampUtc', 'username', 'action', 'entityName', 'entityId', 'ipAddress'];
  readonly expandedRowId = signal<number | null>(null);

  page = 1;
  pageSize = 25;
  filterEntityName = '';
  filterUsername = '';
  filterAction = '';

  constructor(private readonly auditService: AuditService) {}

  ngOnInit(): void {
    this.auditService.getEntityNames().subscribe((names) => this.entityNames.set(names));
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.auditService
      .query({
        entityName: this.filterEntityName || undefined,
        username: this.filterUsername || undefined,
        action: this.filterAction || undefined,
        page: this.page,
        pageSize: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.logs.set(result.items);
          this.totalCount.set(result.totalCount);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false)
      });
  }

  applyFilters(): void {
    this.page = 1;
    this.load();
  }

  clearFilters(): void {
    this.filterEntityName = '';
    this.filterUsername = '';
    this.filterAction = '';
    this.page = 1;
    this.load();
  }

  onPageChange(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  toggleRow(id: number): void {
    this.expandedRowId.set(this.expandedRowId() === id ? null : id);
  }

  hasDiff(log: AuditLogDto): boolean {
    return !!(log.beforeValuesJson || log.afterValuesJson);
  }
}
