namespace Teta.Ippcms.Domain.Security;

/// <summary>TETA role codes (SRS §10 user roles and permission model).</summary>
public static class Roles
{
    public const string Board = "Board";
    public const string ExecutiveAuthority = "ExecutiveAuthority";
    public const string Cfo = "CFO";
    public const string HeadPmo = "HeadPMO";
    public const string HeadScm = "HeadSCM";
    public const string ScmOfficer = "SCMOfficer";
    public const string ProjectManager = "ProjectManager";
    public const string ContractManager = "ContractManager";
    public const string FinanceOfficer = "FinanceOfficer";
    public const string StrategyOfficer = "StrategyOfficer";
    public const string MeOfficer = "MEOfficer";
    public const string RiskCompliance = "RiskCompliance";
    public const string InternalAudit = "InternalAudit";
    public const string Evaluator = "Evaluator";
    public const string Supplier = "Supplier";
    public const string SystemAdministrator = "SystemAdministrator";
    public const string SecurityAdministrator = "SecurityAdministrator";

    public static readonly IReadOnlyList<(string Code, string Name, string Description, bool Privileged, bool Approver)> Definitions = new[]
    {
        (Board, "Accounting Authority / Board", "Read executive portfolio/APP/assurance reports; approve where delegation requires.", false, true),
        (ExecutiveAuthority, "CEO / Executive Authority", "Executive approvals, portfolio oversight, strategic performance.", true, true),
        (Cfo, "Chief Financial Officer", "Budget/financial/SCM oversight and delegated approvals.", true, true),
        (HeadPmo, "Head: PMO / Projects", "Portfolio governance, stage gates, standards and escalations.", false, true),
        (HeadScm, "Head: SCM", "Procurement plan, sourcing, committee administration and award records.", false, true),
        (ScmOfficer, "SCM Officer", "Procurement plan, sourcing, committee administration and award records.", false, false),
        (ProjectManager, "Programme / Project Manager", "Business case, plan, schedule, deliverables, status, risks, certification.", false, false),
        (ContractManager, "Contract Manager", "Contract obligations, performance, variations, expiry and close-out.", false, false),
        (FinanceOfficer, "Finance Officer", "Budget/commitment/invoice/payment validation and ERP reconciliation.", false, true),
        (StrategyOfficer, "Strategy / Performance Officer", "APP hierarchy, targets, evidence verification and reporting.", false, false),
        (MeOfficer, "M&E Officer", "Monitoring plans, visits, findings, evidence and outputs.", false, false),
        (RiskCompliance, "Risk / Compliance", "Risk, control and compliance review and actions.", false, false),
        (InternalAudit, "Internal Audit", "Read-only audit access plus findings/action monitoring.", false, false),
        (Evaluator, "Committee Member / Evaluator", "Time-bound access to assigned procurement evaluation functions.", false, false),
        (Supplier, "Supplier / Implementing Partner", "Portal access to own submissions, contracts, deliverables and invoices.", false, false),
        (SystemAdministrator, "System Administrator", "Technical/configuration administration without business approval authority.", true, false),
        (SecurityAdministrator, "Security Administrator", "Identity/role administration subject to dual control and audit.", true, false)
    };

    /// <summary>Roles whose assignments normally cover the whole organisation rather than specific projects.</summary>
    public static readonly IReadOnlySet<string> OrganisationWide = new HashSet<string>
    {
        Board, ExecutiveAuthority, Cfo, HeadPmo, HeadScm, InternalAudit, RiskCompliance, StrategyOfficer,
        SystemAdministrator, SecurityAdministrator, FinanceOfficer
    };
}

/// <summary>Fine-grained permissions checked by API policies (SEC-003).</summary>
public static class Permissions
{
    public const string StrategyRead = "strategy.read";
    public const string StrategyManage = "strategy.manage";
    public const string StrategyApprove = "strategy.approve";
    public const string PerformanceCapture = "performance.capture";
    public const string PerformanceVerify = "performance.verify";

    public const string PortfolioRead = "portfolio.read";
    public const string PortfolioManage = "portfolio.manage";
    public const string ProjectCreate = "project.create";
    public const string ProjectManage = "project.manage";
    public const string ProjectApprove = "project.approve";
    public const string ProjectGate = "project.gate";

