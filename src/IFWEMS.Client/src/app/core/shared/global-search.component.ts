import { Component, ElementRef, HostListener, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin } from 'rxjs';
import { CaseService } from '../cases/case.service';
import { SuppliersService } from '../suppliers/suppliers.service';
import { ContractsService } from '../contracts/contracts.service';
import { CaseDto } from '../models/case.models';
import { SupplierDto } from '../models/supplier.models';
import { ContractDto } from '../models/contract.models';

interface SearchResult {
  kind: 'Case' | 'Supplier' | 'Contract';
  icon: string;
  title: string;
  subtitle: string;
  link: string[];
}

/**
 * Lightweight cross-entity "global search" (Section 25) over cases, suppliers and
 * contracts. Data is fetched once on first use from the existing list endpoints and
 * matched client-side; this keeps the shell simple and avoids a new server-side search
 * endpoint while still letting a user jump straight to a case/supplier/contract by
 * number, title or name from anywhere in the app.
 */
@Component({
  selector: 'app-global-search',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './global-search.component.html',
  styleUrl: './global-search.component.scss'
})
export class GlobalSearchComponent {
  private readonly caseService = inject(CaseService);
  private readonly suppliersService = inject(SuppliersService);
  private readonly contractsService = inject(ContractsService);
  private readonly elementRef = inject(ElementRef);

  readonly query = signal('');
  readonly isOpen = signal(false);
  readonly isLoading = signal(false);

  private cases: CaseDto[] = [];
  private suppliers: SupplierDto[] = [];
  private contracts: ContractDto[] = [];
  private dataLoaded = false;

  readonly results = computed<SearchResult[]>(() => {
    const term = this.query().trim().toLowerCase();
    if (!term) {
      return [];
    }

    const caseResults: SearchResult[] = this.cases
      .filter((c) => c.caseNumber.toLowerCase().includes(term) || c.title.toLowerCase().includes(term))
      .slice(0, 5)
      .map((c) => ({ kind: 'Case', icon: 'folder_open', title: c.caseNumber, subtitle: c.title, link: ['/cases', c.id] }));

    const supplierResults: SearchResult[] = this.suppliers
      .filter((s) => s.name.toLowerCase().includes(term) || s.supplierCode.toLowerCase().includes(term))
      .slice(0, 5)
      .map((s) => ({ kind: 'Supplier', icon: 'business', title: s.name, subtitle: s.supplierCode, link: ['/suppliers'] }));

    const contractResults: SearchResult[] = this.contracts
      .filter((c) => c.contractNumber.toLowerCase().includes(term) || c.title.toLowerCase().includes(term))
      .slice(0, 5)
      .map((c) => ({ kind: 'Contract', icon: 'description', title: c.contractNumber, subtitle: c.title, link: ['/contracts'] }));

    return [...caseResults, ...supplierResults, ...contractResults];
  });

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.isOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.isOpen.set(false);
  }

  onFocus(): void {
    this.isOpen.set(true);
    this.ensureDataLoaded();
  }

  onInput(): void {
    this.isOpen.set(true);
    this.ensureDataLoaded();
  }

  select(): void {
    this.query.set('');
    this.isOpen.set(false);
  }

  private ensureDataLoaded(): void {
    if (this.dataLoaded || this.isLoading()) {
      return;
    }
    this.isLoading.set(true);
    forkJoin({
      cases: this.caseService.getAll(),
      suppliers: this.suppliersService.getAll(),
      contracts: this.contractsService.getAll()
    }).subscribe({
      next: ({ cases, suppliers, contracts }) => {
        this.cases = cases;
        this.suppliers = suppliers;
        this.contracts = contracts;
        this.dataLoaded = true;
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
