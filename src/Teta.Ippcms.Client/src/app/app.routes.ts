import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from './core/guards';
import { P } from './core/models';
import { LoginComponent } from './features/login/login.component';
import { PLANS } from './features/strategy/strategy.config';
import { ShellComponent } from './layout/shell.component';

const perm = (...permissions: string[]) => ({ canActivate: [permissionGuard], data: { permissions } });

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent) },
      { path: 'inbox', loadComponent: () => import('./features/inbox/inbox.component').then(m => m.InboxComponent), ...perm(P.workflowDecide) },
      { path: 'dashboard', loadComponent: () => import('./features/reports/reports-catalogue.component').then(m => m.ReportsCatalogueComponent), ...perm(P.reportsBoard, P.reportsRead) },
      { path: 'notifications', loadComponent: () => import('./features/notifications/notifications.component').then(m => m.NotificationsComponent) },
      { path: 'account', loadComponent: () => import('./features/account/change-password.component').then(m => m.ChangePasswordComponent) },
      { path: 'search', loadComponent: () => import('./features/search/search-results.component').then(m => m.SearchResultsComponent) },

      // Strategy & performance
      { path: 'strategy/plans', loadComponent: () => import('./shared/crud-page.component').then(m => m.CrudPageComponent),
        canActivate: [permissionGuard], data: { permissions: [P.strategyRead], crud: PLANS } },
      { path: 'strategy/plans/:id', loadComponent: () => import('./features/strategy/plan-detail.component').then(m => m.PlanDetailComponent), ...perm(P.strategyRead) },
      { path: 'strategy/performance', loadComponent: () => import('./features/strategy/performance.component').then(m => m.PerformanceComponent), ...perm(P.strategyRead) },
      { path: 'strategy/results', loadComponent: () => import('./features/strategy/results.component').then(m => m.ResultsComponent), ...perm(P.strategyRead, P.performanceCapture) },

      // Portfolio & projects
      { path: 'portfolio', loadComponent: () => import('./features/projects/hierarchy.component').then(m => m.HierarchyComponent), ...perm(P.portfolioRead) },
      { path: 'projects', loadComponent: () => import('./features/projects/projects-list.component').then(m => m.ProjectsListComponent), ...perm(P.portfolioRead) },
      { path: 'projects/:id', loadComponent: () => import('./features/projects/project-detail.component').then(m => m.ProjectDetailComponent), ...perm(P.portfolioRead) },
      { path: 'issues', loadComponent: () => import('./features/projects/issues-register.component').then(m => m.IssuesRegisterComponent), ...perm(P.portfolioRead) },
      { path: 'change-requests', loadComponent: () => import('./features/projects/change-requests-register.component').then(m => m.ChangeRequestsRegisterComponent), ...perm(P.portfolioRead) },
      { path: 'workload', loadComponent: () => import('./features/projects/workload.component').then(m => m.WorkloadComponent), ...perm(P.portfolioRead) },

      // Procurement & suppliers
      { path: 'procurement/dashboard', loadComponent: () => import('./features/procurement/procurement-dashboard.component').then(m => m.ProcurementDashboardComponent), ...perm(P.procurementRead) },
      { path: 'procurement/plan', loadComponent: () => import('./features/procurement/procurement-plan.component').then(m => m.ProcurementPlanComponent), ...perm(P.procurementRead, P.budgetManage) },
      { path: 'procurement/requisitions', loadComponent: () => import('./features/procurement/requisitions.component').then(m => m.RequisitionsComponent), ...perm(P.procurementRead) },
      { path: 'procurement/exceptions', loadComponent: () => import('./features/procurement/exceptions.component').then(m => m.ExceptionsComponent), ...perm(P.procurementRead) },
      { path: 'procurement/transparency', loadComponent: () => import('./features/procurement/procurement-transparency.component').then(m => m.ProcurementTransparencyComponent), ...perm(P.procurementRead) },
      { path: 'procurement', loadComponent: () => import('./features/procurement/procurement-list.component').then(m => m.ProcurementListComponent), ...perm(P.procurementRead, P.procurementEvaluate) },
      { path: 'procurement/:id', loadComponent: () => import('./features/procurement/procurement-detail.component').then(m => m.ProcurementDetailComponent), ...perm(P.procurementRead, P.procurementEvaluate) },
      { path: 'suppliers', loadComponent: () => import('./features/suppliers/suppliers-list.component').then(m => m.SuppliersListComponent), ...perm(P.supplierRead) },
      { path: 'suppliers/:id', loadComponent: () => import('./features/suppliers/supplier-detail.component').then(m => m.SupplierDetailComponent), ...perm(P.supplierRead) },

      // Contracts
      { path: 'contracts/dashboard', loadComponent: () => import('./features/contracts/contracts-dashboard.component').then(m => m.ContractsDashboardComponent), ...perm(P.contractRead) },
      { path: 'contracts', loadComponent: () => import('./features/contracts/contracts-list.component').then(m => m.ContractsListComponent), ...perm(P.contractRead) },
      { path: 'contracts/:id', loadComponent: () => import('./features/contracts/contract-detail.component').then(m => m.ContractDetailComponent), ...perm(P.contractRead) },

      // Finance
      { path: 'finance', loadComponent: () => import('./features/finance/finance-dashboard.component').then(m => m.FinanceDashboardComponent), ...perm(P.financeRead) },
      { path: 'finance/erp', loadComponent: () => import('./features/finance/erp.component').then(m => m.ErpComponent), ...perm(P.financeErp) },
      { path: 'finance/invoices', loadComponent: () => import('./features/finance/invoices.component').then(m => m.InvoicesComponent), ...perm(P.financeRead) },
      { path: 'finance/invoices/:id', loadComponent: () => import('./features/finance/invoice-detail.component').then(m => m.InvoiceDetailComponent), ...perm(P.financeRead) },

      // Monitoring & evaluation
      { path: 'me', loadComponent: () => import('./features/monitoring/monitoring-dashboard.component').then(m => m.MonitoringDashboardComponent), ...perm(P.meRead) },
      { path: 'me/visits', loadComponent: () => import('./features/monitoring/visits.component').then(m => m.VisitsComponent), ...perm(P.meRead) },
      { path: 'me/visits/:id', loadComponent: () => import('./features/monitoring/visit-detail.component').then(m => m.VisitDetailComponent), ...perm(P.meRead) },
      { path: 'me/actions', loadComponent: () => import('./features/monitoring/findings-actions.component').then(m => m.FindingsActionsComponent) },
      { path: 'me/beneficiaries', loadComponent: () => import('./features/monitoring/beneficiaries.component').then(m => m.BeneficiariesComponent), ...perm(P.beneficiaryManage, P.meRead) },

      // Risk & assurance
      { path: 'assurance', loadComponent: () => import('./features/assurance/assurance-dashboard.component').then(m => m.AssuranceDashboardComponent), ...perm(P.riskRead, P.riskManage) },
      { path: 'assurance/compliance', loadComponent: () => import('./features/assurance/compliance.component').then(m => m.ComplianceComponent), ...perm(P.riskRead, P.riskManage) },
      { path: 'assurance/audit-findings', loadComponent: () => import('./features/assurance/audit-findings.component').then(m => m.AuditFindingsComponent), ...perm(P.riskRead, P.riskManage) },
      { path: 'assurance/risks', loadComponent: () => import('./features/assurance/risks-list.component').then(m => m.RisksListComponent), ...perm(P.riskRead, P.riskManage) },
      { path: 'assurance/risks/:id', loadComponent: () => import('./features/assurance/risk-detail.component').then(m => m.RiskDetailComponent), ...perm(P.riskRead, P.riskManage) },

      // Reporting
      { path: 'reports/exceptions', loadComponent: () => import('./features/reports/exceptions-report.component').then(m => m.ExceptionsReportComponent), ...perm(P.reportsRead, P.reportsBoard, P.portfolioRead) },
      { path: 'reports/board-packs', loadComponent: () => import('./features/reports/board-packs.component').then(m => m.BoardPacksComponent), ...perm(P.reportsBoard) },
      { path: 'reports/analytics', loadComponent: () => import('./features/reports/analytics.component').then(m => m.AnalyticsComponent), ...perm(P.reportsRead, P.reportsBoard, P.portfolioRead) },
      { path: 'reports/data-quality', loadComponent: () => import('./features/reports/data-quality.component').then(m => m.DataQualityComponent), ...perm(P.reportsRead, P.dataQualityManage) },
      { path: 'reports/user-activity', loadComponent: () => import('./features/reports/user-activity-report.component').then(m => m.UserActivityReportComponent), ...perm(P.reportsRead, P.reportsBoard, P.portfolioRead) },
      { path: 'reports/run/:code', loadComponent: () => import('./features/reports/report-run.component').then(m => m.ReportRunComponent), ...perm(P.reportsRead, P.reportsBoard, P.portfolioRead) },
      { path: 'reports/learner-delivery/:projectId', loadComponent: () => import('./features/reports/learner-delivery-report.component').then(m => m.LearnerDeliveryReportComponent), ...perm(P.meRead) },
      { path: 'reports/learner-delivery', loadComponent: () => import('./features/reports/learner-delivery-report.component').then(m => m.LearnerDeliveryReportComponent), ...perm(P.meRead) },
      { path: 'reports', loadComponent: () => import('./features/reports/reports-catalogue.component').then(m => m.ReportsCatalogueComponent), ...perm(P.reportsRead, P.reportsBoard, P.portfolioRead) },

      // Supplier portal
      { path: 'portal', loadComponent: () => import('./features/portal/portal-home.component').then(m => m.PortalHomeComponent), ...perm(P.portalAccess) },

      // Administration
      { path: 'admin', loadComponent: () => import('./features/admin/admin.component').then(m => m.AdminComponent),
        ...perm(P.adminConfig, P.adminWorkflow, P.adminDelegations, P.adminImport, P.adminConfigApprove) },
      { path: 'security', loadComponent: () => import('./features/security/security.component').then(m => m.SecurityComponent), ...perm(P.securityUsers, P.securityRolesApprove) },
      { path: 'audit', loadComponent: () => import('./features/audit/audit-search.component').then(m => m.AuditSearchComponent), ...perm(P.auditRead) },
      { path: 'audit/trail/:entityType/:entityId', loadComponent: () => import('./features/audit/audit-trail.component').then(m => m.AuditTrailComponent), ...perm(P.auditRead) }
    ]
  },
  { path: '**', redirectTo: '' }
];
