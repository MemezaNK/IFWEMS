import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { ReportsService } from '../../core/reports/reports.service';
import { AuthService } from '../../core/auth/auth.service';

interface RegisterOption {
  key: string;
  title: string;
  description: string;
  icon: string;
}

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, FormsModule, MatCardModule, MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss'
})
export class ReportsComponent {
  readonly errorMessage = signal<string | null>(null);
  readonly downloadingKey = signal<string | null>(null);
  readonly otherUserId = signal<string>('');

  readonly registers: RegisterOption[] = [
    {
      key: 'cases',
      title: 'Irregular / Fruitless & Wasteful Expenditure Case Register',
      description: 'One row per case with status, amounts and key dates.',
      icon: 'folder_open'
    },
    {
      key: 'recoveries',
      title: 'Recovery Register',
      description: 'Recovery transactions, amounts and outstanding balances.',
      icon: 'account_balance_wallet'
    },
    {
      key: 'contracts',
      title: 'Contract Exception / Register',
      description: 'Contract value, utilisation and expiry across all suppliers.',
      icon: 'description'
    }
  ];

  constructor(
    private readonly reportsService: ReportsService,
    private readonly authService: AuthService
  ) {}

  get isAdmin(): boolean {
    return this.authService.hasRole('SystemAdministrator', 'ComplianceOfficer');
  }

  download(key: string): void {
    if (key === 'cases') {
      this.run(key, this.reportsService.downloadCasesRegister(), 'cases-register.csv');
    } else if (key === 'recoveries') {
      this.run(key, this.reportsService.downloadRecoveriesRegister(), 'recoveries-register.csv');
    } else if (key === 'contracts') {
      this.run(key, this.reportsService.downloadContractsRegister(), 'contracts-register.csv');
    }
  }

  downloadMyActivityReport(): void {
    this.run('my-activity', this.reportsService.downloadMyActivityReport(), 'my-activity-report.pdf');
  }

  downloadOtherUserActivityReport(): void {
    const userId = this.otherUserId().trim();
    if (!userId) {
      this.errorMessage.set('Enter a user ID to generate their activity report.');
      return;
    }
    this.run('other-activity', this.reportsService.downloadUserActivityReport(userId), 'user-activity-report.pdf');
  }

  downloadAllUsersActivityReport(): void {
    this.run('all-activity', this.reportsService.downloadAllUsersActivityReport(), 'user-activity-summary.pdf');
  }

  private run(key: string, observable: { subscribe: (handlers: { next: (blob: Blob) => void; error: () => void }) => void }, fileName: string): void {
    this.errorMessage.set(null);
    this.downloadingKey.set(key);
    observable.subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        link.click();
        window.URL.revokeObjectURL(url);
        this.downloadingKey.set(null);
      },
      error: () => {
        this.errorMessage.set('Unable to generate the report. You may not have permission for this register.');
        this.downloadingKey.set(null);
      }
    });
  }
}
