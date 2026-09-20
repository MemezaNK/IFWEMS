import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ComplianceService } from '../../core/compliance/compliance.service';
import { AdminService } from '../../core/admin/admin.service';
import { SuppliersService } from '../../core/suppliers/suppliers.service';
import { DocumentsService } from '../../core/documents/documents.service';
import { OrgUnitDto } from '../../core/models/admin.models';
import { SupplierDto } from '../../core/models/supplier.models';
import { DocumentDto } from '../../core/models/document.models';

@Component({
  selector: 'app-emergency-override',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './emergency-override.component.html',
  styleUrl: './emergency-override.component.scss'
})
export class EmergencyOverrideComponent implements OnInit {
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly transactionId = signal<string | null>(null);
  readonly orgUnits = signal<OrgUnitDto[]>([]);
  readonly suppliers = signal<SupplierDto[]>([]);
  readonly documents = signal<DocumentDto[]>([]);

  readonly form;

  constructor(
    private readonly fb: FormBuilder,
    private readonly complianceService: ComplianceService,
    private readonly adminService: AdminService,
    private readonly suppliersService: SuppliersService,
    private readonly documentsService: DocumentsService
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

  ngOnInit(): void {
    this.adminService.getOrgUnits().subscribe((orgUnits) => this.orgUnits.set(orgUnits));
    this.suppliersService.getAll().subscribe((suppliers) => this.suppliers.set(suppliers));
    this.documentsService.getAll().subscribe((documents) => this.documents.set(documents));
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
