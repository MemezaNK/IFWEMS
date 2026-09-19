import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-admin-rules',
  standalone: true,
  imports: [MatCardModule],
  template: `
    <div class="admin-rules">
      <h1>Compliance Rules Management</h1>
      <mat-card>
        <mat-card-content>
          <p>Manage and configure compliance rules for automated checks.</p>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .admin-rules {
      h1 {
        margin-bottom: 20px;
      }
    }
  `]
})
export class AdminRulesComponent {}