    public const string BudgetManage = "budget.manage";
    public const string BudgetApprove = "budget.approve";

    public const string ProcurementRead = "procurement.read";
    public const string ProcurementManage = "procurement.manage";
    public const string ProcurementEvaluate = "procurement.evaluate";
    public const string ProcurementAdjudicate = "procurement.adjudicate";
    public const string ProcurementAward = "procurement.award";
    public const string ProcurementExceptionApprove = "procurement.exception.approve";

    public const string SupplierRead = "supplier.read";
    public const string SupplierManage = "supplier.manage";

    public const string ContractRead = "contract.read";
    public const string ContractManage = "contract.manage";
    public const string ContractApprove = "contract.approve";

    public const string ExecutionManage = "execution.manage";
    public const string ChangeApprove = "change.approve";

    public const string FinanceRead = "finance.read";
    public const string FinanceManage = "finance.manage";
    public const string FinanceCertify = "finance.certify";
    public const string FinanceErp = "finance.erp";

    public const string MeRead = "me.read";
    public const string MeManage = "me.manage";
    public const string BeneficiaryManage = "beneficiary.manage";
    public const string BeneficiaryPii = "beneficiary.pii";

    public const string RiskRead = "risk.read";
    public const string RiskManage = "risk.manage";
    public const string AssuranceManage = "assurance.manage";
    public const string ComplianceAttest = "compliance.attest";

    public const string EvidenceVerify = "evidence.verify";
    public const string DocumentsRead = "documents.read";
    public const string DocumentsUpload = "documents.upload";

    public const string ReportsRead = "reports.read";
    public const string ReportsExport = "reports.export";
    public const string ReportsBoard = "reports.board";
    public const string DataQualityManage = "dataquality.manage";

    public const string WorkflowDecide = "workflow.decide";
    public const string AdminConfig = "admin.config";
    public const string AdminConfigApprove = "admin.config.approve";
    public const string AdminWorkflow = "admin.workflow";
    public const string AdminDelegations = "admin.delegations";
    public const string AdminImport = "admin.import";
    public const string SecurityUsers = "security.users";
    public const string SecurityRolesApprove = "security.roles.approve";
    public const string AuditRead = "audit.read";
    public const string PortalAccess = "portal.access";

    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    private static readonly string[] ReadAll =
    {
        StrategyRead, PortfolioRead, ProcurementRead, SupplierRead, ContractRead, FinanceRead, MeRead, RiskRead,
        ReportsRead, DocumentsRead
    };

