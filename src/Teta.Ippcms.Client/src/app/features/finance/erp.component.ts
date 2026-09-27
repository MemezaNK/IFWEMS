import { Component, OnInit, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';

/** Inbound ERP payment/expenditure/commitment interface: message log, reconciliation and controlled retry (FR-FIN-002). */
@Component({
  selector: 'teta-finance-erp',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>ERP interface</h1>
        <button mat-flat-button color="primary" (click)="retry()"><mat-icon>replay</mat-icon> Retry errors</button>
      </div>
      <div class="card">
        <h2 style="margin-top: 0">Reconciliation runs</h2>
        <teta-data-table [columns]="reconColumns" [rows]="reconciliations()" emptyText="No reconciliation runs yet." exportName="erp-reconciliations" />
      </div>
      <div class="card" style="margin-top: 16px">
        <h2 style="margin-top: 0">Messages</h2>
        <teta-data-table [columns]="messageColumns" [rows]="messages()" emptyText="No ERP messages." exportName="erp-messages" />
      </div>
    </div>
  `
})
export class ErpComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly snack = inject(MatSnackBar);
  readonly messages = signal<any[]>([]);
  readonly reconciliations = signal<any[]>([]);

  reconColumns: Column[] = [
    { key: 'messageType', label: 'Type' }, { key: 'runAtUtc', label: 'Run at', type: 'datetime' }, { key: 'expectedCount', label: 'Expected', type: 'number' },
    { key: 'processedCount', label: 'Processed', type: 'number' }, { key: 'errorCount', label: 'Errors', type: 'number' },
    { key: 'expectedTotal', label: 'Expected total', type: 'money' }, { key: 'processedTotal', label: 'Processed total', type: 'money' },
    { key: 'balanced', label: 'Balanced', type: 'bool' }, { key: 'notes', label: 'Notes' }
  ];
  messageColumns: Column[] = [
    { key: 'direction', label: 'Direction' }, { key: 'messageType', label: 'Type' }, { key: 'externalReference', label: 'Reference' },
    { key: 'controlAmount', label: 'Amount', type: 'money' }, { key: 'status', label: 'Status', type: 'status' }, { key: 'error', label: 'Error' },
    { key: 'attempts', label: 'Attempts', type: 'number' }, { key: 'createdAtUtc', label: 'Created', type: 'datetime' }, { key: 'processedAtUtc', label: 'Processed', type: 'datetime' }
  ];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('finance/erp/messages').subscribe(r => this.messages.set(r));
    this.api.get<any[]>('finance/erp/reconciliations').subscribe(r => this.reconciliations.set(r));
  }

  retry(): void {
    this.api.post('finance/erp/retry').subscribe((r: any) => {
      this.snack.open(`Retried: ${r.processed} processed, ${r.errors} errors.`, 'OK', { duration: 4000 });
      this.load();
    });
  }
}
