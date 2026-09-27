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
import { ProcurementAdjudicationComponent } from './procurement-adjudication.component';
import { ProcurementCommitteeComponent } from './procurement-committee.component';
import { ProcurementEvaluationComponent } from './procurement-evaluation.component';
import { ProcurementOverviewComponent } from './procurement-overview.component';
import { ProcurementSourcingComponent } from './procurement-sourcing.component';

/** A single sourcing process from specification through award (SRS §5.4). */
@Component({
  selector: 'teta-procurement-detail',
  standalone: true,
  imports: [DecimalPipe, RouterLink, MatTabsModule, MatButtonModule, MatIconModule, StatusChipComponent, DocumentsPanelComponent, CommentsComponent,
    ProcurementOverviewComponent, ProcurementCommitteeComponent, ProcurementSourcingComponent, ProcurementEvaluationComponent, ProcurementAdjudicationComponent],
  template: `
    @if (detail(); as d) {
      <div class="page">
        <div class="page-header">
          <h1>{{ d.summary.number }} – {{ d.summary.title }}</h1>
          <teta-status [value]="d.summary.status" />
          @if (canAudit) { <a mat-stroked-button [routerLink]="['/audit/trail', 'Procurement', d.summary.id]"><mat-icon>history</mat-icon> Audit trail</a> }
          <div class="subtitle">{{ d.summary.projectReference }} · {{ d.summary.method }} · R {{ d.summary.estimatedValue | number }}</div>
        </div>
        <mat-tab-group [selectedIndex]="tab()" (selectedIndexChange)="tab.set($event)" animationDuration="0">
          <mat-tab label="Overview"><teta-procurement-overview [detail]="d" (changed)="load()" /></mat-tab>
          <mat-tab label="Committees"><ng-template matTabContent><teta-procurement-committee [detail]="d" (changed)="load()" /></ng-template></mat-tab>
          <mat-tab label="Advert & bids"><ng-template matTabContent><teta-procurement-sourcing [detail]="d" (changed)="load()" /></ng-template></mat-tab>
          <mat-tab label="Evaluation"><ng-template matTabContent><teta-procurement-evaluation [detail]="d" (changed)="load()" /></ng-template></mat-tab>
          <mat-tab label="Adjudication & award"><ng-template matTabContent><teta-procurement-adjudication [detail]="d" (changed)="load()" /></ng-template></mat-tab>
          <mat-tab label="Documents">
            <ng-template matTabContent>
              <div style="padding-top: 12px" class="grid">
                <div class="card"><teta-documents parentType="Procurement" [parentId]="d.summary.id" /></div>
              </div>
            </ng-template>
          </mat-tab>
          <mat-tab label="Activity"><ng-template matTabContent><div style="padding-top: 12px"><teta-comments parentType="Procurement" [parentId]="d.summary.id" /></div></ng-template></mat-tab>
        </mat-tab-group>
      </div>
    }
  `
})
export class ProcurementDetailComponent implements OnChanges {
  @Input() id!: string;
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  readonly detail = signal<any | null>(null);
  readonly tab = signal(0);

  get canAudit(): boolean { return this.auth.has(P.auditRead); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.get(`procurement/${this.id}`).subscribe(d => this.detail.set(d));
  }
}
