import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from '../../core/auth/auth.service';
import { CaseService } from '../../core/cases/case.service';
import { SuppliersService } from '../../core/suppliers/suppliers.service';
import { ContractsService } from '../../core/contracts/contracts.service';
import { SlaService } from '../../core/sla/sla.service';
import { AuditService } from '../../core/audit/audit.service';
import { TransactionsService } from '../../core/transactions/transactions.service';
import { CaseDto, CaseStatus, CaseType } from '../../core/models/case.models';
import { OverdueCaseDto } from '../../core/models/sla.models';
import { AuditLogDto, AuditLogPageDto } from '../../core/models/audit.models';
import { TransactionPageDto } from '../../core/models/transaction.models';
import { EnumLabelPipe } from '../../core/shared/enum-label.pipe';

interface DashboardCard {
  title: string;
  description: string;
  icon: string;
  link: string;
  roles?: string[];
}

interface StatTile {
  label: string;
  value: string;
  icon: string;
  accent: string;
  sublabel?: string;
  link?: string;
}

interface StatusBarDatum {
  status: CaseStatus;
  count: number;
  percentage: number;
  color: string;
}

interface TypeSliceDatum {
  type: CaseType;
  count: number;
  percentage: number;
  color: string;
  dashArray: string;
  dashOffset: number;
}

interface TrendBarDatum {
  monthLabel: string;
  count: number;
  heightPct: number;
}

type RiskRating = 'GREEN' | 'AMBER' | 'RED';

interface RiskSegmentDatum {
  rating: RiskRating;
  label: string;
  count: number;
  percentage: number;
  color: string;
}

interface ActivityItem {
  icon: string;
  message: string;
  timeAgo: string;
  timestamp: string;
}

// Fixed lifecycle order (Section 24) so the pipeline chart always reads left-to-right
// as "how far along the case is", not an arbitrary API ordering.
const CASE_STATUS_ORDER: CaseStatus[] = [
  'Draft',
  'UnderAssessment',
  'UnderInvestigation',
  'Determined',
  'RecoveryInProgress',
  'ConsequenceManagement',
  'CorrectiveActionPending',
  'Closed'
];

// Reuses the exact hex values already assigned to these statuses by the .status-chip
// design system (styles.scss) so a status means the same color everywhere in the app.
const STATUS_COLORS: Record<CaseStatus, string> = {
  Draft: '#455a64',
  UnderAssessment: '#1565c0',
  UnderInvestigation: '#e65100',
  Determined: '#5e35b1',
  RecoveryInProgress: '#00838f',
  ConsequenceManagement: '#d84315',
  CorrectiveActionPending: '#f9a825',
  Closed: '#2e7d32'
};

const CASE_TYPE_ORDER: CaseType[] = [
  'IrregularExpenditure',
  'FruitlessWasteful',
  'UnauthorisedExpenditure',
  'PotentialNonCompliance'
];

const TYPE_COLORS: Record<CaseType, string> = {
  IrregularExpenditure: '#2a78d6',
  FruitlessWasteful: '#eb6834',
  UnauthorisedExpenditure: '#1baf7a',
  PotentialNonCompliance: '#eda100'
};

// Matches the .risk-chip convention used on the Compliance Check and Transaction
// Register screens (GREEN/AMBER/RED).
const RISK_COLORS: Record<RiskRating, string> = {
  GREEN: '#2e7d32',
  AMBER: '#f9a825',
  RED: '#c62828'
};

