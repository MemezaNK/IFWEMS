import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ContractsService } from '../../core/contracts/contracts.service';
import { SuppliersService } from '../../core/suppliers/suppliers.service';
import { ContractDto } from '../../core/models/contract.models';
import { SupplierDto } from '../../core/models/supplier.models';

@Component({
  selector: 'app-contracts',
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
    MatProgressSpinnerModule,
    MatTooltipModule
  ],
  templateUrl: './contracts.component.html',
  styleUrl: './contracts.component.scss'
})
export class ContractsComponent implements OnInit {
  readonly contracts = signal<ContractDto[]>([]);
  readonly suppliers = signal<SupplierDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showCreateForm = signal(false);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['contractNumber', 'title', 'supplier', 'currentValue', 'utilisationPercentage', 'daysToExpiry', 'alertLevel'];

  readonly filteredContracts = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const all = this.contracts();
    if (!term) {
      return all;
    }
    return all.filter(
      (c) =>
        c.contractNumber.toLowerCase().includes(term) ||
        c.title.toLowerCase().includes(term) ||
        this.supplierName(c.supplierId).toLowerCase().includes(term)
    );
  });

  newContract = {
    contractNumber: '',
    title: '',
    supplierId: '',
    originalValue: 0,
    startDateUtc: '',
    expiryDateUtc: ''
  };

  constructor(
    private readonly contractsService: ContractsService,
    private readonly suppliersService: SuppliersService
  ) {}

  ngOnInit(): void {
    this.load();
    this.suppliersService.getAll().subscribe((suppliers) => this.suppliers.set(suppliers));
  }

  load(): void {
    this.isLoading.set(true);
    this.contractsService.getAll().subscribe({
      next: (contracts) => {
        this.contracts.set(contracts);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  toggleCreateForm(): void {
    this.showCreateForm.set(!this.showCreateForm());
  }

  supplierName(supplierId: string): string {
    return this.suppliers().find((s) => s.id === supplierId)?.name ?? supplierId;
  }

  alertSeverity(alertLevel: string | null): string {
    if (!alertLevel) return 'none';
    const normalised = alertLevel.toUpperCase();
    if (normalised.includes('EXPIR') || normalised.includes('BREACH') || normalised.includes('CRITICAL')) return 'RED';
    if (normalised.includes('WARN') || normalised.includes('APPROACH')) return 'AMBER';
    return 'GREEN';
  }

  createContract(): void {
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.contractsService
      .create({
        contractNumber: this.newContract.contractNumber,
        title: this.newContract.title,
        supplierId: this.newContract.supplierId,
        originalValue: this.newContract.originalValue,
        startDateUtc: this.newContract.startDateUtc,
        expiryDateUtc: this.newContract.expiryDateUtc
      })
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.newContract = { contractNumber: '', title: '', supplierId: '', originalValue: 0, startDateUtc: '', expiryDateUtc: '' };
          this.showCreateForm.set(false);
          this.load();
        },
        error: (err) => {
          this.isSaving.set(false);
          this.errorMessage.set(err?.error?.title ?? err?.error?.message ?? 'Failed to create contract.');
        }
      });
  }
}
