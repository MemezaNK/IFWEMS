import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportsService } from '../../core/reports/reports.service';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss'
})
export class ReportsComponent {
  readonly errorMessage = signal<string | null>(null);

  constructor(private readonly reportsService: ReportsService) {}

  downloadCases(): void {
    this.download(this.reportsService.downloadCasesRegister(), 'cases-register.csv');
  }

  downloadRecoveries(): void {
    this.download(this.reportsService.downloadRecoveriesRegister(), 'recoveries-register.csv');
  }

  downloadContracts(): void {
    this.download(this.reportsService.downloadContractsRegister(), 'contracts-register.csv');
  }

  private download(observable: { subscribe: (handlers: { next: (blob: Blob) => void; error: () => void }) => void }, fileName: string): void {
    this.errorMessage.set(null);
    observable.subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        link.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => this.errorMessage.set('Unable to generate the report. You may not have permission for this register.')
    });
  }
}
