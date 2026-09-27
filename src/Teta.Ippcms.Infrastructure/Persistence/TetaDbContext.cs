using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Platform.Security.Identity;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Domain.Admin;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Budget;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Documents;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Reporting;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Strategy;
using Teta.Ippcms.Domain.Suppliers;
using Teta.Ippcms.Domain.Workflow;
using EvidenceEntity = Teta.Ippcms.Domain.Documents.Evidence;

namespace Teta.Ippcms.Infrastructure.Persistence;

/// <summary>
/// TETA-IPPCMS database context. All TETA tables live in the "teta" schema of the shared platform
/// database; the shared identity table dbo.Users is mapped (for login and user look-ups) but is
/// excluded from TETA migrations because IFWEMS owns it.
/// </summary>
public class TetaDbContext : DbContext, ITetaDbContext
{
    public const string Schema = "teta";

    public TetaDbContext(DbContextOptions<TetaDbContext> options) : base(options)
    {
    }

    public DbSet<PlatformUser> Users => Set<PlatformUser>();

    public DbSet<TetaRole> Roles => Set<TetaRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<UserSecurityProfile> UserSecurityProfiles => Set<UserSecurityProfile>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<StrategicPlan> StrategicPlans => Set<StrategicPlan>();
    public DbSet<StrategicPlanVersion> StrategicPlanVersions => Set<StrategicPlanVersion>();
    public DbSet<StrategicOutcome> StrategicOutcomes => Set<StrategicOutcome>();
    public DbSet<StrategicObjective> StrategicObjectives => Set<StrategicObjective>();
    public DbSet<AppIndicator> AppIndicators => Set<AppIndicator>();
    public DbSet<AppTarget> AppTargets => Set<AppTarget>();
    public DbSet<ProjectIndicatorLink> ProjectIndicatorLinks => Set<ProjectIndicatorLink>();
    public DbSet<PerformanceResult> PerformanceResults => Set<PerformanceResult>();

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Programme> Programmes => Set<Programme>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Workstream> Workstreams => Set<Workstream>();
    public DbSet<BusinessCase> BusinessCases => Set<BusinessCase>();
    public DbSet<PrioritisationCriterion> PrioritisationCriteria => Set<PrioritisationCriterion>();
    public DbSet<ProjectPriorityScore> ProjectPriorityScores => Set<ProjectPriorityScore>();
    public DbSet<ProjectCharter> ProjectCharters => Set<ProjectCharter>();
    public DbSet<ProjectStakeholder> ProjectStakeholders => Set<ProjectStakeholder>();
    public DbSet<StageGateCriterion> StageGateCriteria => Set<StageGateCriterion>();
    public DbSet<StageGateReview> StageGateReviews => Set<StageGateReview>();
    public DbSet<StageGateCheck> StageGateChecks => Set<StageGateCheck>();
    public DbSet<ProjectStatusHistory> ProjectStatusHistory => Set<ProjectStatusHistory>();

    public DbSet<ProjectBudgetLine> BudgetLines => Set<ProjectBudgetLine>();
    public DbSet<BudgetRevision> BudgetRevisions => Set<BudgetRevision>();
    public DbSet<ProcurementPlanItem> ProcurementPlanItems => Set<ProcurementPlanItem>();
    public DbSet<ProcurementMethodRule> ProcurementMethodRules => Set<ProcurementMethodRule>();

