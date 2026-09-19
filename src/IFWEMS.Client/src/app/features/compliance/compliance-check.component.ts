import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ComplianceService } from '../../core/compliance/compliance.service';
import { TransactionCheckResult } from '../../core/models/compliance.models';
import { AdminService } from '../../core/admin/admin.service';
import { SuppliersService } from '../../core/suppliers/suppliers.service';
import { ContractsService } from '../../core/contracts/contracts.service';
import { OrgUnitDto } from '../../core/models/admin.models';
import { SupplierDto } from '../../core/models/supplier.models';
import { ContractDto } from '../../core/models/contract.models';

@Component({
  selector: 'app-compliance-check',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './compliance-check.component.html',
  styleUrl: './compliance-check.component.scss'
})
export class ComplianceCheckComponent implements OnInit {
  readonly result = signal<TransactionCheckResult | null>(null);
  readonly isChecking = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly orgUnits = signal<OrgUnitDto[]>([]);
  readonly suppliers = signal<SupplierDto[]>([]);
  readonly contracts = signal<ContractDto[]>([]);

  readonly form;

  constructor(
    private readonly fb: FormBuilder,
    private readonly complianceService: ComplianceService,
    private readonly adminService: AdminService,
    private readonly suppliersService: SuppliersService,
    private readonly contractsService: ContractsService
  ) {
    this.form = this.fb.nonNullable.group({
      transactionReference: ['', Validators.required],
      orgUnitId: ['', Validators.required],
      supplierId: [''],
      contractId: [''],
      amount: [0, [Validators.required, Validators.min(0.01)]],
      invoiceReference: [''],
      purchaseOrderDateUtc: [''],
      commodityCode: ['']
    });
  }

  ngOnInit(): void {
    this.adminService.getOrgUnits().subscribe((orgUnits) => this.orgUnits.set(orgUnits));
    this.suppliersService.getAll().subscribe((suppliers) => this.suppliers.set(suppliers));
    this.contractsService.getAll().subscribe((contracts) => this.contracts.set(contracts));
  }

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.isChecking.set(true);
    this.errorMessage.set(null);
    this.result.set(null);

    const raw = this.form.getRawValue();
    this.complianceService
      .check({
        transactionReference: raw.transactionReference,
        orgUnitId: raw.orgUnitId,
        supplierId: raw.supplierId || null,
        contractId: raw.contractId || null,
        amount: raw.amount,
        invoiceReference: raw.invoiceReference || null,
        purchaseOrderDateUtc: raw.purchaseOrderDateUtc || null,
        commodityCode: raw.commodityCode || null
      })
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.isChecking.set(false);
        },
        error: () => {
          this.errorMessage.set('Unable to complete the compliance check.');
          this.isChecking.set(false);
        }
      });
  }
}
