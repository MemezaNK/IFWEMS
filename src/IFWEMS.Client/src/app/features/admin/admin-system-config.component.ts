import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-admin-system-config',
  standalone: true,
  imports: [MatCardModule],
  template: `
    <div class="admin-system-config">
      <h1>System Configuration</h1>
      <mat-card>
        <mat-card-content>
          <p>Configure system settings, parameters and external integrations.</p>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .admin-system-config {
      h1 {
        margin-bottom: 20px;
      }
    }
  `]
})
export class AdminSystemConfigComponent {}
