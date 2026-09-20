import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ComplianceRulesService } from '../../core/compliance-rules/compliance-rules.service';
import { ComplianceRuleDto } from '../../core/models/compliance-rule.models';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-admin-rules',
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
    MatProgressSpinnerModule
  ],
  templateUrl: './admin-rules.component.html',
  styleUrl: './admin-rules.component.scss'
})
export class AdminRulesComponent implements OnInit {
  readonly rules = signal<ComplianceRuleDto[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showCreateForm = signal(false);
  readonly displayedColumns = ['code', 'name', 'version', 'status', 'effectiveFromUtc', 'actions'];

  newRule = {
    code: '',
    name: '',
    description: '',
    parametersJson: '{}',
    effectiveFromUtc: new Date().toISOString().slice(0, 10),
    effectiveToUtc: ''
  };

  constructor(
    private readonly rulesService: ComplianceRulesService,
    readonly authService: AuthService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.rulesService.getAll().subscribe({
      next: (rules) => {
        this.rules.set(rules);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  toggleCreateForm(): void {
    this.showCreateForm.set(!this.showCreateForm());
  }

  canApprove(): boolean {
    return this.authService.hasRole('ApprovingOfficial', 'SystemAdministrator');
  }

  canCreate(): boolean {
    return this.authService.hasRole('ComplianceOfficer', 'SystemAdministrator');
  }

  createRule(): void {
    this.errorMessage.set(null);
    this.isSaving.set(true);
    this.rulesService
      .create({
        code: this.newRule.code,
        name: this.newRule.name,
        description: this.newRule.description,
        parametersJson: this.newRule.parametersJson || '{}',
        effectiveFromUtc: this.newRule.effectiveFromUtc,
        effectiveToUtc: this.newRule.effectiveToUtc || null
      })
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.newRule = {
            code: '',
            name: '',
            description: '',
            parametersJson: '{}',
            effectiveFromUtc: new Date().toISOString().slice(0, 10),
            effectiveToUtc: ''
          };
          this.showCreateForm.set(false);
          this.load();
        },
        error: (err) => {
          this.isSaving.set(false);
          this.errorMessage.set(err?.error?.title ?? err?.error?.message ?? 'Failed to create rule.');
        }
      });
  }

  approve(rule: ComplianceRuleDto): void {
    this.rulesService.approve(rule.id).subscribe({
      next: () => this.load(),
      error: (err) => this.errorMessage.set(err?.error?.message ?? 'Unable to approve rule.')
    });
  }
}
