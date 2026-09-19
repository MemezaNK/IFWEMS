import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CommonModule } from '@angular/common';
import { signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { CaseService } from '../../core/cases/case.service';

interface DashboardCard {
  title: string;
  description: string;
  icon: string;
  link: string;
  roles?: string[];
}

interface DashboardMetric {
  label: string;
  value: number;
  unit?: string;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink, 
    MatCardModule, 
    MatButtonModule, 
    MatIconModule,
    MatProgressBarModule
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  metrics = signal<DashboardMetric[]>([]);
  caseStatusCounts = signal<Record<string, number>>({});
  caseTypeCounts = signal<Record<string, number>>({});
  systemStats = signal({
    totalCases: 0,
    totalSuppliers: 0,
    totalContracts: 0,
    pendingReview: 0
  });
  private readonly allCards: DashboardCard[] = [
    { title: 'Case Management', description: 'Capture, review and track cases.', icon: 'folder_open', link: '/cases' },
    { title: 'Compliance Check', description: 'Run automated compliance checks on transactions.', icon: 'fact_check', link: '/compliance/check' },
    {
      title: 'Emergency Override',
      description: 'Approve an emergency override with justification.',
      icon: 'warning',
      link: '/compliance/override',
      roles: ['ApprovingOfficial', 'SystemAdministrator']
    },
    {
      title: 'Contracts',
      description: 'Manage supplier contracts and monitor utilisation.',
      icon: 'description',
      link: '/contracts',
      roles: ['ContractOfficer', 'SystemAdministrator']
    },
    {
      title: 'Suppliers',
      description: 'Manage suppliers and view risk profiles.',
      icon: 'business',
      link: '/suppliers',
      roles: ['ContractOfficer', 'SystemAdministrator']
    },
    {
      title: 'Notifications',
      description: 'View and action your notifications.',
      icon: 'notifications',
      link: '/notifications'
    },
    {
      title: 'Reports',
      description: 'Download case, recovery and contract registers.',
      icon: 'summarize',
      link: '/reports',
      roles: ['CaseReviewer', 'ApprovingOfficial', 'ComplianceOfficer', 'FinanceOfficer', 'ContractOfficer', 'SystemAdministrator']
    },
    {
      title: 'Administration',
      description: 'Manage users, roles and organisation units.',
      icon: 'admin_panel_settings',
      link: '/admin',
      roles: ['SystemAdministrator']
    }
  ];

  constructor(readonly authService: AuthService, private caseService: CaseService) {}

  ngOnInit(): void {
    this.loadDashboardData();
  }

  visibleCards(): DashboardCard[] {
    return this.allCards.filter((card) => !card.roles || this.authService.hasRole(...card.roles));
  }

  private loadDashboardData(): void {
    this.caseService.getAll().subscribe({
      next: (cases: any[]) => {
        // Calculate metrics from cases
        const statusCounts: Record<string, number> = {};
        const typeCounts: Record<string, number> = {};
        
        cases.forEach((caseItem: any) => {
          // Count by status
          statusCounts[caseItem.status] = (statusCounts[caseItem.status] ?? 0) + 1;
          
          // Count by type
          typeCounts[caseItem.caseType] = (typeCounts[caseItem.caseType] ?? 0) + 1;
        });

        this.caseStatusCounts.set(statusCounts);
        this.caseTypeCounts.set(typeCounts);

        // Build metrics array
        const metricsArray: DashboardMetric[] = [
          {
            label: 'Total Cases',
            value: cases.length,
            icon: 'folder_open',
            color: '#FF6B6B'
          },
          {
            label: 'Under Investigation',
            value: statusCounts['UnderInvestigation'] ?? 0,
            icon: 'search',
            color: '#4ECDC4'
          },
          {
            label: 'Under Assessment',
            value: statusCounts['UnderAssessment'] ?? 0,
            icon: 'assignment',
            color: '#45B7D1'
          },
          {
            label: 'Recovery In Progress',
            value: statusCounts['RecoveryInProgress'] ?? 0,
            icon: 'trending_up',
            color: '#F7B731'
          },
          {
            label: 'Closed',
            value: statusCounts['Closed'] ?? 0,
            icon: 'check_circle',
            color: '#5F27CD'
          },
          {
            label: 'Pending Corrective Action',
            value: statusCounts['PendingCorrectiveAction'] ?? 0,
            icon: 'warning',
            color: '#EE5A6F'
          }
        ];

        this.metrics.set(metricsArray);
        
        // Update system stats
        this.systemStats.set({
          totalCases: cases.length,
          totalSuppliers: 0,
          totalContracts: 0,
          pendingReview: statusCounts['UnderAssessment'] ?? 0
        });
      },
      error: (error: any) => {
        console.error('Failed to load dashboard data:', error);
      }
    });
  }

  getStatusBarPercentage(status: string): number {
    const total = this.systemStats().totalCases;
    if (total === 0) return 0;
    return ((this.caseStatusCounts()[status] ?? 0) / total) * 100;
  }

  getTypePercentage(type: string): number {
    const total = this.systemStats().totalCases;
    if (total === 0) return 0;
    return ((this.caseTypeCounts()[type] ?? 0) / total) * 100;
  }

  getStatusColors(): Record<string, string> {
    return {
      'New': '#e74c3c',
      'UnderInvestigation': '#3498db',
      'UnderAssessment': '#f39c12',
      'RecoveryInProgress': '#9b59b6',
      'Closed': '#27ae60',
      'PendingCorrectiveAction': '#e67e22'
    };
  }

  getTypeColors(): Record<string, string> {
    return {
      'IrregularExpenditure': '#FF6B6B',
      'FruitlessExpenditure': '#4ECDC4',
      'WastedExpenditure': '#45B7D1',
      'UnauthorisedExpenditure': '#96CEB4',
      'Other': '#BFBFBF'
    };
  }
}
