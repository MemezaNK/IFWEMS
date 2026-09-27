import { Component, inject } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { AdminAccessComponent } from './admin-access.component';
import { AdminConfigComponent } from './admin-config.component';
import { AdminImportsComponent } from './admin-imports.component';
import { AdminWorkflowsComponent } from './admin-workflows.component';

/** Configuration administration: workflows, delegations/substitutions, segregation of duties, reference data, calendar, settings, templates, retention and bulk import (SRS §5.11). */
@Component({
  selector: 'teta-admin',
  standalone: true,
  imports: [MatTabsModule, AdminWorkflowsComponent, AdminAccessComponent, AdminConfigComponent, AdminImportsComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>Configuration</h1></div>
      <mat-tab-group animationDuration="0">
        @if (auth.hasAny(P.adminWorkflow, P.adminConfigApprove)) {
          <mat-tab label="Workflows"><ng-template matTabContent><teta-admin-workflows /></ng-template></mat-tab>
        }
        <mat-tab label="Delegations & substitutions"><ng-template matTabContent><teta-admin-access /></ng-template></mat-tab>
        @if (auth.has(P.adminConfig)) {
          <mat-tab label="Configuration"><ng-template matTabContent><teta-admin-config /></ng-template></mat-tab>
        }
        @if (auth.has(P.adminImport)) {
          <mat-tab label="Bulk imports"><ng-template matTabContent><teta-admin-imports /></ng-template></mat-tab>
        }
      </mat-tab-group>
    </div>
  `
})
export class AdminComponent {
  readonly auth = inject(AuthService);
  readonly P = P;
}
