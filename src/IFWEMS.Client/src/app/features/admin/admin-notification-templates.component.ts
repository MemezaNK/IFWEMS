import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-admin-notification-templates',
  standalone: true,
  imports: [MatCardModule],
  template: `
    <div class="admin-notification-templates">
      <h1>Notification Templates</h1>
      <mat-card>
        <mat-card-content>
          <p>Configure email and system notification templates.</p>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .admin-notification-templates {
      h1 {
        margin-bottom: 20px;
      }
    }
  `]
})
export class AdminNotificationTemplatesComponent {}
