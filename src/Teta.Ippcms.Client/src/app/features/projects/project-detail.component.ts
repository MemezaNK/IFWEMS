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
import { ProjectBudgetComponent } from './project-budget.component';
import { ProjectClosureComponent } from './project-closure.component';
import { ProjectControlsComponent } from './project-controls.component';
import { ProjectGovernanceComponent } from './project-governance.component';
import { ProjectOverviewComponent } from './project-overview.component';
import { ProjectScheduleComponent } from './project-schedule.component';

/** Project record with summary, governance, financial, schedule, control, document, activity and audit tabs (SRS §13). */
@Component({
  selector: 'teta-project-detail',
  standalone: true,
  imports: [MatTabsModule, MatButtonModule, MatIconModule, RouterLink, StatusChipComponent, DocumentsPanelComponent, CommentsComponent,
    ProjectOverviewComponent, ProjectGovernanceComponent, ProjectBudgetComponent, ProjectScheduleComponent, ProjectControlsComponent, ProjectClosureComponent],
  template: `
    @if (project(); as p) {
      <div class="page">
        <div class="page-header">
          <h1>{{ p.reference }} – {{ p.name }}</h1>
          <teta-status [value]="p.status" /> <teta-status [value]="p.stage" /> <teta-status [value]="p.health" />
          @if (canAudit) { <a mat-stroked-button [routerLink]="['/audit/trail', 'Project', p.id]"><mat-icon>history</mat-icon> Audit trail</a> }
          <div class="subtitle">{{ p.portfolioName }} › {{ p.programmeName }} · Manager: {{ p.managerName ?? '—' }} · Sponsor: {{ p.sponsorName ?? '—' }} · Business owner: {{ p.businessOwner ?? '—' }}</div>
        </div>
        <mat-tab-group [selectedIndex]="tab()" (selectedIndexChange)="tab.set($event)" animationDuration="0">
          <mat-tab label="Summary"><teta-project-overview [project]="p" (changed)="load()" /></mat-tab>
          <mat-tab label="Governance"><ng-template matTabContent><teta-project-governance [project]="p" (changed)="load()" /></ng-template></mat-tab>
          <mat-tab label="Budget & finance"><ng-template matTabContent><teta-project-budget [project]="p" /></ng-template></mat-tab>
          <mat-tab label="Schedule"><ng-template matTabContent><teta-project-schedule [project]="p" /></ng-template></mat-tab>
          <mat-tab label="Issues, changes & risks"><ng-template matTabContent><teta-project-controls [project]="p" (changed)="load()" /></ng-template></mat-tab>
          <mat-tab label="Documents">
            <ng-template matTabContent>
              <div style="padding-top: 12px" class="grid">
                <div class="card"><teta-documents parentType="Project" [parentId]="p.id" /></div>
                <div class="card"><teta-documents parentType="Project" [parentId]="p.id" [evidenceMode]="true" /></div>
              </div>
            </ng-template>
          </mat-tab>
          <mat-tab label="Close-out & benefits"><ng-template matTabContent><teta-project-closure [project]="p" (changed)="load()" /></ng-template></mat-tab>
          <mat-tab label="Activity"><ng-template matTabContent><div style="padding-top: 12px"><teta-comments parentType="Project" [parentId]="p.id" /></div></ng-template></mat-tab>
        </mat-tab-group>
      </div>
    }
  `
})
export class ProjectDetailComponent implements OnChanges {
  @Input() id!: string;
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  readonly project = signal<any | null>(null);
  readonly tab = signal(0);

  get canAudit(): boolean { return this.auth.has(P.auditRead); }

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.get(`projects/${this.id}`).subscribe(p => this.project.set(p));
  }
}
