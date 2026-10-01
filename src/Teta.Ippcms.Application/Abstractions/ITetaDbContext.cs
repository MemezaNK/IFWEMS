using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Platform.Security.Identity;
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

namespace Teta.Ippcms.Application.Abstractions;

/// <summary>Persistence abstraction over the TETA schema (plus the shared identity table).</summary>
public interface ITetaDbContext
{
    // Shared identity (dbo.Users, owned by IFWEMS migrations)
    DbSet<PlatformUser> Users { get; }

    // Security
    DbSet<TetaRole> Roles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRoleAssignment> UserRoleAssignments { get; }
    DbSet<UserSecurityProfile> UserSecurityProfiles { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<AuditLog> AuditLogs { get; }

    // Strategy
    DbSet<StrategicPlan> StrategicPlans { get; }
    DbSet<StrategicPlanVersion> StrategicPlanVersions { get; }
    DbSet<StrategicOutcome> StrategicOutcomes { get; }
    DbSet<StrategicObjective> StrategicObjectives { get; }
    DbSet<AppIndicator> AppIndicators { get; }
    DbSet<AppTarget> AppTargets { get; }
    DbSet<ProjectIndicatorLink> ProjectIndicatorLinks { get; }
    DbSet<PerformanceResult> PerformanceResults { get; }

    // Portfolio / projects
    DbSet<Portfolio> Portfolios { get; }
    DbSet<Programme> Programmes { get; }
    DbSet<Project> Projects { get; }
    DbSet<Workstream> Workstreams { get; }
    DbSet<BusinessCase> BusinessCases { get; }
    DbSet<PrioritisationCriterion> PrioritisationCriteria { get; }
    DbSet<ProjectPriorityScore> ProjectPriorityScores { get; }
    DbSet<ProjectCharter> ProjectCharters { get; }
    DbSet<ProjectStakeholder> ProjectStakeholders { get; }
    DbSet<StageGateCriterion> StageGateCriteria { get; }
    DbSet<StageGateReview> StageGateReviews { get; }
    DbSet<StageGateCheck> StageGateChecks { get; }
    DbSet<ProjectStatusHistory> ProjectStatusHistory { get; }

    // Budget
    DbSet<ProjectBudgetLine> BudgetLines { get; }
    DbSet<BudgetRevision> BudgetRevisions { get; }
    DbSet<ProcurementPlanItem> ProcurementPlanItems { get; }
    DbSet<ProcurementMethodRule> ProcurementMethodRules { get; }

    // Procurement
    DbSet<Requisition> Requisitions { get; }
    DbSet<Procurement> Procurements { get; }
    DbSet<Specification> Specifications { get; }
    DbSet<Committee> Committees { get; }
    DbSet<CommitteeMember> CommitteeMembers { get; }
    DbSet<CommitteeMeeting> CommitteeMeetings { get; }
    DbSet<Declaration> Declarations { get; }
    DbSet<Publication> Publications { get; }
    DbSet<Bid> Bids { get; }
    DbSet<EvaluationCriterion> EvaluationCriteria { get; }
    DbSet<EvaluationScore> EvaluationScores { get; }
    DbSet<PricePreferenceSystem> PricePreferenceSystems { get; }
    DbSet<DueDiligenceCheck> DueDiligenceChecks { get; }
    DbSet<Adjudication> Adjudications { get; }
    DbSet<Award> Awards { get; }
    DbSet<BidderCommunication> BidderCommunications { get; }
    DbSet<ProcurementException> ProcurementExceptions { get; }

    // Suppliers
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierUser> SupplierUsers { get; }

    // Contracts
    DbSet<Contract> Contracts { get; }
    DbSet<ContractObligation> ContractObligations { get; }
    DbSet<Deliverable> Deliverables { get; }
    DbSet<PaymentScheduleItem> PaymentScheduleItems { get; }
    DbSet<ContractVariation> ContractVariations { get; }
    DbSet<ContractPerformanceReview> ContractPerformanceReviews { get; }
    DbSet<ContractBreach> ContractBreaches { get; }

    // Execution
    DbSet<WbsElement> WbsElements { get; }
    DbSet<WbsDependency> WbsDependencies { get; }
    DbSet<ScheduleBaseline> ScheduleBaselines { get; }
    DbSet<ProgressUpdate> ProgressUpdates { get; }
    DbSet<ResourceAssignment> ResourceAssignments { get; }
    DbSet<Issue> Issues { get; }
    DbSet<ProjectDependency> ProjectDependencies { get; }
    DbSet<ChangeRequest> ChangeRequests { get; }
    DbSet<ProjectHealthSnapshot> ProjectHealthSnapshots { get; }
    DbSet<Comment> Comments { get; }
    DbSet<ProjectClosure> ProjectClosures { get; }
    DbSet<BenefitReview> BenefitReviews { get; }

    // Finance
    DbSet<Invoice> Invoices { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Commitment> Commitments { get; }
    DbSet<Expenditure> Expenditures { get; }
    DbSet<Accrual> Accruals { get; }
    DbSet<CostForecast> CostForecasts { get; }
    DbSet<ErpInterfaceMessage> ErpInterfaceMessages { get; }
    DbSet<ErpReconciliation> ErpReconciliations { get; }

    // M&E
    DbSet<MePlan> MePlans { get; }
    DbSet<MonitoringTemplate> MonitoringTemplates { get; }
    DbSet<MonitoringVisit> MonitoringVisits { get; }
    DbSet<Finding> Findings { get; }
    DbSet<CorrectiveAction> CorrectiveActions { get; }
    DbSet<Beneficiary> Beneficiaries { get; }
    DbSet<BeneficiaryStatusHistory> BeneficiaryStatusHistory { get; }
    DbSet<LearnerDeliveryTarget> LearnerDeliveryTargets { get; }

    // Risk / assurance
    DbSet<RiskRatingBand> RiskRatingBands { get; }
    DbSet<Risk> Risks { get; }
    DbSet<RiskControl> RiskControls { get; }
    DbSet<ControlAssessment> ControlAssessments { get; }
    DbSet<RiskTreatment> RiskTreatments { get; }
    DbSet<ComplianceObligation> ComplianceObligations { get; }
    DbSet<ComplianceAttestation> ComplianceAttestations { get; }
    DbSet<AuditFinding> AuditFindings { get; }
    DbSet<AssuranceCoverage> AssuranceCoverage { get; }

    // Workflow / admin
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowStepDefinition> WorkflowStepDefinitions { get; }
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<WorkflowTask> WorkflowTasks { get; }
    DbSet<Delegation> Delegations { get; }
    DbSet<Substitution> Substitutions { get; }
    DbSet<SodRule> SodRules { get; }
    DbSet<TransactionAction> TransactionActions { get; }
    DbSet<EscalationEvent> EscalationEvents { get; }
    DbSet<ReferenceDataItem> ReferenceData { get; }
    DbSet<PublicHoliday> PublicHolidays { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<NotificationTemplate> NotificationTemplates { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ImportJob> ImportJobs { get; }
    DbSet<RetentionPolicy> RetentionPolicies { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<NumberSequence> NumberSequences { get; }

    // Documents
    DbSet<DocumentRecord> Documents { get; }
    DbSet<Evidence> Evidence { get; }

    // Reporting
    DbSet<BoardReportPack> BoardReportPacks { get; }
    DbSet<ReportSchedule> ReportSchedules { get; }
    DbSet<DataQualityIssue> DataQualityIssues { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
