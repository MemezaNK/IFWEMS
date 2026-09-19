import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';

interface SolutionCard {
  title: string;
  description: string;
  icon: string;
}

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule, MatCardModule],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss'
})
export class LandingComponent {
  readonly navLinks = [];

  readonly solutions: SolutionCard[] = [
    {
      title: 'Case Management',
      description: 'Capture, triage and track fraud, waste and irregular expenditure cases through their full lifecycle.',
      icon: 'folder_open'
    },
    {
      title: 'Compliance Tracking',
      description: 'Run automated compliance checks against transactions and manage emergency overrides with justification.',
      icon: 'fact_check'
    },
    {
      title: 'Investigations & Recoveries',
      description: 'Coordinate investigations, corrective actions and financial recoveries in one auditable trail.',
      icon: 'search'
    },
    {
      title: 'Reporting & SLA Monitoring',
      description: 'Monitor overdue cases against SLA policies and export register-ready reports for oversight bodies.',
      icon: 'insights'
    }
  ];
}
