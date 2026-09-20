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
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SlaService } from '../../core/sla/sla.service';
import { OverdueCaseDto, SlaPolicyDto } from '../../core/models/sla.models';
import { EnumLabelPipe } from '../../core/shared/enum-label.pipe';

@Component({
  selector: 'app-admin-sla',
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
    EnumLabelPipe
  ],
  templateUrl: './admin-sla.component.html',
  styleUrl: './admin-sla.component.scss'
})
export class AdminSlaComponent implements OnInit {
  readonly policies = signal<SlaPolicyDto[]>([]);
  readonly overdueCases = signal<OverdueCaseDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showCreateForm = signal(false);
  readonly policyColumns = ['caseType', 'status', 'maxDurationHours', 'isActive', 'effectiveFromUtc'];
  readonly overdueColumns = ['caseNumber', 'caseType', 'status', 'hoursInStatus', 'slaMaxHours', 'hoursOverdue'];

  readonly caseTypes = ['IrregularExpenditure', 'FruitlessWasteful', 'UnauthorisedExpenditure', 'PotentialNonCompliance'];
  readonly caseStatuses = [
    'Draft',
    'UnderAssessment',
    'UnderInvestigation',
    'Determined',
    'RecoveryInProgress',
    'ConsequenceManagement',
    'CorrectiveActionPending'
  ];

  newPolicy = {
    caseType: '',
    status: '',
    maxDurationHours: 72,
    effectiveFromUtc: new Date().toISOString().slice(0, 10),
    effectiveToUtc: ''
  };

  constructor(private readonly slaService: SlaService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.slaService.getPolicies().subscribe({
      next: (policies) => {
        this.policies.set(policies);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
    this.slaService.getOverdueCases().subscribe((cases) => this.overdueCases.set(cases));
  }

  toggleCreateForm(): void {
    this.showCreateForm.set(!this.showCreateForm());
  }

  createPolicy(): void {
    if (!this.newPolicy.caseType || !this.newPolicy.status) {
      return;
    }
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.slaService
      .createPolicy({
        caseType: this.newPolicy.caseType,
        status: this.newPolicy.status,
        maxDurationHours: this.newPolicy.maxDurationHours,
        effectiveFromUtc: this.newPolicy.effectiveFromUtc,
        effectiveToUtc: this.newPolicy.effectiveToUtc || null
      })
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.newPolicy = {
            caseType: '',
            status: '',
            maxDurationHours: 72,
            effectiveFromUtc: new Date().toISOString().slice(0, 10),
            effectiveToUtc: ''
          };
          this.showCreateForm.set(false);
          this.load();
        },
        error: (err) => {
          this.isSaving.set(false);
          this.errorMessage.set(err?.error?.title ?? err?.error?.message ?? 'Failed to create SLA policy.');
        }
      });
  }
}
