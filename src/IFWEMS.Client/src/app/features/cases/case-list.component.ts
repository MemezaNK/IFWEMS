import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CaseService } from '../../core/cases/case.service';
import { CaseDto, CaseType } from '../../core/models/case.models';
import { AdminService } from '../../core/admin/admin.service';
import { OrgUnitDto } from '../../core/models/admin.models';
import { EnumLabelPipe } from '../../core/shared/enum-label.pipe';

@Component({
  selector: 'app-case-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    EnumLabelPipe
  ],
  templateUrl: './case-list.component.html',
  styleUrl: './case-list.component.scss'
})
export class CaseListComponent implements OnInit {
  readonly cases = signal<CaseDto[]>([]);
  readonly orgUnits = signal<OrgUnitDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showCreateForm = signal(false);
  readonly searchTerm = signal('');
  readonly displayedColumns = ['caseNumber', 'caseType', 'status', 'title', 'recoverableAmount', 'recoveredAmount', 'actions'];

  readonly filteredCases = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const all = this.cases();
    if (!term) {
      return all;
    }
    return all.filter(
      (c) =>
        c.caseNumber.toLowerCase().includes(term) ||
        c.title.toLowerCase().includes(term) ||
        c.caseType.toLowerCase().includes(term) ||
        c.status.toLowerCase().includes(term)
    );
  });

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

  toggleCreateForm(): void {
    this.showCreateForm.set(!this.showCreateForm());
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
          this.showCreateForm.set(false);
          this.load();
        },
        error: (err) => {
          this.isSaving.set(false);
          this.errorMessage.set(err?.error?.title ?? err?.error?.message ?? 'Failed to create case.');
        }
      });
  }
}