    public DbSet<Requisition> Requisitions => Set<Requisition>();
    public DbSet<Procurement> Procurements => Set<Procurement>();
    public DbSet<Specification> Specifications => Set<Specification>();
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<CommitteeMember> CommitteeMembers => Set<CommitteeMember>();
    public DbSet<CommitteeMeeting> CommitteeMeetings => Set<CommitteeMeeting>();
    public DbSet<Declaration> Declarations => Set<Declaration>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<EvaluationCriterion> EvaluationCriteria => Set<EvaluationCriterion>();
    public DbSet<EvaluationScore> EvaluationScores => Set<EvaluationScore>();
    public DbSet<PricePreferenceSystem> PricePreferenceSystems => Set<PricePreferenceSystem>();
    public DbSet<DueDiligenceCheck> DueDiligenceChecks => Set<DueDiligenceCheck>();
    public DbSet<Adjudication> Adjudications => Set<Adjudication>();
    public DbSet<Award> Awards => Set<Award>();
    public DbSet<BidderCommunication> BidderCommunications => Set<BidderCommunication>();
    public DbSet<ProcurementException> ProcurementExceptions => Set<ProcurementException>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierUser> SupplierUsers => Set<SupplierUser>();

    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractObligation> ContractObligations => Set<ContractObligation>();
    public DbSet<Deliverable> Deliverables => Set<Deliverable>();
    public DbSet<PaymentScheduleItem> PaymentScheduleItems => Set<PaymentScheduleItem>();
    public DbSet<ContractVariation> ContractVariations => Set<ContractVariation>();
    public DbSet<ContractPerformanceReview> ContractPerformanceReviews => Set<ContractPerformanceReview>();
    public DbSet<ContractBreach> ContractBreaches => Set<ContractBreach>();

    public DbSet<WbsElement> WbsElements => Set<WbsElement>();
    public DbSet<WbsDependency> WbsDependencies => Set<WbsDependency>();
    public DbSet<ScheduleBaseline> ScheduleBaselines => Set<ScheduleBaseline>();
    public DbSet<ProgressUpdate> ProgressUpdates => Set<ProgressUpdate>();
    public DbSet<ResourceAssignment> ResourceAssignments => Set<ResourceAssignment>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<ProjectDependency> ProjectDependencies => Set<ProjectDependency>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ProjectHealthSnapshot> ProjectHealthSnapshots => Set<ProjectHealthSnapshot>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ProjectClosure> ProjectClosures => Set<ProjectClosure>();
    public DbSet<BenefitReview> BenefitReviews => Set<BenefitReview>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Commitment> Commitments => Set<Commitment>();
    public DbSet<Expenditure> Expenditures => Set<Expenditure>();
    public DbSet<Accrual> Accruals => Set<Accrual>();
    public DbSet<CostForecast> CostForecasts => Set<CostForecast>();
    public DbSet<ErpInterfaceMessage> ErpInterfaceMessages => Set<ErpInterfaceMessage>();
    public DbSet<ErpReconciliation> ErpReconciliations => Set<ErpReconciliation>();

    public DbSet<MePlan> MePlans => Set<MePlan>();
    public DbSet<MonitoringTemplate> MonitoringTemplates => Set<MonitoringTemplate>();
    public DbSet<MonitoringVisit> MonitoringVisits => Set<MonitoringVisit>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<BeneficiaryStatusHistory> BeneficiaryStatusHistory => Set<BeneficiaryStatusHistory>();

    public DbSet<RiskRatingBand> RiskRatingBands => Set<RiskRatingBand>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<RiskControl> RiskControls => Set<RiskControl>();
    public DbSet<ControlAssessment> ControlAssessments => Set<ControlAssessment>();
    public DbSet<RiskTreatment> RiskTreatments => Set<RiskTreatment>();
    public DbSet<ComplianceObligation> ComplianceObligations => Set<ComplianceObligation>();
    public DbSet<ComplianceAttestation> ComplianceAttestations => Set<ComplianceAttestation>();
    public DbSet<AuditFinding> AuditFindings => Set<AuditFinding>();
    public DbSet<AssuranceCoverage> AssuranceCoverage => Set<AssuranceCoverage>();

    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStepDefinition> WorkflowStepDefinitions => Set<WorkflowStepDefinition>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<Delegation> Delegations => Set<Delegation>();
    public DbSet<Substitution> Substitutions => Set<Substitution>();
    public DbSet<SodRule> SodRules => Set<SodRule>();
    public DbSet<TransactionAction> TransactionActions => Set<TransactionAction>();
    public DbSet<EscalationEvent> EscalationEvents => Set<EscalationEvent>();
    public DbSet<ReferenceDataItem> ReferenceData => Set<ReferenceDataItem>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
    public DbSet<EvidenceEntity> Evidence => Set<EvidenceEntity>();

