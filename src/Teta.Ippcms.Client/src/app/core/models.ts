export interface CurrentUser {
  userId: string;
  username: string;
  displayName: string;
  email: string;
  roles: string[];
  permissions: string[];
  scopes: { roleCode: string; scopeType: string; scopeId?: string; scopeName?: string }[];
  supplierId?: string;
  mfaEnabled: boolean;
  mfaRequired: boolean;
  inactivityMinutes: number;
}

export interface LoginResult {
  succeeded: boolean;
  accessToken?: string;
  expiresAtUtc?: string;
  challengeToken?: string;
  mfaRequired: boolean;
  mfaEnrolmentRequired: boolean;
  message?: string;
  user?: CurrentUser;
}

export interface MfaEnrolment {
  secret: string;
  otpAuthUri: string;
  challengeToken: string;
}

export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReportTable {
  code: string;
  title: string;
  generatedAtUtc: string;
  columns: string[];
  rows: unknown[][];
  filters: Record<string, string | null>;
}

export interface Kpi {
  code?: string;
  label: string;
  value: number;
  unit?: string | null;
  status: string;
  drillLink?: string;
  link?: string;
}

/** Permission codes (mirror of Teta.Ippcms.Domain.Security.Permissions). */
export const P = {
  strategyRead: 'strategy.read', strategyManage: 'strategy.manage', strategyApprove: 'strategy.approve',
  performanceCapture: 'performance.capture', performanceVerify: 'performance.verify',
  portfolioRead: 'portfolio.read', portfolioManage: 'portfolio.manage', projectCreate: 'project.create', projectManage: 'project.manage',
  projectApprove: 'project.approve', projectGate: 'project.gate', budgetManage: 'budget.manage', budgetApprove: 'budget.approve',
  procurementRead: 'procurement.read', procurementManage: 'procurement.manage', procurementEvaluate: 'procurement.evaluate',
  procurementAdjudicate: 'procurement.adjudicate', procurementAward: 'procurement.award',
  supplierRead: 'supplier.read', supplierManage: 'supplier.manage',
  contractRead: 'contract.read', contractManage: 'contract.manage', contractApprove: 'contract.approve',
  executionManage: 'execution.manage', changeApprove: 'change.approve',
  financeRead: 'finance.read', financeManage: 'finance.manage', financeCertify: 'finance.certify', financeErp: 'finance.erp',
  meRead: 'me.read', meManage: 'me.manage', beneficiaryManage: 'beneficiary.manage', beneficiaryPii: 'beneficiary.pii',
  riskRead: 'risk.read', riskManage: 'risk.manage', assuranceManage: 'assurance.manage', complianceAttest: 'compliance.attest',
  evidenceVerify: 'evidence.verify', documentsRead: 'documents.read', documentsUpload: 'documents.upload',
  reportsRead: 'reports.read', reportsExport: 'reports.export', reportsBoard: 'reports.board', dataQualityManage: 'dataquality.manage',
  workflowDecide: 'workflow.decide', adminConfig: 'admin.config', adminConfigApprove: 'admin.config.approve', adminWorkflow: 'admin.workflow',
  adminDelegations: 'admin.delegations', adminImport: 'admin.import', securityUsers: 'security.users', securityRolesApprove: 'security.roles.approve',
  auditRead: 'audit.read', portalAccess: 'portal.access'
} as const;

/** Role codes (mirror of Teta.Ippcms.Domain.Security.Roles) for delegation/assignment/step pickers. */
export const ROLE_CODES = [
  'Board', 'ExecutiveAuthority', 'CFO', 'HeadPMO', 'HeadSCM', 'SCMOfficer', 'ProjectManager', 'ContractManager',
  'FinanceOfficer', 'StrategyOfficer', 'MEOfficer', 'RiskCompliance', 'InternalAudit', 'Evaluator', 'Supplier',
  'SystemAdministrator', 'SecurityAdministrator'
];

/** Delegated authority types (mirror of Teta.Ippcms.Domain.Security.AuthorityTypes). */
export const AUTHORITY_TYPES = [
  'ProjectApproval', 'BudgetApproval', 'ProcurementApproval', 'AwardApproval', 'ContractApproval', 'VariationApproval',
  'PaymentCertification', 'ChangeApproval', 'ExceptionApproval'
];
