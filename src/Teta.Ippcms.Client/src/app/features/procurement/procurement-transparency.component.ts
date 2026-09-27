import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ApiService } from '../../core/api.service';
import { Column, DataTableComponent } from '../../shared/data-table.component';

/** Public procurement transparency register: tender, bidders, award and outcome (FR-SCM-013, SRS §5.4 transparency). */
@Component({
  selector: 'teta-procurement-transparency',
  standalone: true,
  imports: [MatFormFieldModule, MatInputModule, FormsModule, DataTableComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>Procurement transparency register</h1></div>
      <div class="toolbar-row">
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>From</mat-label><input matInput type="date" [(ngModel)]="from" (ngModelChange)="load()" /></mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>To</mat-label><input matInput type="date" [(ngModel)]="to" (ngModelChange)="load()" /></mat-form-field>
      </div>
      <teta-data-table [columns]="columns" [rows]="rows()" emptyText="No published tenders in this period." exportName="transparency-register" />
    </div>
  `
})
export class ProcurementTransparencyComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly rows = signal<any[]>([]);
  from: string | null = null;
  to: string | null = null;

  columns: Column[] = [
    { key: 'tenderNumber', label: 'Tender number' }, { key: 'description', label: 'Description' }, { key: 'method', label: 'Method' },
    { key: 'closingDate', label: 'Closing date' }, { key: 'bidsReceived', label: 'Bids received', type: 'number' },
    { key: 'successfulBidder', label: 'Successful bidder' }, { key: 'supplierRegistration', label: 'Registration no.' },
    { key: 'bbbeeLevel', label: 'B-BBEE level', type: 'number' }, { key: 'awardValue', label: 'Award value', type: 'money' },
    { key: 'awardDate', label: 'Award date' }, { key: 'status', label: 'Status', type: 'status' }, { key: 'cancellationReason', label: 'Cancellation reason' }
  ];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.get<any[]>('procurement/transparency', { from: this.from, to: this.to }).subscribe(r => this.rows.set(r));
  }
}
