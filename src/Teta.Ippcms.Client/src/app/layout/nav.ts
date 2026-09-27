import { P } from '../core/models';

export interface NavItem { label: string; icon: string; link: string; permissions?: string[]; supplierOnly?: boolean; }
export interface NavGroup { title: string; items: NavItem[]; }

/** Global navigation (SRS §13): Strategy, Portfolio, Procurement, Contracts, Projects, Performance and Assurance. */
export const NAV: NavGroup[] = [
  { title: '', items: [
    { label: 'Home', icon: 'home', link: '/' },
    { label: 'Approvals inbox', icon: 'inbox', link: '/inbox', permissions: [P.workflowDecide] },
    { label: 'Executive dashboard', icon: 'insights', link: '/dashboard', permissions: [P.reportsBoard, P.reportsRead] }
  ]},
  { title: 'Strategy & performance', items: [
    { label: 'Strategic plans / APP', icon: 'flag', link: '/strategy/plans', permissions: [P.strategyRead] },
    { label: 'APP performance', icon: 'query_stats', link: '/strategy/performance', permissions: [P.strategyRead] },
    { label: 'Performance results', icon: 'fact_check', link: '/strategy/results', permissions: [P.strategyRead, P.performanceCapture] }
  ]},
  { title: 'Portfolio & projects', items: [
    { label: 'Portfolio hierarchy', icon: 'account_tree', link: '/portfolio', permissions: [P.portfolioRead] },
    { label: 'Projects', icon: 'work', link: '/projects', permissions: [P.portfolioRead] },
    { label: 'Issues', icon: 'report_problem', link: '/issues', permissions: [P.portfolioRead] },
    { label: 'Change requests', icon: 'published_with_changes', link: '/change-requests', permissions: [P.portfolioRead] },
    { label: 'Resource workload', icon: 'groups', link: '/workload', permissions: [P.portfolioRead] }
  ]},
  { title: 'Procurement', items: [
    { label: 'Procurement dashboard', icon: 'dashboard', link: '/procurement/dashboard', permissions: [P.procurementRead] },
    { label: 'Procurement plan', icon: 'event_note', link: '/procurement/plan', permissions: [P.procurementRead, P.budgetManage] },
    { label: 'Requisitions', icon: 'request_quote', link: '/procurement/requisitions', permissions: [P.procurementRead] },
    { label: 'Procurements', icon: 'gavel', link: '/procurement', permissions: [P.procurementRead, P.procurementEvaluate] },
    { label: 'Deviations', icon: 'rule', link: '/procurement/exceptions', permissions: [P.procurementRead] },
    { label: 'Suppliers', icon: 'storefront', link: '/suppliers', permissions: [P.supplierRead] }
  ]},
  { title: 'Contracts & finance', items: [
    { label: 'Contracts', icon: 'description', link: '/contracts', permissions: [P.contractRead] },
    { label: 'Finance dashboard', icon: 'account_balance', link: '/finance', permissions: [P.financeRead] },
    { label: 'Invoices', icon: 'receipt_long', link: '/finance/invoices', permissions: [P.financeRead] },
    { label: 'ERP interface', icon: 'sync_alt', link: '/finance/erp', permissions: [P.financeErp] }
  ]},
  { title: 'Monitoring & assurance', items: [
    { label: 'M&E dashboard', icon: 'monitor_heart', link: '/me', permissions: [P.meRead] },
    { label: 'Monitoring visits', icon: 'place', link: '/me/visits', permissions: [P.meRead] },
    { label: 'Findings & actions', icon: 'task_alt', link: '/me/actions' },
    { label: 'Beneficiaries', icon: 'diversity_3', link: '/me/beneficiaries', permissions: [P.beneficiaryManage, P.meRead] },
    { label: 'Risk & assurance', icon: 'shield', link: '/assurance', permissions: [P.riskRead, P.riskManage] },
    { label: 'Risk register', icon: 'warning', link: '/assurance/risks', permissions: [P.riskRead, P.riskManage] },
    { label: 'Compliance', icon: 'verified_user', link: '/assurance/compliance', permissions: [P.riskRead, P.riskManage] },
    { label: 'Audit findings', icon: 'policy', link: '/assurance/audit-findings', permissions: [P.riskRead, P.riskManage] }
  ]},
  { title: 'Reporting', items: [
    { label: 'Reports', icon: 'summarize', link: '/reports', permissions: [P.reportsRead, P.reportsBoard, P.portfolioRead] },
    { label: 'Exceptions', icon: 'crisis_alert', link: '/reports/exceptions', permissions: [P.reportsRead, P.reportsBoard, P.portfolioRead] },
    { label: 'Board packs', icon: 'menu_book', link: '/reports/board-packs', permissions: [P.reportsBoard] },
    { label: 'Analytics & map', icon: 'map', link: '/reports/analytics', permissions: [P.reportsRead, P.reportsBoard, P.portfolioRead] },
    { label: 'Data quality', icon: 'cleaning_services', link: '/reports/data-quality', permissions: [P.reportsRead, P.dataQualityManage] }
  ]},
  { title: 'Supplier portal', items: [
    { label: 'My supplier portal', icon: 'storefront', link: '/portal', permissions: [P.portalAccess] }
  ]},
  { title: 'Administration', items: [
    { label: 'Configuration', icon: 'settings', link: '/admin', permissions: [P.adminConfig, P.adminWorkflow, P.adminDelegations, P.adminImport, P.adminConfigApprove] },
    { label: 'Users & roles', icon: 'manage_accounts', link: '/security', permissions: [P.securityUsers, P.securityRolesApprove] },
    { label: 'Audit trail', icon: 'history', link: '/audit', permissions: [P.auditRead] }
  ]}
];