const EMPTY_TX_PAGE: TransactionPageDto = { items: [], totalCount: 0, page: 1, pageSize: 1 };
const EMPTY_AUDIT_PAGE: AuditLogPageDto = { items: [], totalCount: 0, page: 1, pageSize: 8 };

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    EnumLabelPipe
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  readonly isLoading = signal(true);
  readonly loadError = signal(false);

  readonly statTiles = signal<StatTile[]>([]);
  readonly statusBars = signal<StatusBarDatum[]>([]);
  readonly typeSlices = signal<TypeSliceDatum[]>([]);
  readonly typeTotal = signal(0);
  readonly trendBars = signal<TrendBarDatum[]>([]);
  readonly riskSegments = signal<RiskSegmentDatum[]>([]);
  readonly recoveryRatePct = signal(0);
  readonly recoverySeverity = signal<'good' | 'warning' | 'critical'>('warning');
  readonly recentActivity = signal<ActivityItem[]>([]);
  readonly overdueCases = signal<OverdueCaseDto[]>([]);

  readonly donutRadius = 54;
  readonly donutCircumference = 2 * Math.PI * this.donutRadius;

  private readonly enumLabelPipe = new EnumLabelPipe();

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

  readonly canSeeCompliance = computed(() =>
    this.authService.hasRole('ComplianceOfficer', 'ApprovingOfficial', 'ReadOnlyAuditor', 'SystemAdministrator')
  );
  readonly canSeeAudit = computed(() =>
    this.authService.hasRole('ReadOnlyAuditor', 'ComplianceOfficer', 'SystemAdministrator')
  );

  constructor(
    readonly authService: AuthService,
    private readonly caseService: CaseService,
    private readonly suppliersService: SuppliersService,
    private readonly contractsService: ContractsService,
    private readonly slaService: SlaService,
    private readonly auditService: AuditService,
    private readonly transactionsService: TransactionsService
  ) {}

  ngOnInit(): void {
    this.loadDashboardData();
  }

  visibleCards(): DashboardCard[] {
    return this.allCards.filter((card) => !card.roles || this.authService.hasRole(...card.roles));
  }

  refresh(): void {
    this.loadDashboardData();
  }

  private loadDashboardData(): void {
    this.isLoading.set(true);
    this.loadError.set(false);

    const suppliers$ = this.suppliersService.getAll().pipe(catchError(() => of([])));
    const contracts$ = this.contractsService.getAll().pipe(catchError(() => of([])));
    const overdue$ = this.slaService.getOverdueCases().pipe(catchError(() => of([])));

    const risk$ = this.canSeeCompliance()
      ? forkJoin({
          green: this.transactionsService.query({ riskRating: 'GREEN', page: 1, pageSize: 1 }).pipe(catchError(() => of(EMPTY_TX_PAGE))),
          amber: this.transactionsService.query({ riskRating: 'AMBER', page: 1, pageSize: 1 }).pipe(catchError(() => of(EMPTY_TX_PAGE))),
          red: this.transactionsService.query({ riskRating: 'RED', page: 1, pageSize: 1 }).pipe(catchError(() => of(EMPTY_TX_PAGE)))
        })
      : of(null);

    const activity$ = this.canSeeAudit()
      ? this.auditService.query({ page: 1, pageSize: 8 }).pipe(catchError(() => of(EMPTY_AUDIT_PAGE)))
      : of(null);

    forkJoin({
      cases: this.caseService.getAll(),
      suppliers: suppliers$,
      contracts: contracts$,
      overdue: overdue$,
      risk: risk$,
      activity: activity$
    }).subscribe({
      next: (result) => {
        this.processCases(result.cases, result.suppliers.length, result.contracts.length, result.overdue);
        this.overdueCases.set([...result.overdue].sort((a, b) => b.hoursOverdue - a.hoursOverdue).slice(0, 5));

        if (result.risk) {
          this.processRisk(result.risk.green.totalCount, result.risk.amber.totalCount, result.risk.red.totalCount);
        }
        if (result.activity) {
          this.recentActivity.set(result.activity.items.map((entry) => this.toActivityItem(entry)));
        }

        this.isLoading.set(false);
      },
      error: (error) => {
        console.error('Failed to load dashboard data:', error);
        this.isLoading.set(false);
        this.loadError.set(true);
      }
    });
  }

  private processCases(cases: CaseDto[], suppliersCount: number, contractsCount: number, overdue: OverdueCaseDto[]): void {
    const total = cases.length;
    const statusCounts: Partial<Record<CaseStatus, number>> = {};
    const typeCounts: Partial<Record<CaseType, number>> = {};
    let totalExposure = 0;
    let totalRecoverable = 0;
    let totalRecovered = 0;

    for (const c of cases) {
      statusCounts[c.status] = (statusCounts[c.status] ?? 0) + 1;
      typeCounts[c.caseType] = (typeCounts[c.caseType] ?? 0) + 1;
      totalExposure += c.amountInvolved ?? 0;
      totalRecoverable += c.recoverableAmount ?? 0;
      totalRecovered += c.recoveredAmount ?? 0;
    }

    // Status pipeline: always show all 8 lifecycle stages (even at zero) so the shape
    // of the pipeline is visible, not just the stages that happen to be populated today.
    this.statusBars.set(
      CASE_STATUS_ORDER.map((status) => ({
        status,
        count: statusCounts[status] ?? 0,
        percentage: total > 0 ? ((statusCounts[status] ?? 0) / total) * 100 : 0,
        color: STATUS_COLORS[status]
      }))
    );

    // Case-type donut: only slices that actually occur, with a 2-unit surface gap
    // between segments per the mark spec.
    const typeEntries = CASE_TYPE_ORDER.map((type) => ({ type, count: typeCounts[type] ?? 0 })).filter(
      (entry) => entry.count > 0
    );
    const typeTotalCount = typeEntries.reduce((sum, e) => sum + e.count, 0);
    const gap = typeEntries.length > 1 ? 3 : 0;
    let cumulative = 0;
    const slices: TypeSliceDatum[] = typeEntries.map((entry) => {
      const percentage = typeTotalCount > 0 ? (entry.count / typeTotalCount) * 100 : 0;
      const length = (percentage / 100) * this.donutCircumference;
      const dashLength = Math.max(length - gap, 0);
      const dashOffset = -(cumulative + gap / 2);
      cumulative += length;
      return {
        type: entry.type,
        count: entry.count,
        percentage,
        color: TYPE_COLORS[entry.type],
        dashArray: `${dashLength} ${this.donutCircumference - dashLength}`,
        dashOffset
      };
    });
    this.typeSlices.set(slices);
    this.typeTotal.set(typeTotalCount);

    // Monthly trend: last 6 calendar months including the current one, bucketed by the
    // case's real createdAtUtc so this reflects actual system activity, not a mock series.
    const now = new Date();
    const months: { key: string; label: string; count: number }[] = [];
    for (let i = 5; i >= 0; i--) {
      const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
      months.push({ key: `${d.getFullYear()}-${d.getMonth()}`, label: d.toLocaleString('en-ZA', { month: 'short' }), count: 0 });
    }
    const monthIndex = new Map(months.map((m, idx) => [m.key, idx]));
    for (const c of cases) {
      const created = new Date(c.createdAtUtc);
      const key = `${created.getFullYear()}-${created.getMonth()}`;
      const idx = monthIndex.get(key);
      if (idx !== undefined) {
        months[idx].count += 1;
      }
    }
    const maxCount = Math.max(...months.map((m) => m.count), 1);
    this.trendBars.set(
      months.map((m) => ({ monthLabel: m.label, count: m.count, heightPct: (m.count / maxCount) * 100 }))
    );

    // Recovery meter: recovered as a share of what is actually recoverable.
    const rate = totalRecoverable > 0 ? (totalRecovered / totalRecoverable) * 100 : 0;
    this.recoveryRatePct.set(Math.round(rate));
    this.recoverySeverity.set(rate >= 60 ? 'good' : rate >= 30 ? 'warning' : 'critical');

    const tiles: StatTile[] = [
      { label: 'Total Cases', value: total.toLocaleString('en-ZA'), icon: 'folder_open', accent: '#1f3d85', link: '/cases' },
      { label: 'Financial Exposure', value: this.formatCurrency(totalExposure), icon: 'payments', accent: '#e2a012' },
      {
        label: 'Total Recovered',
        value: this.formatCurrency(totalRecovered),
        icon: 'savings',
        accent: '#2e7d32',
        sublabel: `${Math.round(rate)}% of recoverable`
      },
      {
        label: 'Active Investigations',
        value: (statusCounts['UnderInvestigation'] ?? 0).toLocaleString('en-ZA'),
        icon: 'search',
        accent: '#e65100',
        link: '/cases'
      },
      {
        label: 'SLA Breaches',
        value: overdue.length.toLocaleString('en-ZA'),
        icon: 'schedule',
        accent: '#c62828',
        sublabel: overdue.length > 0 ? 'Needs attention' : 'All within SLA'
      },
      { label: 'Suppliers', value: suppliersCount.toLocaleString('en-ZA'), icon: 'business', accent: '#5e35b1', link: '/suppliers' },
      { label: 'Contracts', value: contractsCount.toLocaleString('en-ZA'), icon: 'description', accent: '#00838f', link: '/contracts' }
    ];
    this.statTiles.set(tiles);
  }

  private processRisk(green: number, amber: number, red: number): void {
    const total = green + amber + red;
    const build = (rating: RiskRating, label: string, count: number): RiskSegmentDatum => ({
      rating,
      label,
      count,
      percentage: total > 0 ? (count / total) * 100 : 0,
      color: RISK_COLORS[rating]
    });
    this.riskSegments.set([build('GREEN', 'Low risk', green), build('AMBER', 'Medium risk', amber), build('RED', 'High risk', red)]);

    this.statTiles.update((tiles) => [
      ...tiles,
      {
        label: 'High-Risk Transactions',
        value: red.toLocaleString('en-ZA'),
        icon: 'report_problem',
        accent: '#c62828',
        sublabel: total > 0 ? `of ${total.toLocaleString('en-ZA')} screened` : 'None screened yet',
        link: '/compliance/transactions'
      }
    ]);
  }

  private toActivityItem(entry: AuditLogDto): ActivityItem {
    const icon = entry.action === 'Added' ? 'add_circle' : entry.action === 'Deleted' ? 'remove_circle' : 'edit';
    const verb = entry.action === 'Added' ? 'created' : entry.action === 'Deleted' ? 'deleted' : 'updated';
    const who = entry.username ?? 'System';
    const what = this.enumLabelPipe.transform(entry.entityName);
    return {
      icon,
      message: `${who} ${verb} ${what}`,
      timeAgo: this.timeAgo(entry.timestampUtc),
      timestamp: entry.timestampUtc
    };
  }

  private timeAgo(isoDate: string): string {
    const then = new Date(isoDate).getTime();
    const diffMs = Date.now() - then;
    const minutes = Math.floor(diffMs / 60000);
    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 30) return `${days}d ago`;
    return new Date(isoDate).toLocaleDateString('en-ZA', { day: 'numeric', month: 'short' });
  }

  private formatCurrency(value: number): string {
    return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', maximumFractionDigits: 0 }).format(value);
  }
}
