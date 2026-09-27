import { DecimalPipe } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { DocumentsPanelComponent } from '../../shared/documents-panel.component';
import { StatusChipComponent } from '../../shared/status-chip.component';
import { CommentsComponent } from '../common/comments.component';
import { ContractDeliverablesComponent } from './contract-deliverables.component';
import { ContractOverviewComponent } from './contract-overview.component';
import { ContractPerformanceComponent } from './contract-performance.component';

/** A single contract: obligations, deliverables, variations, performance, breaches and close-out (SRS §5.6). */
@Component({
  selector: 'teta-contract-detail',
  standalone: true,
  imports: [DecimalPipe, RouterLink, MatTabsModule, MatButtonModule, MatIconModule, StatusChipComponent, DocumentsPanelComponent, CommentsComponent,
    ContractOverviewComponent, ContractDeliverablesComponent, ContractPerformanceComponent],
  template: `
    @if (detail(); as d) {
      <div class="page">
        <div class="page-header">
          <h1>{{ d.summary.contractNumber }} – {{ d.summary.title }}</h1>
          <teta-status [value]="d.summary.status" /> <teta-status [value]="d.summary.signatureStatus" />
          @if (canAudit) { <a mat-stroked-button [routerLink]="['/audit/trail', 'Contract', d.summary.id]"><mat-icon>history</mat-icon> Audit trail</a> }
          <div class="subtitle">{{ d.summary.projectReference }} · {{ d.summary.supplierName }} · R {{ d.summary.revisedValue | number }}</div>
        </div>
        <mat-tab-group animationDuration="0">
          <mat-tab label="Overview"><teta-contract-overview [detail]="d" (changed)="load()" /></mat-tab>
          <mat-tab label="Deliverables & variations"><ng-template matTabContent><teta-contract-deliverables [detail]="d" (changed)="load()" /></ng-template></mat-tab>
          <mat-tab label="Performance & close-out"><ng-template matTabContent><teta-contract-performance [detail]="d" (changed)="load()" /></ng-template></mat-tab>
          <mat-tab label="Documents">
            <ng-template matTabContent>
              <div style="padding-top: 12px" class="card"><teta-documents parentType="Contract" [parentId]="d.summary.id" /></div>
            </ng-template>
          </mat-tab>
          <mat-tab label="Activity"><ng-template matTabContent><div style="padding-top: 12px"><teta-comments parentType="Contract" [parentId]="d.summary.id" /></div></ng-template></mat-tab>
        </mat-tab-group>
      </div>
    }
  `
})
export class ContractDetailComponent implements OnChanges {
  @Input() id!: string;
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  readonly detail = signal<any | null>(null);

  get canAudit(): boolean { return this.auth.has(P.auditRead); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.get(`contracts/${this.id}`).subscribe(d => this.detail.set(d));
  }
}
