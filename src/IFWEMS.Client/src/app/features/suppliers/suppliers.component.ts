import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SuppliersService } from '../../core/suppliers/suppliers.service';
import { SupplierDto } from '../../core/models/supplier.models';
import { SupplierRiskProfileDto } from '../../core/models/supplier-risk.models';

@Component({
  selector: 'app-suppliers',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './suppliers.component.html',
  styleUrl: './suppliers.component.scss'
})
export class SuppliersComponent implements OnInit {
  readonly suppliers = signal<SupplierDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly riskProfile = signal<SupplierRiskProfileDto | null>(null);

  newSupplier = { supplierCode: '', name: '', registrationNumber: '' };

  constructor(private readonly suppliersService: SuppliersService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.suppliersService.getAll().subscribe({
      next: (suppliers) => {
        this.suppliers.set(suppliers);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  createSupplier(): void {
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.suppliersService
      .create({
        supplierCode: this.newSupplier.supplierCode,
        name: this.newSupplier.name,
        registrationNumber: this.newSupplier.registrationNumber || null
      })
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.newSupplier = { supplierCode: '', name: '', registrationNumber: '' };
          this.load();
        },
        error: (err) => {
          this.isSaving.set(false);
          this.errorMessage.set(err?.error?.title ?? err?.error?.message ?? 'Failed to create supplier.');
        }
      });
  }

  viewRiskProfile(supplierId: string): void {
    this.riskProfile.set(null);
    this.suppliersService.getRiskProfile(supplierId).subscribe((profile) => this.riskProfile.set(profile));
  }
}