    /// <summary>Default role → permission matrix (seeded; maintainable by administrators).</summary>
    public static IReadOnlyDictionary<string, string[]> Defaults { get; } = new Dictionary<string, string[]>
    {
        [Roles.Board] = ReadAll.Concat(new[] { ReportsBoard, ReportsExport, WorkflowDecide }).ToArray(),
        [Roles.ExecutiveAuthority] = ReadAll.Concat(new[]
        {
            StrategyApprove, ProjectApprove, ProjectGate, BudgetApprove, ProcurementAdjudicate, ProcurementAward,
            ProcurementExceptionApprove, ContractApprove, ChangeApprove, FinanceCertify, ReportsBoard, ReportsExport,
            WorkflowDecide, AuditRead
        }).ToArray(),
        [Roles.Cfo] = ReadAll.Concat(new[]
        {
            ProjectApprove, BudgetManage, BudgetApprove, ProcurementAdjudicate, ProcurementAward,
            ProcurementExceptionApprove, ContractApprove, ChangeApprove, FinanceManage, FinanceCertify, FinanceErp,
            ReportsBoard, ReportsExport, WorkflowDecide, AuditRead, AdminConfigApprove
        }).ToArray(),
        [Roles.HeadPmo] = ReadAll.Concat(new[]
        {
            PortfolioManage, ProjectCreate, ProjectManage, ProjectApprove, ProjectGate, BudgetManage, ExecutionManage,
            ChangeApprove, ContractManage, MeManage, RiskManage, PerformanceCapture, EvidenceVerify, DocumentsUpload,
            ReportsExport, ReportsBoard, WorkflowDecide, DataQualityManage
        }).ToArray(),
        [Roles.HeadScm] = ReadAll.Concat(new[]
        {
            ProcurementManage, ProcurementAward, ProcurementExceptionApprove, SupplierManage, ContractManage,
            DocumentsUpload, ReportsExport, WorkflowDecide, AdminConfigApprove
        }).ToArray(),
        [Roles.ScmOfficer] = ReadAll.Concat(new[]
        {
            ProcurementManage, SupplierManage, DocumentsUpload, ReportsExport, WorkflowDecide
        }).ToArray(),
        [Roles.ProjectManager] = new[]
        {
            StrategyRead, PortfolioRead, ProjectCreate, ProjectManage, BudgetManage, ProcurementRead, SupplierRead,
            ContractRead, ExecutionManage, FinanceRead, MeRead, RiskRead, RiskManage, PerformanceCapture,
            DocumentsRead, DocumentsUpload, ReportsRead, ReportsExport, WorkflowDecide, BeneficiaryManage
        },
        [Roles.ContractManager] = new[]
        {
            PortfolioRead, ProcurementRead, SupplierRead, ContractRead, ContractManage, ExecutionManage, FinanceRead,
            RiskRead, RiskManage, DocumentsRead, DocumentsUpload, ReportsRead, ReportsExport, WorkflowDecide
        },
        [Roles.FinanceOfficer] = ReadAll.Concat(new[]
        {
            BudgetManage, FinanceManage, FinanceCertify, FinanceErp, DocumentsUpload, ReportsExport, WorkflowDecide,
            DataQualityManage
        }).ToArray(),
        [Roles.StrategyOfficer] = ReadAll.Concat(new[]
        {
            StrategyManage, PerformanceCapture, PerformanceVerify, EvidenceVerify, DocumentsUpload, ReportsExport,
            ReportsBoard, WorkflowDecide
        }).ToArray(),
        [Roles.MeOfficer] = new[]
        {
            StrategyRead, PortfolioRead, ContractRead, SupplierRead, MeRead, MeManage, BeneficiaryManage,
            BeneficiaryPii, PerformanceCapture, EvidenceVerify, RiskRead, DocumentsRead, DocumentsUpload, ReportsRead,
            ReportsExport, WorkflowDecide
        },
        [Roles.RiskCompliance] = ReadAll.Concat(new[]
        {
            RiskManage, AssuranceManage, ComplianceAttest, ReportsExport, WorkflowDecide, AuditRead
        }).ToArray(),
        [Roles.InternalAudit] = ReadAll.Concat(new[] { AssuranceManage, AuditRead, ReportsExport }).ToArray(),
        [Roles.Evaluator] = new[] { ProcurementRead, ProcurementEvaluate, SupplierRead, DocumentsRead },
        [Roles.Supplier] = new[] { PortalAccess },
        [Roles.SystemAdministrator] = new[]
        {
            PortfolioRead, ReportsRead, AdminConfig, AdminWorkflow, AdminDelegations, AdminImport, AuditRead,
            DataQualityManage, SupplierRead
        },
        [Roles.SecurityAdministrator] = new[] { SecurityUsers, SecurityRolesApprove, AuditRead }
    };
}

/// <summary>Delegated authority types used by workflow steps and delegation rules (FR-ADM-002).</summary>
public static class AuthorityTypes
{
    public const string ProjectApproval = "ProjectApproval";
    public const string BudgetApproval = "BudgetApproval";
    public const string ProcurementApproval = "ProcurementApproval";
    public const string AwardApproval = "AwardApproval";
    public const string ContractApproval = "ContractApproval";
    public const string VariationApproval = "VariationApproval";
    public const string PaymentCertification = "PaymentCertification";
    public const string ChangeApproval = "ChangeApproval";
    public const string ExceptionApproval = "ExceptionApproval";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ProjectApproval, BudgetApproval, ProcurementApproval, AwardApproval, ContractApproval, VariationApproval,
        PaymentCertification, ChangeApproval, ExceptionApproval
    };
}
