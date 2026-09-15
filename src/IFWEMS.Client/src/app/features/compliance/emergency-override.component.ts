import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ComplianceService } from '../../core/compliance/compliance.service';

@Component({
  selector: 'app-emergency-override',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './emergency-override.component.html',
  styleUrl: './emergency-override.component.scss'
})
export class EmergencyOverrideComponent {
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly transactionId = signal<string | null>(null);

  readonly form;

  constructor(
    private readonly fb: FormBuilder,
    private readonly complianceService: ComplianceService
  ) {
    this.form = this.fb.nonNullable.group({
      transactionReference: ['', Validators.required],
      orgUnitId: ['', Validators.required],
      supplierId: [''],
      amount: [0, [Validators.required, Validators.min(0.01)]],
      emergencyCategory: ['', Validators.required],
      reason: ['', Validators.required],
      evidenceDocumentId: ['', Validators.required],
      commodityCode: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.transactionId.set(null);

    const raw = this.form.getRawValue();
    this.complianceService
      .override({
        transactionReference: raw.transactionReference,
        orgUnitId: raw.orgUnitId,
        supplierId: raw.supplierId || null,
        amount: raw.amount,
        emergencyCategory: raw.emergencyCategory,
        reason: raw.reason,
        evidenceDocumentId: raw.evidenceDocumentId,
        commodityCode: raw.commodityCode || null
      })
      .subscribe({
        next: (response) => {
          this.transactionId.set(response.transactionId);
          this.isSubmitting.set(false);
          this.form.reset({ amount: 0 });
        },
        error: () => {
          this.errorMessage.set('Unable to capture the emergency override.');
          this.isSubmitting.set(false);
        }
      });
  }
}
