import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/auth/auth.service';

interface DashboardCard {
  title: string;
  description: string;
  icon: string;
  link: string;
  roles?: string[];
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  private readonly allCards: DashboardCard[] = [
    { title: 'Case Management', description: 'Capture, review and track cases.', icon: 'folder_open', link: '/cases' },
    { title: 'Compliance Check', description: 'Run automated compliance checks on transactions.', icon: 'fact_check', link: '/compliance/check' },
    {
      title: 'Emergency Override',
      description: 'Approve an emergency override with justification.',
      icon: 'warning',
      link: '/compliance/override',
      roles: ['ApprovingOfficial', 'SystemAdministrator']
    }
  ];

  constructor(readonly authService: AuthService) {}

  visibleCards(): DashboardCard[] {
    return this.allCards.filter((card) => !card.roles || this.authService.hasRole(...card.roles));
  }
}
