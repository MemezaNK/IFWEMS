import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ComplianceService } from '../../core/compliance/compliance.service';
import { TransactionCheckResult } from '../../core/models/compliance.models';

@Component({
  selector: 'app-compliance-check',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './compliance-check.component.html',
  styleUrl: './compliance-check.component.scss'
})
export class ComplianceCheckComponent {
  readonly result = signal<TransactionCheckResult | null>(null);
  readonly isChecking = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form;

  constructor(
    private readonly fb: FormBuilder,
    private readonly complianceService: ComplianceService
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