    public DbSet<BoardReportPack> BoardReportPacks => Set<BoardReportPack>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();
    public DbSet<DataQualityIssue> DataQualityIssues => Set<DataQualityIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapSharedPlatformUsers();
        TetaModel.Configure(modelBuilder);
    }
}

/// <summary>Model configuration kept separate so the context stays readable.</summary>
internal static class TetaModel
{
    public static void Configure(ModelBuilder b)
    {
        // ---- Keys, unique business identifiers and indexes (SRS §7.1, §25) ----
        b.Entity<AuditLog>(e =>
        {
            // The append-only trigger is created by the initial migration; declaring it stops EF using OUTPUT without INTO.
            e.ToTable(t => t.HasTrigger("TR_AuditLogs_AppendOnly"));
            e.HasKey(x => x.Id);
            e.Property(x => x.CorrelationId).HasMaxLength(100);
            e.Property(x => x.Module).HasMaxLength(50);
            e.Property(x => x.EntityType).HasMaxLength(80);
            e.Property(x => x.EntityId).HasMaxLength(80);
            e.Property(x => x.Action).HasMaxLength(80);
            e.Property(x => x.Username).HasMaxLength(256);
            e.Property(x => x.SourceIp).HasMaxLength(64);
            e.Property(x => x.Seal).HasMaxLength(64);
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc });
            e.HasIndex(x => x.OccurredAtUtc);
            e.HasIndex(x => x.UserId);
        });

        Unique<TetaRole>(b, x => x.Code);
        b.Entity<RolePermission>().HasIndex(x => new { x.RoleCode, x.Permission }).IsUnique();
        b.Entity<UserRoleAssignment>().HasIndex(x => new { x.UserId, x.Status });
        Unique<UserSecurityProfile>(b, x => x.UserId);
        b.Entity<UserSession>().HasIndex(x => x.UserId);

        Unique<StrategicPlan>(b, x => x.Code);
        b.Entity<StrategicPlanVersion>().HasIndex(x => new { x.PlanId, x.VersionNumber }).IsUnique();
        b.Entity<StrategicOutcome>().HasIndex(x => new { x.PlanId, x.Code }).IsUnique();
        b.Entity<StrategicObjective>().HasIndex(x => new { x.PlanId, x.Code }).IsUnique();
        b.Entity<AppIndicator>().HasIndex(x => new { x.ObjectiveId, x.Code }).IsUnique();
        b.Entity<AppTarget>().HasIndex(x => new { x.IndicatorId, x.FinancialYear, x.Quarter }).IsUnique();
        b.Entity<ProjectIndicatorLink>().HasIndex(x => new { x.ProjectId, x.IndicatorId }).IsUnique();
        b.Entity<PerformanceResult>().HasIndex(x => new { x.IndicatorId, x.FinancialYear, x.Quarter });

        Unique<Portfolio>(b, x => x.Code);
        Unique<Programme>(b, x => x.Code);
        Unique<Project>(b, x => x.DraftReference);
        b.Entity<Project>().HasIndex(x => x.ProjectNumber).IsUnique().HasFilter("[ProjectNumber] IS NOT NULL");
        b.Entity<Project>().HasIndex(x => new { x.ProgrammeId, x.Status });
        b.Entity<ProjectPriorityScore>().HasIndex(x => new { x.ProjectId, x.CriterionId }).IsUnique();
        Unique<PrioritisationCriterion>(b, x => x.Code);
        b.Entity<StageGateCriterion>().HasIndex(x => new { x.Stage, x.Code }).IsUnique();
        b.Entity<ProjectStatusHistory>().HasIndex(x => x.ProjectId);

        b.Entity<ProjectBudgetLine>().HasIndex(x => new { x.ProjectId, x.FinancialYear });
        b.Entity<ProcurementPlanItem>().HasIndex(x => new { x.FinancialYear, x.ProjectId });
        b.Entity<ProcurementMethodRule>().HasIndex(x => new { x.MethodCode, x.RuleVersion }).IsUnique();

        Unique<Requisition>(b, x => x.Number);
        Unique<Procurement>(b, x => x.Number);
        Unique<Award>(b, x => x.Number);
        Unique<ProcurementException>(b, x => x.Number);
        b.Entity<Declaration>().HasIndex(x => new { x.ProcurementId, x.UserId }).IsUnique();
        b.Entity<Bid>().HasIndex(x => new { x.ProcurementId, x.SupplierId });
        b.Entity<EvaluationScore>().HasIndex(x => new { x.BidId, x.CriterionId, x.EvaluatorUserId }).IsUnique();
        b.Entity<PricePreferenceSystem>().HasIndex(x => new { x.Code, x.EffectiveFrom }).IsUnique();
        b.Entity<CommitteeMember>().HasIndex(x => new { x.CommitteeId, x.UserId }).IsUnique();

        Unique<Supplier>(b, x => x.SupplierNumber);
        b.Entity<Supplier>().HasIndex(x => x.RegistrationNumber).IsUnique().HasFilter("[RegistrationNumber] IS NOT NULL");
        b.Entity<Supplier>().HasIndex(x => x.CsdNumber).IsUnique().HasFilter("[CsdNumber] IS NOT NULL");
        b.Entity<Supplier>().HasIndex(x => x.NormalizedName);
        b.Entity<SupplierUser>().HasIndex(x => new { x.SupplierId, x.UserId }).IsUnique();

        Unique<Contract>(b, x => x.ContractNumber);
        Unique<ContractVariation>(b, x => x.Number);
        Unique<Deliverable>(b, x => x.Number);
        b.Entity<Contract>().HasIndex(x => new { x.Status, x.CurrentEndDate });

        b.Entity<WbsElement>().HasIndex(x => new { x.ProjectId, x.ParentId });
        b.Entity<ScheduleBaseline>().HasIndex(x => new { x.ProjectId, x.BaselineNumber }).IsUnique();
        b.Entity<ProgressUpdate>().HasIndex(x => x.WbsElementId);
        Unique<Issue>(b, x => x.Number);
        Unique<ChangeRequest>(b, x => x.Number);
        b.Entity<ProjectHealthSnapshot>().HasIndex(x => new { x.ProjectId, x.CalculatedAtUtc });
        b.Entity<Comment>().HasIndex(x => new { x.ParentType, x.ParentId });

        Unique<Invoice>(b, x => x.Number);
        b.Entity<Invoice>().HasIndex(x => new { x.SupplierId, x.SupplierInvoiceNumber });
        b.Entity<Invoice>().HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        b.Entity<Payment>().HasIndex(x => x.ErpReference).IsUnique();
        b.Entity<Expenditure>().HasIndex(x => x.ErpReference).IsUnique();
        b.Entity<ErpInterfaceMessage>().HasIndex(x => new { x.MessageType, x.ExternalReference });
        b.Entity<ErpInterfaceMessage>().HasIndex(x => x.Status);

        b.Entity<MonitoringTemplate>().HasIndex(x => new { x.Code, x.TemplateVersion }).IsUnique();
        Unique<MonitoringVisit>(b, x => x.Number);
        Unique<Finding>(b, x => x.Number);
        Unique<CorrectiveAction>(b, x => x.Number);
        b.Entity<CorrectiveAction>().HasIndex(x => new { x.ParentType, x.ParentId });
        Unique<Beneficiary>(b, x => x.Number);
        b.Entity<Beneficiary>().HasIndex(x => x.IdentifierHash);

        Unique<Risk>(b, x => x.Number);
        b.Entity<Risk>().HasIndex(x => new { x.ParentType, x.ParentId });
        Unique<ComplianceObligation>(b, x => x.Code);
        Unique<AuditFinding>(b, x => x.Number);

        b.Entity<WorkflowDefinition>().HasIndex(x => new { x.Code, x.DefinitionVersion }).IsUnique();
        b.Entity<WorkflowInstance>().HasIndex(x => new { x.EntityType, x.EntityId });
        b.Entity<WorkflowTask>().HasIndex(x => new { x.Decision, x.AssignedRole });
        b.Entity<TransactionAction>().HasIndex(x => new { x.EntityType, x.EntityId });
        Unique<SodRule>(b, x => x.Code);
        b.Entity<ReferenceDataItem>().HasIndex(x => new { x.Category, x.Code }).IsUnique();
        Unique<PublicHoliday>(b, x => x.Date);
        Unique<SystemSetting>(b, x => x.Key);
        Unique<NotificationTemplate>(b, x => x.Code);
        b.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead });
        b.Entity<OutboxMessage>().HasIndex(x => x.ProcessedAtUtc);
        Unique<IdempotencyRecord>(b, x => x.Key);
        b.Entity<NumberSequence>().HasIndex(x => new { x.Prefix, x.Year }).IsUnique();
        b.Entity<NumberSequence>().Property(x => x.Version).IsConcurrencyToken();
        Unique<RetentionPolicy>(b, x => x.RecordClass);

        b.Entity<DocumentRecord>().HasIndex(x => new { x.ParentType, x.ParentId });
        b.Entity<EvidenceEntity>().HasIndex(x => new { x.ParentType, x.ParentId });

        Unique<BoardReportPack>(b, x => x.Number);
        b.Entity<DataQualityIssue>().HasIndex(x => new { x.RuleCode, x.EntityId });

        // ---- Aggregate children (navigations) ----
        b.Entity<StrategicPlan>().HasMany(x => x.Outcomes).WithOne().HasForeignKey(x => x.PlanId);
        b.Entity<StrategicPlan>().HasMany(x => x.Objectives).WithOne().HasForeignKey(x => x.PlanId);
        b.Entity<StrategicPlan>().HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.PlanId);
        b.Entity<StrategicObjective>().HasMany(x => x.Indicators).WithOne().HasForeignKey(x => x.ObjectiveId);
        b.Entity<AppIndicator>().HasMany(x => x.Targets).WithOne().HasForeignKey(x => x.IndicatorId);
        b.Entity<Portfolio>().HasMany(x => x.Programmes).WithOne().HasForeignKey(x => x.PortfolioId);
        b.Entity<StageGateReview>().HasMany(x => x.Checks).WithOne().HasForeignKey(x => x.ReviewId);
        b.Entity<Committee>().HasMany(x => x.Members).WithOne().HasForeignKey(x => x.CommitteeId);
        b.Entity<WorkflowDefinition>().HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.DefinitionId);
        b.Entity<WorkflowInstance>().HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.InstanceId);

        // ---- Referential integrity along the strategy-to-execution chain (SRS §25) ----
        Fk<StrategicObjective, StrategicOutcome>(b, x => x.OutcomeId);
        Fk<ProjectIndicatorLink, Project>(b, x => x.ProjectId);
        Fk<ProjectIndicatorLink, AppIndicator>(b, x => x.IndicatorId);
        Fk<PerformanceResult, AppIndicator>(b, x => x.IndicatorId);
        Fk<PerformanceResult, Project>(b, x => x.ProjectId);
        Fk<Project, Programme>(b, x => x.ProgrammeId);
        Fk<Workstream, Project>(b, x => x.ProjectId);
        Fk<BusinessCase, Project>(b, x => x.ProjectId);
        Fk<ProjectCharter, Project>(b, x => x.ProjectId);
        Fk<ProjectStakeholder, Project>(b, x => x.ProjectId);
        Fk<StageGateReview, Project>(b, x => x.ProjectId);
        Fk<ProjectStatusHistory, Project>(b, x => x.ProjectId);
        Fk<ProjectPriorityScore, Project>(b, x => x.ProjectId);
        Fk<ProjectPriorityScore, PrioritisationCriterion>(b, x => x.CriterionId);
        Fk<ProjectBudgetLine, Project>(b, x => x.ProjectId);
        Fk<BudgetRevision, ProjectBudgetLine>(b, x => x.BudgetLineId);
        Fk<ProcurementPlanItem, Project>(b, x => x.ProjectId);
        Fk<Requisition, Project>(b, x => x.ProjectId);
        Fk<Requisition, ProjectBudgetLine>(b, x => x.BudgetLineId);
        Fk<Procurement, Project>(b, x => x.ProjectId);
        Fk<Procurement, Requisition>(b, x => x.RequisitionId);
        Fk<Specification, Procurement>(b, x => x.ProcurementId);
        Fk<Declaration, Procurement>(b, x => x.ProcurementId);
        Fk<Publication, Procurement>(b, x => x.ProcurementId);
        Fk<Bid, Procurement>(b, x => x.ProcurementId);
        Fk<Bid, Supplier>(b, x => x.SupplierId);
        Fk<EvaluationCriterion, Procurement>(b, x => x.ProcurementId);
        Fk<EvaluationScore, Bid>(b, x => x.BidId);
        Fk<EvaluationScore, EvaluationCriterion>(b, x => x.CriterionId);
        Fk<DueDiligenceCheck, Bid>(b, x => x.BidId);
        Fk<Adjudication, Procurement>(b, x => x.ProcurementId);
        Fk<Award, Procurement>(b, x => x.ProcurementId);
        Fk<Award, Bid>(b, x => x.BidId);
        Fk<Award, Supplier>(b, x => x.SupplierId);
        Fk<BidderCommunication, Procurement>(b, x => x.ProcurementId);
        Fk<Contract, Project>(b, x => x.ProjectId);
        Fk<Contract, Supplier>(b, x => x.SupplierId);
        Fk<Contract, Award>(b, x => x.AwardId);
        Fk<ContractObligation, Contract>(b, x => x.ContractId);
        Fk<Deliverable, Project>(b, x => x.ProjectId);
        Fk<Deliverable, Contract>(b, x => x.ContractId);
        Fk<PaymentScheduleItem, Contract>(b, x => x.ContractId);
        Fk<ContractVariation, Contract>(b, x => x.ContractId);
        Fk<ContractPerformanceReview, Contract>(b, x => x.ContractId);
        Fk<ContractBreach, Contract>(b, x => x.ContractId);
        Fk<WbsElement, Project>(b, x => x.ProjectId);
        Fk<WbsElement, WbsElement>(b, x => x.ParentId);
        Fk<WbsDependency, WbsElement>(b, x => x.PredecessorId);
        Fk<WbsDependency, WbsElement>(b, x => x.SuccessorId);
        Fk<ScheduleBaseline, Project>(b, x => x.ProjectId);
        Fk<ProgressUpdate, WbsElement>(b, x => x.WbsElementId);
        Fk<ResourceAssignment, WbsElement>(b, x => x.WbsElementId);
        Fk<Issue, Project>(b, x => x.ProjectId);
        Fk<ProjectDependency, Project>(b, x => x.ProjectId);
        Fk<ChangeRequest, Project>(b, x => x.ProjectId);
        Fk<ProjectClosure, Project>(b, x => x.ProjectId);
        Fk<BenefitReview, Project>(b, x => x.ProjectId);
        Fk<Invoice, Contract>(b, x => x.ContractId);
        Fk<Invoice, Supplier>(b, x => x.SupplierId);
        Fk<Invoice, Project>(b, x => x.ProjectId);
        Fk<Invoice, Deliverable>(b, x => x.DeliverableId);
        Fk<Payment, Invoice>(b, x => x.InvoiceId);
        Fk<Commitment, Project>(b, x => x.ProjectId);
        Fk<Expenditure, Project>(b, x => x.ProjectId);
        Fk<Accrual, Project>(b, x => x.ProjectId);
        Fk<CostForecast, Project>(b, x => x.ProjectId);
        Fk<MePlan, Project>(b, x => x.ProjectId);
        Fk<MonitoringVisit, Project>(b, x => x.ProjectId);
        Fk<Finding, Project>(b, x => x.ProjectId);
        Fk<Beneficiary, Project>(b, x => x.ProjectId);
        Fk<BeneficiaryStatusHistory, Beneficiary>(b, x => x.BeneficiaryId);
        Fk<RiskControl, Risk>(b, x => x.RiskId);
        Fk<ControlAssessment, RiskControl>(b, x => x.ControlId);
        Fk<RiskTreatment, Risk>(b, x => x.RiskId);
        Fk<ComplianceAttestation, ComplianceObligation>(b, x => x.ObligationId);
        Fk<WorkflowInstance, WorkflowDefinition>(b, x => x.DefinitionId);
        Fk<EvidenceEntity, DocumentRecord>(b, x => x.DocumentId);

        // ---- Conventions ----
        foreach (var entityType in b.Model.GetEntityTypes().ToList())
        {
            var clr = entityType.ClrType;

            // Optimistic concurrency on all auditable business records (NFR-004).
            if (typeof(AuditableEntity).IsAssignableFrom(clr))
            {
                b.Entity(clr).Property(nameof(AuditableEntity.Version)).IsConcurrencyToken();
                b.Entity(clr).Property(nameof(AuditableEntity.CreatedBy)).HasMaxLength(256);
                b.Entity(clr).Property(nameof(AuditableEntity.UpdatedBy)).HasMaxLength(256);
            }

            foreach (var property in entityType.GetProperties().ToList())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum)
                {
                    // Enums are stored as readable strings (reports, audit, integrations).
                    b.Entity(clr).Property(property.Name).HasConversion<string>().HasMaxLength(40);
                }
                else if (type == typeof(decimal))
                {
                    // Currency and precision stored consistently (SRS §7.1).
                    b.Entity(clr).Property(property.Name).HasPrecision(18, 2);
                }
                else if (type == typeof(string) && property.GetMaxLength() is null && IsIndexed(entityType, property))
                {
                    b.Entity(clr).Property(property.Name).HasMaxLength(200);
                }
            }

            // Business records are never cascade-deleted; lifecycle status is used instead (SRS §25).
            foreach (var fk in entityType.GetForeignKeys())
            {
                fk.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }

        // Weights and fractional values that need more than 2 decimals.
        b.Entity<ProjectIndicatorLink>().Property(x => x.Weight).HasPrecision(18, 4);
        b.Entity<ProjectIndicatorLink>().Property(x => x.PlannedContribution).HasPrecision(18, 4);
        b.Entity<PerformanceResult>().Property(x => x.Value).HasPrecision(18, 4);
        b.Entity<AppTarget>().Property(x => x.TargetValue).HasPrecision(18, 4);
        b.Entity<AppTarget>().Property(x => x.ForecastValue).HasPrecision(18, 4);
        b.Entity<MonitoringVisit>().Property(x => x.Latitude).HasPrecision(9, 6);
        b.Entity<MonitoringVisit>().Property(x => x.Longitude).HasPrecision(9, 6);
    }

    private static bool IsIndexed(IMutableEntityType entityType, IMutableProperty property) =>
        entityType.GetIndexes().Any(i => i.Properties.Contains(property));

    private static void Unique<T>(ModelBuilder b, System.Linq.Expressions.Expression<Func<T, object?>> key) where T : class =>
        b.Entity<T>().HasIndex(key).IsUnique();

    private static void Fk<TDependent, TPrincipal>(ModelBuilder b, System.Linq.Expressions.Expression<Func<TDependent, object?>> fk)
        where TDependent : class where TPrincipal : class =>
        b.Entity<TDependent>().HasOne<TPrincipal>().WithMany().HasForeignKey(fk).OnDelete(DeleteBehavior.Restrict);
}
