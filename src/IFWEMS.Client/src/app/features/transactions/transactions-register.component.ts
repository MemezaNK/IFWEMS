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
import { TransactionsService } from '../../core/transactions/transactions.service';
import { TransactionDto } from '../../core/models/transaction.models';

@Component({
  selector: 'app-transactions-register',
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
    MatProgressSpinnerModule
  ],
  templateUrl: './transactions-register.component.html',
  styleUrl: './transactions-register.component.scss'
})
export class TransactionsRegisterComponent implements OnInit {
  readonly transactions = signal<TransactionDto[]>([]);
  readonly totalCount = signal(0);
  readonly isLoading = signal(true);
  readonly displayedColumns = ['createdAtUtc', 'transactionReference', 'supplierName', 'amount', 'riskRating', 'recommendedAction', 'failedRuleCodes', 'source'];

  page = 1;
  pageSize = 25;
  filterRiskRating = '';
  filterSearch = '';

  constructor(private readonly transactionsService: TransactionsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.transactionsService
      .query({
        riskRating: this.filterRiskRating || undefined,
        search: this.filterSearch || undefined,
        page: this.page,
        pageSize: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.transactions.set(result.items);
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
    this.filterRiskRating = '';
    this.filterSearch = '';
    this.page = 1;
    this.load();
  }

  onPageChange(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }
}
