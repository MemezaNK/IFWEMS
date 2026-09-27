import { Component, inject } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { SecurityRolesComponent } from './security-roles.component';
import { SecuritySessionsComponent } from './security-sessions.component';
import { SecurityUsersComponent } from './security-users.component';

/** Identity and access administration with dual control: users, scoped role assignments, roles/permissions and active sessions (SRS §10). */
@Component({
  selector: 'teta-security',
  standalone: true,
  imports: [MatTabsModule, SecurityUsersComponent, SecurityRolesComponent, SecuritySessionsComponent],
  template: `
    <div class="page">
      <div class="page-header"><h1>Users &amp; roles</h1></div>
      <mat-tab-group animationDuration="0">
        @if (auth.hasAny(P.securityUsers, P.securityRolesApprove)) {
          <mat-tab label="Users"><ng-template matTabContent><teta-security-users /></ng-template></mat-tab>
        }
        <mat-tab label="Roles & permissions"><ng-template matTabContent><teta-security-roles /></ng-template></mat-tab>
        @if (auth.has(P.securityUsers)) {
          <mat-tab label="Sessions"><ng-template matTabContent><teta-security-sessions /></ng-template></mat-tab>
        }
      </mat-tab-group>
    </div>
  `
})
export class SecurityComponent {
  readonly auth = inject(AuthService);
  readonly P = P;
}
