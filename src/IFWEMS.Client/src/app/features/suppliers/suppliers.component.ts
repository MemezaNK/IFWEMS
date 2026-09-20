import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { SuppliersService } from '../../core/suppliers/suppliers.service';
import { SupplierDto } from '../../core/models/supplier.models';
import { SupplierRiskProfileDto } from '../../core/models/supplier-risk.models';

@Component({
  selector: 'app-suppliers',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatTooltipModule
  ],
  templateUrl: './suppliers.component.html',
  styleUrl: './suppliers.component.scss'
})
export class SuppliersComponent implements OnInit {
  readonly suppliers = signal<SupplierDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly riskProfile = signal<SupplierRiskProfileDto | null>(null);
  readonly showCreateForm = signal(false);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['supplierCode', 'name', 'registrationNumber', 'actions'];

  readonly filteredSuppliers = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const all = this.suppliers();
    if (!term) {
      return all;
    }
    return all.filter((s) => s.name.toLowerCase().includes(term) || s.supplierCode.toLowerCase().includes(term));
  });

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

  toggleCreateForm(): void {
    this.showCreateForm.set(!this.showCreateForm());
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
          this.showCreateForm.set(false);
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

  closeRiskProfile(): void {
    this.riskProfile.set(null);
  }
}
