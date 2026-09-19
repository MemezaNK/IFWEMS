import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CaseService } from '../../core/cases/case.service';
import { CaseDto, CaseType } from '../../core/models/case.models';
import { AdminService } from '../../core/admin/admin.service';
import { OrgUnitDto } from '../../core/models/admin.models';

@Component({
  selector: 'app-case-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './case-list.component.html',
  styleUrl: './case-list.component.scss'
})
export class CaseListComponent implements OnInit {
  readonly cases = signal<CaseDto[]>([]);
  readonly orgUnits = signal<OrgUnitDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly caseTypes: CaseType[] = [
    'IrregularExpenditure',
    'FruitlessWasteful',
    'UnauthorisedExpenditure',
    'PotentialNonCompliance'
  ];

  newCase = {
    caseType: '' as CaseType | '',
    orgUnitId: '',
    title: '',
    description: '',
    amountInvolved: null as number | null
  };

  constructor(
    private readonly caseService: CaseService,
    private readonly adminService: AdminService
  ) {}

  ngOnInit(): void {
    this.load();
    this.adminService.getOrgUnits().subscribe((orgUnits) => this.orgUnits.set(orgUnits));
  }

  load(): void {
    this.isLoading.set(true);
    this.caseService.getAll().subscribe({
      next: (cases) => {
        this.cases.set(cases);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  createCase(): void {
    if (!this.newCase.caseType) {
      return;
    }
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.caseService
      .create({
        caseType: this.newCase.caseType,
        orgUnitId: this.newCase.orgUnitId,
        title: this.newCase.title,
        description: this.newCase.description || null,
        amountInvolved: this.newCase.amountInvolved
      })
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.newCase = { caseType: '', orgUnitId: '', title: '', description: '', amountInvolved: null };
          this.load();
        },
        error: (err) => {
          this.isSaving.set(false);
          this.errorMessage.set(err?.error?.title ?? err?.error?.message ?? 'Failed to create case.');
        }
      });
  }
}
