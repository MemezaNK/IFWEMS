import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ContractsService } from '../../core/contracts/contracts.service';
import { SuppliersService } from '../../core/suppliers/suppliers.service';
import { ContractDto } from '../../core/models/contract.models';
import { SupplierDto } from '../../core/models/supplier.models';

@Component({
  selector: 'app-contracts',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './contracts.component.html',
  styleUrl: './contracts.component.scss'
})
export class ContractsComponent implements OnInit {
  readonly contracts = signal<ContractDto[]>([]);
  readonly suppliers = signal<SupplierDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);

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

  supplierName(supplierId: string): string {
    return this.suppliers().find((s) => s.id === supplierId)?.name ?? supplierId;
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
          this.load();
        },
        error: (err) => {
          this.isSaving.set(false);
          this.errorMessage.set(err?.error?.title ?? err?.error?.message ?? 'Failed to create contract.');
        }
      });
  }
}
