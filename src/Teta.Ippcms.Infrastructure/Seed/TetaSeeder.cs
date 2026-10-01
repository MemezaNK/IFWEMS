using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Platform.Core;
using Platform.Security.Crypto;
using Platform.Security.Identity;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Engines;
using Teta.Ippcms.Application.Projects;
using Teta.Ippcms.Domain.Admin;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Budget;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Execution;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Domain.Monitoring;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Strategy;
using Teta.Ippcms.Domain.Suppliers;
using Teta.Ippcms.Domain.Workflow;
using Teta.Ippcms.Infrastructure.Persistence;
using EvidenceEntity = Teta.Ippcms.Domain.Documents.Evidence;

namespace Teta.Ippcms.Infrastructure.Seed;

/// <summary>
/// Idempotent configuration seed: roles and permissions, workflow definitions, delegations, SoD
/// rules, reference data, settings, templates, stage-gate criteria, rating bands, procurement
/// method rules, preference-point systems, public holidays and retention classes. Existing rows are
/// never overwritten, so administrators' changes survive redeployments. Values are defaults for
/// TETA to confirm during configuration workshops (see docs/teta/CONFIGURATION.md).
/// Optional: bootstrap administrators (existing shared users) and demo data for UAT environments.
/// </summary>
public sealed class TetaSeeder
{
    private const decimal Unlimited = 999_999_999_999m;

    private readonly TetaDbContext _db;
    private readonly IConfiguration _config;
    private readonly IPlatformPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IFieldProtector _fieldProtector;
    private readonly ILogger<TetaSeeder> _logger;

    public TetaSeeder(TetaDbContext db, IConfiguration config, IPlatformPasswordHasher hasher, IClock clock, IFieldProtector fieldProtector, ILogger<TetaSeeder> logger)
    {
        _db = db;
        _config = config;
        _hasher = hasher;
        _clock = clock;
        _fieldProtector = fieldProtector;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync(ct);
        await SeedWorkflowsAsync(ct);
        await SeedDelegationsAsync(ct);
        await SeedSodRulesAsync(ct);
        await SeedReferenceDataAsync(ct);
        await SeedSettingsAndTemplatesAsync(ct);
        await SeedProjectConfigurationAsync(ct);
        await SeedProcurementConfigurationAsync(ct);
        await SeedCalendarAndRetentionAsync(ct);
        await _db.SaveChangesAsync(ct);

        await BootstrapAdministratorsAsync(ct);
        if (_config.GetValue("Seed:DemoData", false)) await SeedDemoDataAsync(ct);
    }

    // ---------------- Roles & permissions ----------------
    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var existing = await _db.Roles.Select(r => r.Code).ToListAsync(ct);
        foreach (var (code, name, description, privileged, approver) in Roles.Definitions.Where(d => !existing.Contains(d.Code)))
        {
            _db.Roles.Add(new TetaRole { Code = code, Name = name, Description = description, IsPrivileged = privileged, HasApprovalAuthority = approver });
            foreach (var permission in Permissions.Defaults.GetValueOrDefault(code) ?? Array.Empty<string>())
                _db.RolePermissions.Add(new RolePermission { RoleCode = code, Permission = permission });
        }
    }

    // ---------------- Workflows (FR-ADM-001) ----------------
    private sealed record StepSeed(int Order, string Code, string Name, string Role, string? Authority = null, decimal? Min = null, decimal? Max = null,
        int Sla = 72, string? Escalation = null);

    private static readonly (string Code, string Name, string EntityType, StepSeed[] Steps)[] Workflows =
    {
        ("BUSINESS_CASE_APPROVAL", "Business case approval", "BusinessCase", new[]
        {
            new StepSeed(10, "PMO_REVIEW", "PMO review", Roles.HeadPmo, Escalation: Roles.ExecutiveAuthority),
            new StepSeed(20, "CFO_BUDGET", "CFO budget confirmation", Roles.Cfo, Escalation: Roles.ExecutiveAuthority),
            new StepSeed(30, "EXEC_APPROVE", "Executive approval", Roles.ExecutiveAuthority, AuthorityTypes.ProjectApproval, Sla: 120),
            new StepSeed(40, "BOARD_APPROVE", "Board approval", Roles.Board, Min: 50_000_000.01m, Sla: 240)
        }),
        ("STRATEGIC_PLAN_APPROVAL", "Strategic plan / APP approval", "StrategicPlan", new[]
        {
            new StepSeed(10, "PMO_REVIEW", "Strategy/PMO review", Roles.HeadPmo),
            new StepSeed(20, "EXEC_APPROVE", "Executive approval", Roles.ExecutiveAuthority, Sla: 120),
            new StepSeed(30, "BOARD_APPROVE", "Board approval", Roles.Board, Sla: 240)
        }),
        ("APP_TARGET_APPROVAL", "APP target approval", "AppTarget", new[]
        {
            new StepSeed(10, "PMO_REVIEW", "PMO review", Roles.HeadPmo),
            new StepSeed(20, "EXEC_APPROVE", "Executive approval", Roles.ExecutiveAuthority, Sla: 120)
        }),
        ("REQUISITION_APPROVAL", "Procurement requisition approval", "Requisition", new[]
        {
            new StepSeed(10, "FIN_BUDGET", "Finance budget confirmation", Roles.FinanceOfficer, Escalation: Roles.Cfo),
            new StepSeed(20, "SCM_APPROVE", "Head SCM approval", Roles.HeadScm, AuthorityTypes.ProcurementApproval, Max: 1_000_000m, Escalation: Roles.Cfo),
            new StepSeed(21, "CFO_APPROVE", "CFO approval", Roles.Cfo, AuthorityTypes.ProcurementApproval, Min: 1_000_000.01m, Max: 10_000_000m),
            new StepSeed(22, "EXEC_APPROVE", "Executive approval", Roles.ExecutiveAuthority, AuthorityTypes.ProcurementApproval, Min: 10_000_000.01m)
        }),
        ("PROCUREMENT_ADJUDICATION", "Bid adjudication and award", "Procurement", new[]
        {
            new StepSeed(10, "BAC_RECOMMEND", "Adjudication committee recommendation", Roles.HeadScm, Escalation: Roles.Cfo),
            new StepSeed(20, "CFO_AWARD", "CFO award decision", Roles.Cfo, AuthorityTypes.AwardApproval, Max: 10_000_000m, Sla: 96),
            new StepSeed(21, "EXEC_AWARD", "Accounting officer award decision", Roles.ExecutiveAuthority, AuthorityTypes.AwardApproval, Min: 10_000_000.01m, Sla: 120)
        }),
        ("PROCUREMENT_EXCEPTION", "Procurement deviation / exception", "ProcurementException", new[]
        {
            new StepSeed(10, "SCM_REVIEW", "Head SCM review", Roles.HeadScm),
            new StepSeed(20, "CFO_APPROVE", "CFO approval", Roles.Cfo, AuthorityTypes.ExceptionApproval, Max: 1_000_000m),
            new StepSeed(21, "EXEC_APPROVE", "Accounting officer approval", Roles.ExecutiveAuthority, AuthorityTypes.ExceptionApproval, Min: 1_000_000.01m)
        }),
        ("CONTRACT_VARIATION", "Contract variation / extension", "ContractVariation", new[]
        {
            new StepSeed(10, "SCM_REVIEW", "SCM review", Roles.HeadScm),
            new StepSeed(20, "CFO_APPROVE", "CFO approval", Roles.Cfo, AuthorityTypes.VariationApproval, Max: 5_000_000m),
            new StepSeed(21, "EXEC_APPROVE", "Accounting officer approval", Roles.ExecutiveAuthority, AuthorityTypes.VariationApproval, Min: 5_000_000.01m)
        }),
        ("CHANGE_REQUEST", "Project change request", "ChangeRequest", new[]
        {
            new StepSeed(10, "PMO_APPROVE", "Head PMO approval", Roles.HeadPmo, AuthorityTypes.ChangeApproval, Max: 1_000_000m),
            new StepSeed(11, "PMO_REVIEW", "Head PMO review", Roles.HeadPmo, Min: 1_000_000.01m),
            new StepSeed(20, "CFO_APPROVE", "CFO approval", Roles.Cfo, AuthorityTypes.ChangeApproval, Min: 1_000_000.01m, Max: 10_000_000m),
            new StepSeed(21, "EXEC_APPROVE", "Executive approval", Roles.ExecutiveAuthority, AuthorityTypes.ChangeApproval, Min: 10_000_000.01m)
        }),
        ("PROJECT_CLOSURE", "Project closure", "ProjectClosure", new[]
        {
            new StepSeed(10, "FIN_CONFIRM", "Financial reconciliation confirmation", Roles.FinanceOfficer),
            new StepSeed(20, "PMO_APPROVE", "Head PMO closure approval", Roles.HeadPmo)
        }),
        ("INVOICE_CERTIFICATION", "Invoice / payment certification", "Invoice", new[]
        {
            new StepSeed(10, "PM_CONFIRM", "Project manager delivery confirmation", Roles.ProjectManager, Sla: 48, Escalation: Roles.HeadPmo),
            new StepSeed(20, "FIN_CERTIFY", "Finance certification", Roles.FinanceOfficer, AuthorityTypes.PaymentCertification, Max: 500_000m, Sla: 48),
            new StepSeed(21, "CFO_CERTIFY", "CFO certification", Roles.Cfo, AuthorityTypes.PaymentCertification, Min: 500_000.01m, Sla: 48)
        })
    };

    private async Task SeedWorkflowsAsync(CancellationToken ct)
    {
        var existing = await _db.WorkflowDefinitions.Select(d => d.Code).Distinct().ToListAsync(ct);
        foreach (var (code, name, entityType, steps) in Workflows.Where(w => !existing.Contains(w.Code)))
        {
            var definition = new WorkflowDefinition
            {
                Code = code, Name = name, EntityType = entityType, DefinitionVersion = 1, Status = ConfigStatus.Approved,
                Description = "Seeded default; confirm against TETA's delegation of authority.", ActivatedAtUtc = _clock.UtcNow, ActivatedBy = "seed"
            };
            foreach (var s in steps)
            {
                definition.Steps.Add(new WorkflowStepDefinition
                {
                    DefinitionId = definition.Id, StepOrder = s.Order, Code = s.Code, Name = s.Name, RequiredRole = s.Role, AuthorityType = s.Authority,
                    MinimumValue = s.Min, MaximumValue = s.Max, SlaHours = s.Sla, EscalationRole = s.Escalation
                });
            }
            _db.WorkflowDefinitions.Add(definition);
        }
    }

    private async Task SeedDelegationsAsync(CancellationToken ct)
    {
        if (await _db.Delegations.AnyAsync(ct)) return;
        var from = new DateOnly(2026, 4, 1);
        var matrix = new (string Authority, string Role, decimal Max)[]
        {
            (AuthorityTypes.ProjectApproval, Roles.HeadPmo, 5_000_000m), (AuthorityTypes.ProjectApproval, Roles.Cfo, 20_000_000m),
            (AuthorityTypes.ProjectApproval, Roles.ExecutiveAuthority, Unlimited),
            (AuthorityTypes.BudgetApproval, Roles.Cfo, 20_000_000m), (AuthorityTypes.BudgetApproval, Roles.ExecutiveAuthority, Unlimited),
            (AuthorityTypes.ProcurementApproval, Roles.HeadScm, 1_000_000m), (AuthorityTypes.ProcurementApproval, Roles.Cfo, 10_000_000m),
            (AuthorityTypes.ProcurementApproval, Roles.ExecutiveAuthority, Unlimited),
            (AuthorityTypes.AwardApproval, Roles.Cfo, 10_000_000m), (AuthorityTypes.AwardApproval, Roles.ExecutiveAuthority, Unlimited),
            (AuthorityTypes.ContractApproval, Roles.HeadScm, 1_000_000m), (AuthorityTypes.ContractApproval, Roles.Cfo, 10_000_000m),
            (AuthorityTypes.ContractApproval, Roles.ExecutiveAuthority, Unlimited),
            (AuthorityTypes.VariationApproval, Roles.Cfo, 5_000_000m), (AuthorityTypes.VariationApproval, Roles.ExecutiveAuthority, Unlimited),
            (AuthorityTypes.PaymentCertification, Roles.FinanceOfficer, 500_000m), (AuthorityTypes.PaymentCertification, Roles.Cfo, 10_000_000m),
            (AuthorityTypes.PaymentCertification, Roles.ExecutiveAuthority, Unlimited),
            (AuthorityTypes.ChangeApproval, Roles.HeadPmo, 1_000_000m), (AuthorityTypes.ChangeApproval, Roles.Cfo, 10_000_000m),
            (AuthorityTypes.ChangeApproval, Roles.ExecutiveAuthority, Unlimited),
            (AuthorityTypes.ExceptionApproval, Roles.Cfo, 1_000_000m), (AuthorityTypes.ExceptionApproval, Roles.ExecutiveAuthority, Unlimited)
        };
        foreach (var (authority, role, max) in matrix)
        {
            _db.Delegations.Add(new Delegation
            {
                AuthorityType = authority, RoleCode = role, MaxAmount = max, EffectiveFrom = from, IsActive = true,
                PolicyReference = "Seeded default - replace with the approved TETA Delegation of Authority"
            });
        }
    }

    private async Task SeedSodRulesAsync(CancellationToken ct)
    {
        var existing = await _db.SodRules.Select(r => r.Code).ToListAsync(ct);
        var rules = new (string Code, string Description, string Entity, string First, string Second)[]
        {
            ("SOD-001", "The person who submitted a deliverable cannot accept it.", "Deliverable", "SubmitDeliverable", "AcceptDeliverable"),
            ("SOD-002", "The uploader of evidence cannot verify it.", "Evidence", "UploadEvidence", "VerifyEvidence"),
            ("SOD-003", "The author of a project charter cannot approve it.", "ProjectCharter", "Author", "Approve"),
            ("SOD-004", "The person who captured a performance result cannot verify it.", "PerformanceResult", "Capture", "Verify"),
            ("SOD-005", "The author of a specification cannot approve it.", "Specification", "Author", "Approve"),
            ("SOD-006", "The person who registered an invoice cannot certify it.", "Invoice", "RegisterInvoice", "FIN_CERTIFY"),
            ("SOD-007", "The person who registered an invoice cannot certify it (CFO band).", "Invoice", "RegisterInvoice", "CFO_CERTIFY"),
            ("SOD-008", "The person who accepted the deliverable cannot certify the related payment.", "Invoice", "AcceptDeliverable", "FIN_CERTIFY"),
            ("SOD-009", "The person who accepted the deliverable cannot certify the related payment (CFO band).", "Invoice", "AcceptDeliverable", "CFO_CERTIFY"),
            ("SOD-010", "A bid evaluator cannot take the adjudication recommendation.", "Procurement", "Evaluate", "BAC_RECOMMEND"),
            ("SOD-011", "A bid evaluator cannot make the award decision.", "Procurement", "Evaluate", "CFO_AWARD"),
            ("SOD-012", "A bid evaluator cannot make the award decision (accounting officer band).", "Procurement", "Evaluate", "EXEC_AWARD"),
            ("SOD-013", "The requisitioner cannot approve their own requisition.", "Requisition", "CreateRequisition", "SCM_APPROVE")
        };
        foreach (var r in rules.Where(r => !existing.Contains(r.Code)))
            _db.SodRules.Add(new SodRule { Code = r.Code, Description = r.Description, EntityType = r.Entity, FirstAction = r.First, SecondAction = r.Second, Mode = SodMode.Block });
    }

    // ---------------- Reference data (FR-ADM-006) ----------------
    private async Task SeedReferenceDataAsync(CancellationToken ct)
    {
        var existing = (await _db.ReferenceData.Select(r => new { r.Category, r.Code }).ToListAsync(ct)).Select(r => (r.Category, r.Code)).ToHashSet();
        var data = new Dictionary<string, string[]>
        {
            ["CostCategory"] = new[] { "Training delivery", "Learner stipends", "Professional services", "Goods and equipment", "Infrastructure", "Travel and subsistence", "Monitoring and evaluation", "Administration" },
            ["FundingSource"] = new[] { "Mandatory grant", "Discretionary grant", "Administration budget", "Donor funding", "Special project fund" },
            ["ProjectType"] = new[] { "Learnership", "Skills programme", "Bursary", "Internship", "Artisan development", "Infrastructure", "ICT", "Research" },
            ["Province"] = new[] { "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo", "Mpumalanga", "North West", "Northern Cape", "Western Cape" },
            ["EvidenceType"] = new[] { "Attendance register", "Certificate", "Completion report", "Site visit report", "Invoice", "Photo evidence", "Signed acceptance", "Learner agreement" },
            ["DocumentType"] = new[] { "Business case", "Project charter", "Specification", "Bid document", "Evaluation report", "Contract", "Variation", "Progress report", "Close-out report", "Minutes", "Declaration" },
            ["RiskCategory"] = new[] { "Strategic", "Financial", "Operational", "Compliance", "Fraud and corruption", "Reputational", "Information technology", "Delivery" },
            ["OrgUnit"] = new[] { "Office of the CEO", "Finance", "Supply Chain Management", "Projects/PMO", "Skills Planning", "Quality Assurance", "Corporate Services" }
        };
        foreach (var (category, names) in data)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var code = new string(names[i].ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
                if (code.Length > 50) code = code[..50];
                if (existing.Contains((category, code))) continue;
                _db.ReferenceData.Add(new ReferenceDataItem { Category = category, Code = code, Name = names[i], SortOrder = (i + 1) * 10, IsActive = true });
            }
        }
    }

    private async Task SeedSettingsAndTemplatesAsync(CancellationToken ct)
    {
        var keys = await _db.SystemSettings.Select(s => s.Key).ToListAsync(ct);
        foreach (var (key, (value, category, description)) in SettingKeys.Defaults.Where(d => !keys.Contains(d.Key)))
            _db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Category = category, Description = description });

        var templates = await _db.NotificationTemplates.Select(t => t.Code).ToListAsync(ct);
        foreach (var (code, subject, body) in NotificationTemplates.Defaults.Where(d => !templates.Contains(d.Code)))
            _db.NotificationTemplates.Add(new NotificationTemplate { Code = code, Subject = subject, Body = body, SendEmail = true, IsActive = true });
    }

    private async Task SeedProjectConfigurationAsync(CancellationToken ct)
    {
        if (!await _db.StageGateCriteria.AnyAsync(ct))
        {
            var gates = new (ProjectStage Stage, string Code, string Description, string CheckCode)[]
            {
                (ProjectStage.Initiation, "G1-01", "Business case approved", GateCheckCodes.BusinessCaseApproved),
                (ProjectStage.Initiation, "G1-02", "Project aligned to at least one APP indicator", GateCheckCodes.IndicatorAligned),
                (ProjectStage.Initiation, "G1-03", "Project charter approved", GateCheckCodes.CharterApproved),
                (ProjectStage.Initiation, "G1-04", "Sponsor, manager and stakeholders defined", GateCheckCodes.StakeholdersDefined),
                (ProjectStage.Planning, "G2-01", "Budget lines reconcile to approved budget", GateCheckCodes.BudgetReconciled),
                (ProjectStage.Planning, "G2-02", "Schedule baseline approved", GateCheckCodes.BaselineApproved),
                (ProjectStage.Planning, "G2-03", "Risk register populated", GateCheckCodes.RisksRegistered),
                (ProjectStage.Planning, "G2-04", "M&E plan defined", GateCheckCodes.MePlanDefined),
                (ProjectStage.Execution, "G3-01", "All milestones complete", GateCheckCodes.MilestonesComplete),
                (ProjectStage.Execution, "G3-02", "All deliverables accepted", GateCheckCodes.DeliverablesAccepted),
                (ProjectStage.Execution, "G3-03", "No open critical issues", GateCheckCodes.NoOpenCriticalIssues),
                (ProjectStage.CloseOut, "G4-01", "Contracts closed", GateCheckCodes.ContractsClosed),
                (ProjectStage.CloseOut, "G4-02", "Invoices settled", GateCheckCodes.InvoicesSettled),
                (ProjectStage.CloseOut, "G4-03", "Closure report approved", GateCheckCodes.ClosureApproved),
                (ProjectStage.CloseOut, "G4-04", "Benefit review scheduled", GateCheckCodes.BenefitReviewScheduled),
                (ProjectStage.BenefitReview, "G5-01", "Benefit review completed", GateCheckCodes.BenefitReviewCompleted)
            };
            var order = 0;
            foreach (var g in gates)
                _db.StageGateCriteria.Add(new StageGateCriterion { Stage = g.Stage, Code = g.Code, Description = g.Description, CheckCode = g.CheckCode, IsMandatory = true, SortOrder = ++order, IsActive = true });
        }

        if (!await _db.PrioritisationCriteria.AnyAsync(ct))
        {
            var criteria = new (string Code, string Name, string Category, decimal Weight)[]
            {
                ("STRAT", "Strategic / APP alignment", "Strategic", 30), ("IMPACT", "Beneficiary impact", "Impact", 25),
                ("VFM", "Value for money", "Financial", 20), ("READY", "Implementation readiness", "Delivery", 15), ("RISK", "Delivery risk (inverse)", "Risk", 10)
            };
            foreach (var c in criteria)
                _db.PrioritisationCriteria.Add(new PrioritisationCriterion { Code = c.Code, Name = c.Name, Category = c.Category, Weight = c.Weight, MaxScore = 5, IsActive = true });
        }

        if (!await _db.RiskRatingBands.AnyAsync(ct))
        {
            var i = 0;
            foreach (var band in RiskRating.DefaultBands)
                _db.RiskRatingBands.Add(new RiskRatingBand { Name = band.Name, MinScore = band.MinScore, MaxScore = band.MaxScore, Colour = band.Colour, SortOrder = i++ });
        }
    }

    private async Task SeedProcurementConfigurationAsync(CancellationToken ct)
    {
        var from = new DateOnly(2026, 4, 1);
        if (!await _db.ProcurementMethodRules.AnyAsync(ct))
        {
            var rules = new (string Code, string Name, decimal Min, decimal? Max, bool Publish, int Quotes, int AdvertDays, string Authority)[]
            {
                ("PETTY_CASH", "Petty cash", 0m, 2_000m, false, 1, 0, "Programme manager"),
                ("QUOTATIONS", "Written price quotations", 2_000.01m, 30_000m, false, 3, 0, "Head SCM"),
                ("RFQ", "Formal written price quotations (advertised)", 30_000.01m, 1_000_000m, true, 3, 7, "Head SCM"),
                ("OPEN_TENDER", "Competitive bidding (open tender)", 1_000_000.01m, null, true, 0, 21, "Accounting officer / delegate")
            };
            foreach (var r in rules)
            {
                _db.ProcurementMethodRules.Add(new ProcurementMethodRule
                {
                    MethodCode = r.Code, Name = r.Name, MinValue = r.Min, MaxValue = r.Max, EffectiveFrom = from, RuleVersion = 1, Status = ConfigStatus.Approved,
                    RequiresPublication = r.Publish, MinimumQuotations = r.Quotes, MinimumAdvertDays = r.AdvertDays, ApprovalAuthority = r.Authority,
                    PolicyReference = "Seeded default - confirm thresholds against the current National Treasury SCM Instruction and TETA SCM policy",
                    ApprovedBy = "seed", ApprovedAtUtc = _clock.UtcNow
                });
            }
        }

        if (!await _db.PricePreferenceSystems.AnyAsync(ct))
        {
            _db.PricePreferenceSystems.Add(new PricePreferenceSystem
            {
                Code = "80/20", Name = "80/20 preference point system", PricePoints = 80, PreferencePoints = 20, ApplicableFromValue = 0m,
                ApplicableToValue = 50_000_000m, EffectiveFrom = new DateOnly(2023, 1, 16), Status = ConfigStatus.Approved,
                PolicyReference = "Preferential Procurement Regulations, 2022"
            });
            _db.PricePreferenceSystems.Add(new PricePreferenceSystem
            {
                Code = "90/10", Name = "90/10 preference point system", PricePoints = 90, PreferencePoints = 10, ApplicableFromValue = 50_000_000.01m,
                ApplicableToValue = null, EffectiveFrom = new DateOnly(2023, 1, 16), Status = ConfigStatus.Approved,
                PolicyReference = "Preferential Procurement Regulations, 2022"
            });
        }
    }

    private async Task SeedCalendarAndRetentionAsync(CancellationToken ct)
    {
        var dates = (await _db.PublicHolidays.Select(h => h.Date).ToListAsync(ct)).ToHashSet();
        var holidays = new (int Y, int M, int D, string Name)[]
        {
            (2026, 1, 1, "New Year's Day"), (2026, 3, 21, "Human Rights Day"), (2026, 4, 3, "Good Friday"), (2026, 4, 6, "Family Day"),
            (2026, 4, 27, "Freedom Day"), (2026, 5, 1, "Workers' Day"), (2026, 6, 16, "Youth Day"), (2026, 8, 10, "National Women's Day (observed)"),
            (2026, 9, 24, "Heritage Day"), (2026, 12, 16, "Day of Reconciliation"), (2026, 12, 25, "Christmas Day"), (2026, 12, 26, "Day of Goodwill"),
            (2027, 1, 1, "New Year's Day"), (2027, 3, 22, "Human Rights Day (observed)"), (2027, 3, 26, "Good Friday"), (2027, 3, 29, "Family Day"),
            (2027, 4, 27, "Freedom Day"), (2027, 5, 1, "Workers' Day"), (2027, 6, 16, "Youth Day"), (2027, 8, 9, "National Women's Day"),
            (2027, 9, 24, "Heritage Day"), (2027, 12, 16, "Day of Reconciliation"), (2027, 12, 25, "Christmas Day"), (2027, 12, 27, "Day of Goodwill (observed)")
        };
        foreach (var h in holidays)
        {
            var date = new DateOnly(h.Y, h.M, h.D);
            if (dates.Add(date)) _db.PublicHolidays.Add(new PublicHoliday { Date = date, Name = h.Name });
        }

        if (!await _db.RetentionPolicies.AnyAsync(ct))
        {
            var policies = new (string Class, int Years, string Action)[]
            {
                ("Financial", 5, "Review"), ("Procurement", 5, "Review"), ("Contract", 7, "Review"), ("Project", 7, "Archive"),
                ("Beneficiary", 5, "Review"), ("Governance", 10, "Archive")
            };
            foreach (var p in policies)
                _db.RetentionPolicies.Add(new RetentionPolicy
                {
                    RecordClass = p.Class, RetentionYears = p.Years, DisposalAction = p.Action, IsActive = true,
                    LegalReference = "Seeded default - confirm with TETA's approved file plan / National Archives disposal authority"
                });
        }
    }

    // ---------------- Bootstrap administrators ----------------
    /// <summary>
    /// Grants the TETA System and Security Administrator roles to existing shared-platform users
    /// named in <c>Teta:BootstrapAdmins</c> (comma separated), only while no active Security
    /// Administrator exists. After that, role changes go through dual-control approval.
    /// </summary>
    private async Task BootstrapAdministratorsAsync(CancellationToken ct)
    {
        var names = (_config["Teta:BootstrapAdmins"] ?? "admin").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0) return;
        if (await _db.UserRoleAssignments.AnyAsync(a => a.RoleCode == Roles.SecurityAdministrator && a.Status == AssignmentStatus.Active, ct)) return;

        var users = await _db.Users.AsNoTracking().Where(u => names.Contains(u.Username) && u.IsActive).ToListAsync(ct);
        foreach (var user in users)
        {
            foreach (var role in new[] { Roles.SystemAdministrator, Roles.SecurityAdministrator })
            {
                _db.UserRoleAssignments.Add(new UserRoleAssignment
                {
                    UserId = user.Id, RoleCode = role, ScopeType = ScopeType.Global, EffectiveFrom = _clock.Today, Status = AssignmentStatus.Active,
                    RequestedBy = "bootstrap", ApprovedBy = "bootstrap", ApprovedAtUtc = _clock.UtcNow, Reason = "Initial administrator (Teta:BootstrapAdmins)"
                });
            }
            _logger.LogWarning("Bootstrapped TETA administrator roles for {Username}", user.Username);
        }
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Demo / UAT data ----------------
    private static readonly (string Username, string Name, string Role)[] DemoUsers =
    {
        ("teta.ceo", "Thandi Mokoena (CEO)", Roles.ExecutiveAuthority),
        ("teta.cfo", "Pieter Botha (CFO)", Roles.Cfo),
        ("teta.board", "Board Member", Roles.Board),
        ("teta.pmo", "Lindiwe Dube (Head PMO)", Roles.HeadPmo),
        ("teta.pm", "Sipho Nkosi (Project Manager)", Roles.ProjectManager),
        ("teta.headscm", "Ayesha Patel (Head SCM)", Roles.HeadScm),
        ("teta.scm", "Johan van Wyk (SCM Officer)", Roles.ScmOfficer),
        ("teta.contracts", "Naledi Khumalo (Contract Manager)", Roles.ContractManager),
        ("teta.finance", "Kagiso Molefe (Finance Officer)", Roles.FinanceOfficer),
        ("teta.strategy", "Zanele Mthembu (Strategy Officer)", Roles.StrategyOfficer),
        ("teta.me", "Bongani Zulu (M&E Officer)", Roles.MeOfficer),
        ("teta.risk", "Fatima Adams (Risk & Compliance)", Roles.RiskCompliance),
        ("teta.audit", "Internal Auditor", Roles.InternalAudit),
        ("teta.evaluator", "Committee Evaluator", Roles.Evaluator),
        ("teta.secadmin", "Security Administrator", Roles.SecurityAdministrator),
        ("teta.sysadmin", "System Administrator", Roles.SystemAdministrator),
        ("teta.supplier", "Supplier Portal User", Roles.Supplier)
    };

    private async Task SeedDemoDataAsync(CancellationToken ct)
    {
        var password = _config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password) || PasswordPolicy.Validate(password).Count > 0)
        {
            _logger.LogWarning("Seed:DemoData is on but Seed:DemoPassword is missing or does not meet the password policy; demo users skipped.");
            return;
        }

        var names = DemoUsers.Select(d => d.Username).ToList();
        var existingUsers = await _db.Users.Where(u => names.Contains(u.Username)).ToDictionaryAsync(u => u.Username, ct);
        foreach (var (username, name, role) in DemoUsers)
        {
            if (!existingUsers.TryGetValue(username, out var user))
            {
                user = new PlatformUser
                {
                    Username = username, Email = $"{username}@teta.demo", DisplayName = name, PasswordHash = _hasher.Hash(password), IsActive = true,
                    CreatedAtUtc = _clock.UtcNow, CreatedBy = "seed"
                };
                _db.Users.Add(user);
                existingUsers[username] = user;
            }
            if (!await _db.UserRoleAssignments.AnyAsync(a => a.UserId == user.Id && a.RoleCode == role, ct))
            {
                _db.UserRoleAssignments.Add(new UserRoleAssignment
                {
                    UserId = user.Id, RoleCode = role, ScopeType = ScopeType.Global, EffectiveFrom = _clock.Today, Status = AssignmentStatus.Active,
                    RequestedBy = "seed", ApprovedBy = "seed", ApprovedAtUtc = _clock.UtcNow, Reason = "Demo/UAT user"
                });
            }
        }
        await _db.SaveChangesAsync(ct);

        if (await _db.Portfolios.AnyAsync(ct)) return;

        var portfolio = new Portfolio { Code = "SKILLS", Name = "Sector Skills Development", Description = "Discretionary grant funded skills interventions", OwnerName = "Head PMO" };
        var programme1 = new Programme { PortfolioId = portfolio.Id, Code = "LRN", Name = "Learnerships and Apprenticeships", OwnerName = "Programme Manager" };
        var programme2 = new Programme { PortfolioId = portfolio.Id, Code = "RPL", Name = "Recognition of Prior Learning", OwnerName = "Programme Manager" };
        _db.Portfolios.Add(portfolio);
        _db.Programmes.AddRange(programme1, programme2);

        var plan = new StrategicPlan
        {
            Code = "SP-2025-2030", Name = "TETA Strategic Plan 2025/26 - 2029/30", PeriodStart = new DateOnly(2025, 4, 1), PeriodEnd = new DateOnly(2030, 3, 31),
            Status = PlanStatus.Approved, ApprovedAtUtc = _clock.UtcNow, ApprovedBy = "seed"
        };
        var outcome = new StrategicOutcome { PlanId = plan.Id, Code = "O1", Description = "Increased access to occupationally directed programmes in the transport sector" };
        var objective = new StrategicObjective
        {
            PlanId = plan.Id, OutcomeId = outcome.Id, Code = "SO1.1", Description = "Fund learnerships and apprenticeships for unemployed youth",
            OwnerName = "Head: Skills Development", ProgrammeName = programme1.Name
        };
        var indicator = new AppIndicator
        {
            ObjectiveId = objective.Id, Code = "PI-1.1.1", Name = "Number of unemployed learners entering learnerships", UnitOfMeasure = "Learners",
            EvidenceRule = "Signed learner agreements and attendance registers", RequiredEvidenceTypes = "Learner agreement,Attendance register",
            ResponsibleExecutive = "COO"
        };
        _db.StrategicPlans.Add(plan);
        _db.StrategicOutcomes.Add(outcome);
        _db.StrategicObjectives.Add(objective);
        _db.AppIndicators.Add(indicator);
        for (var q = 1; q <= 4; q++)
            _db.AppTargets.Add(new AppTarget { IndicatorId = indicator.Id, FinancialYear = "2026/27", Quarter = q, TargetValue = 250, Status = TargetStatus.Approved, ApprovedBy = "seed", ApprovedAtUtc = _clock.UtcNow });

        // A second portfolio/programme pair and a second strategic objective, so the demo shows more
        // than one line of business.
        var programme3 = new Programme { PortfolioId = portfolio.Id, Code = "BURS", Name = "Bursaries and Internships", OwnerName = "Programme Manager" };
        _db.Programmes.Add(programme3);
        var portfolio2 = new Portfolio { Code = "INFRA", Name = "ICT and Infrastructure", Description = "Enabling infrastructure and digital capacity", OwnerName = "Head ICT" };
        var programme4 = new Programme { PortfolioId = portfolio2.Id, Code = "ICT", Name = "ICT Infrastructure and Digital Capacity", OwnerName = "Programme Manager" };
        _db.Portfolios.Add(portfolio2);
        _db.Programmes.Add(programme4);

        var outcome2 = new StrategicOutcome { PlanId = plan.Id, Code = "O2", Description = "Strengthened institutional and ICT capacity to deliver the SETA mandate" };
        var objective2 = new StrategicObjective
        {
            PlanId = plan.Id, OutcomeId = outcome2.Id, Code = "SO2.1", Description = "Modernise regional office ICT infrastructure",
            OwnerName = "Head: ICT", ProgrammeName = programme4.Name
        };
        var indicator2 = new AppIndicator
        {
            ObjectiveId = objective2.Id, Code = "PI-2.1.1", Name = "Number of regional office sites with upgraded ICT infrastructure", UnitOfMeasure = "Sites",
            EvidenceRule = "Signed completion/acceptance reports and site photographs", RequiredEvidenceTypes = "Signed acceptance,Photo evidence",
            ResponsibleExecutive = "Head: ICT"
        };
        _db.StrategicOutcomes.Add(outcome2);
        _db.StrategicObjectives.Add(objective2);
        _db.AppIndicators.Add(indicator2);
        for (var q = 1; q <= 4; q++)
            _db.AppTargets.Add(new AppTarget { IndicatorId = indicator2.Id, FinancialYear = "2026/27", Quarter = q, TargetValue = 3, Status = TargetStatus.Approved, ApprovedBy = "seed", ApprovedAtUtc = _clock.UtcNow });

        // ---- Suppliers (a handful, active and one suspended, for realistic pipeline/history data) ----
        var supplier = new Supplier
        {
            SupplierNumber = "SUP-DEMO-0001", LegalName = "Demo Training Provider (Pty) Ltd", NormalizedName = Supplier.Normalize("Demo Training Provider (Pty) Ltd"),
            RegistrationNumber = "2019/000001/07", CsdNumber = "MAAA0000001", BbbeeLevel = 1, Status = SupplierStatus.Active, CsdVerified = true,
            IsImplementingPartner = true, Email = "provider@teta.demo", Province = "Gauteng", TaxClearanceExpiry = _clock.Today.AddYears(1)
        };
        var supplier2 = new Supplier
        {
            SupplierNumber = "SUP-DEMO-0002", LegalName = "Vhutali Construction and Projects (Pty) Ltd", NormalizedName = Supplier.Normalize("Vhutali Construction and Projects (Pty) Ltd"),
            RegistrationNumber = "2015/044213/07", CsdNumber = "MAAA0000002", TaxNumber = "9001122334", BbbeeLevel = 2, Status = SupplierStatus.Active, CsdVerified = true,
            Email = "info@vhutali.demo", Province = "Limpopo", TaxClearanceExpiry = _clock.Today.AddMonths(8)
        };
        var supplier3 = new Supplier
        {
            SupplierNumber = "SUP-DEMO-0003", LegalName = "Sizanani Skills Academy (Pty) Ltd", NormalizedName = Supplier.Normalize("Sizanani Skills Academy (Pty) Ltd"),
            RegistrationNumber = "2011/018832/07", CsdNumber = "MAAA0000003", BbbeeLevel = 1, Status = SupplierStatus.Active, CsdVerified = true,
            IsImplementingPartner = true, Email = "admin@sizanani.demo", Province = "KwaZulu-Natal", TaxClearanceExpiry = _clock.Today.AddYears(1)
        };
        var supplier4 = new Supplier
        {
            SupplierNumber = "SUP-DEMO-0004", LegalName = "Metro ICT Solutions (Pty) Ltd", NormalizedName = Supplier.Normalize("Metro ICT Solutions (Pty) Ltd"),
            RegistrationNumber = "2018/501234/07", CsdNumber = "MAAA0000004", TaxNumber = "9112233445", BbbeeLevel = 4, Status = SupplierStatus.Active, CsdVerified = true,
            Email = "sales@metroict.demo", Province = "Gauteng", TaxClearanceExpiry = _clock.Today.AddMonths(5)
        };
        var supplier5 = new Supplier
        {
            SupplierNumber = "SUP-DEMO-0005", LegalName = "Karabo Logistics and Fleet Services CC", NormalizedName = Supplier.Normalize("Karabo Logistics and Fleet Services CC"),
            RegistrationNumber = "2009/077621/23", CsdNumber = "MAAA0000005", BbbeeLevel = 1, Status = SupplierStatus.Active, CsdVerified = true,
            Email = "ops@karabologistics.demo", Province = "Free State", TaxClearanceExpiry = _clock.Today.AddYears(1)
        };
        var supplier6 = new Supplier
        {
            SupplierNumber = "SUP-DEMO-0006", LegalName = "Ubuntu Consulting Engineers (Pty) Ltd", NormalizedName = Supplier.Normalize("Ubuntu Consulting Engineers (Pty) Ltd"),
            RegistrationNumber = "2013/099887/07", CsdNumber = "MAAA0000006", TaxNumber = "9223344556", BbbeeLevel = 3, Status = SupplierStatus.Active, CsdVerified = true,
            Email = "info@ubuntueng.demo", Province = "Western Cape", TaxClearanceExpiry = _clock.Today.AddMonths(10)
        };
        var supplier7 = new Supplier
        {
            SupplierNumber = "SUP-DEMO-0007", LegalName = "Thusong Community Development Trust", NormalizedName = Supplier.Normalize("Thusong Community Development Trust"),
            RegistrationNumber = "IT004455/2012", CsdNumber = "MAAA0000007", BbbeeLevel = 1, Status = SupplierStatus.Active, CsdVerified = true,
            IsImplementingPartner = true, Email = "trust@thusong.demo", Province = "Eastern Cape", TaxClearanceExpiry = _clock.Today.AddYears(1)
        };
        var supplier8 = new Supplier
        {
            SupplierNumber = "SUP-DEMO-0008", LegalName = "Falcon Office Supplies (Pty) Ltd", NormalizedName = Supplier.Normalize("Falcon Office Supplies (Pty) Ltd"),
            RegistrationNumber = "2020/611234/07", CsdNumber = "MAAA0000008", BbbeeLevel = 2, Status = SupplierStatus.Suspended, CsdVerified = true,
            Email = "orders@falconoffice.demo", Province = "Gauteng", TaxClearanceExpiry = _clock.Today.AddDays(-30)
        };
        _db.Suppliers.AddRange(supplier, supplier2, supplier3, supplier4, supplier5, supplier6, supplier7, supplier8);
        if (existingUsers.TryGetValue("teta.supplier", out var supplierUser))
            _db.SupplierUsers.Add(new SupplierUser { SupplierId = supplier.Id, UserId = supplierUser.Id, LinkedAtUtc = _clock.UtcNow });

        var year = _clock.UtcNow.Year;
        var today = _clock.Today;
        string Num(string prefix, int n) => $"{prefix}-{year}-{n:D4}";

        // ================================================================================
        // Projects across the full lifecycle: concept, approved/planning, in-execution at
        // Green/Amber/Red health, nearing closure with a pending change, closing, a closed
        // historical project with a completed benefit review, and one rejected concept.
        // ================================================================================
        var project1 = new Project
        {
            DraftReference = Num(NumberPrefixes.ProjectDraft, 1), Name = "Artisan Development Programme - Polokwane", ProgrammeId = programme1.Id,
            SponsorName = "Lindiwe Dube (Head PMO)", ManagerName = "Regional Programme Coordinator", OrgUnit = "Projects/PMO", ProjectType = "Artisan development",
            Province = "Limpopo", District = "Capricorn", Municipality = "Polokwane", Status = ProjectStatus.BusinessCase, Stage = ProjectStage.Initiation,
            PlannedStart = today.AddMonths(2), PlannedEnd = today.AddMonths(14)
        };
        var project2 = new Project
        {
            DraftReference = Num(NumberPrefixes.ProjectDraft, 2), Name = "Learnership Programme: Road Freight Logistics 2026/27", ProgrammeId = programme1.Id,
            SponsorName = "Lindiwe Dube (Head PMO)", ManagerName = "Sipho Nkosi", ManagerUserId = existingUsers.TryGetValue("teta.pm", out var pmUser) ? pmUser.Id : null,
            OrgUnit = "Skills Planning", ProjectType = "Learnership", Province = "Gauteng", Status = ProjectStatus.Concept, Stage = ProjectStage.Initiation,
            PlannedStart = today.AddMonths(1), PlannedEnd = today.AddMonths(13)
        };
        var project3 = new Project
        {
            DraftReference = Num(NumberPrefixes.ProjectDraft, 3), Name = "Recognition of Prior Learning: Heavy Vehicle Drivers", ProgrammeId = programme2.Id,
            SponsorName = "Lindiwe Dube (Head PMO)", ManagerName = "Sipho Nkosi", ManagerUserId = pmUser?.Id, OrgUnit = "Skills Planning", ProjectType = "Skills programme",
            Province = "Gauteng", Status = ProjectStatus.Concept, Stage = ProjectStage.Initiation, PlannedStart = today.AddMonths(-6), PlannedEnd = today.AddMonths(6)
        };
        var project4 = new Project
        {
            DraftReference = Num(NumberPrefixes.ProjectDraft, 4), Name = "Bursary Programme: Engineering Graduates 2026", ProgrammeId = programme3.Id,
            SponsorName = "Pieter Botha (CFO)", ManagerName = "Naledi Khumalo", OrgUnit = "Skills Planning", ProjectType = "Bursary", Province = "Gauteng",
            Status = ProjectStatus.Concept, Stage = ProjectStage.Initiation, PlannedStart = today.AddMonths(-8), PlannedEnd = today.AddMonths(4)
        };
        var project5 = new Project
        {
            DraftReference = Num(NumberPrefixes.ProjectDraft, 5), Name = "ICT Infrastructure Upgrade: Regional Offices", ProgrammeId = programme4.Id,
            SponsorName = "Thandi Mokoena (CEO)", ManagerName = "Metro ICT Programme Lead", OrgUnit = "Office of the CEO", ProjectType = "ICT", Province = "Gauteng",
            Status = ProjectStatus.Concept, Stage = ProjectStage.Initiation, PlannedStart = today.AddMonths(-10), PlannedEnd = today.AddMonths(-1)
        };
        var project6 = new Project
        {
            DraftReference = Num(NumberPrefixes.ProjectDraft, 6), Name = "Artisan Development Programme - Nelspruit", ProgrammeId = programme1.Id,
            SponsorName = "Lindiwe Dube (Head PMO)", ManagerName = "Sipho Nkosi", ManagerUserId = pmUser?.Id, OrgUnit = "Projects/PMO", ProjectType = "Artisan development",
            Province = "Mpumalanga", District = "Ehlanzeni", Municipality = "Mbombela", Status = ProjectStatus.Concept, Stage = ProjectStage.Initiation,
            PlannedStart = today.AddMonths(-11), PlannedEnd = today.AddMonths(-1)
        };
        var project7 = new Project
        {
            DraftReference = Num(NumberPrefixes.ProjectDraft, 7), Name = "Skills Programme: Warehouse Management", ProgrammeId = programme2.Id,
            SponsorName = "Lindiwe Dube (Head PMO)", ManagerName = "Regional Programme Coordinator", OrgUnit = "Skills Planning", ProjectType = "Skills programme",
            Province = "KwaZulu-Natal", Status = ProjectStatus.Concept, Stage = ProjectStage.Initiation, PlannedStart = today.AddMonths(-13), PlannedEnd = today.AddMonths(-2)
        };
        var project8 = new Project
        {
            DraftReference = "CON-2025-0031", Name = "Learnership Programme: Rail Operations (2024/25)", ProgrammeId = programme1.Id,
            SponsorName = "Lindiwe Dube (Head PMO)", ManagerName = "Regional Programme Coordinator", OrgUnit = "Skills Planning", ProjectType = "Learnership",
            Province = "Gauteng", Status = ProjectStatus.Concept, Stage = ProjectStage.Initiation, PlannedStart = new DateOnly(2024, 6, 1), PlannedEnd = new DateOnly(2025, 5, 31)
        };
        var project9 = new Project
        {
            DraftReference = Num(NumberPrefixes.ProjectDraft, 9), Name = "Skills Programme: Green Economy Pilot", ProgrammeId = programme2.Id,
            SponsorName = "Zanele Mthembu (Strategy Officer)", ManagerName = "Strategy Analyst", OrgUnit = "Strategy", ProjectType = "Skills programme",
            Province = "Western Cape", Status = ProjectStatus.Rejected, Stage = ProjectStage.Initiation, PlannedStart = today.AddMonths(3), PlannedEnd = today.AddMonths(9)
        };
        _db.Projects.AddRange(project1, project2, project3, project4, project5, project6, project7, project8, project9);

        // Business cases: project1 deliberately incomplete (missing Risks/DeliveryModel) to demo
        // validation; the rest are approved with a full case; project9's is rejected.
        _db.BusinessCases.AddRange(
            new BusinessCase
            {
                ProjectId = project1.Id, Problem = "Youth unemployment and a shortage of qualified artisans in the Capricorn district.",
                Objectives = "Place 120 unemployed youth into accredited artisan trades.", Options = "In-house delivery vs. accredited training provider partnership.",
                Scope = "Recruitment, trade theory, workplace-based learning and trade testing for 120 learners.", Benefits = "120 qualified artisans; improved regional employability.",
                EstimatedCost = 9_800_000m, Status = BusinessCaseStatus.Draft, SubmittedAtUtc = _clock.UtcNow.AddDays(-3)
            },
            new BusinessCase
            {
                ProjectId = project2.Id, Problem = "Shortage of qualified drivers and logistics staff in the freight sector.", Objectives = "Fund 100 learnerships in road freight logistics.",
                Options = "Accredited training provider partnership.", Scope = "Recruitment, theory and workplace-based learning for 100 learners over 12 months.",
                Benefits = "100 qualified logistics learners; improved sector employability.", EstimatedCost = 8_500_000m, Risks = "Provider capacity; learner attrition.",
                DeliveryModel = "Accredited training provider", Status = BusinessCaseStatus.Approved, SubmittedAtUtc = _clock.UtcNow.AddMonths(-2), DecidedAtUtc = _clock.UtcNow.AddMonths(-1)
            },
            new BusinessCase
            {
                ProjectId = project3.Id, Problem = "Experienced heavy-vehicle drivers lack formal recognition of prior learning.", Objectives = "Assess and certify 200 experienced drivers under RPL.",
                Options = "Panel-based RPL assessment vs. full re-training.", Scope = "RPL assessment, gap training and certification for 200 drivers.",
                Benefits = "200 formally recognised drivers; reduced re-training cost.", EstimatedCost = 12_000_000m, Risks = "Assessor availability; logistics of regional sites.",
                DeliveryModel = "Panel-based RPL assessment", Status = BusinessCaseStatus.Approved, SubmittedAtUtc = _clock.UtcNow.AddMonths(-8), DecidedAtUtc = _clock.UtcNow.AddMonths(-7)
            },
            new BusinessCase
            {
                ProjectId = project4.Id, Problem = "Engineering graduates require workplace exposure to complete professional registration.", Objectives = "Fund bursaries and workplace placement for 40 engineering graduates.",
                Options = "In-house placement vs. implementing partner.", Scope = "Bursary funding, mentorship and workplace placement for 40 graduates over 18 months.",
                Benefits = "40 professionally registered engineers.", EstimatedCost = 6_000_000m, Risks = "Host employer capacity; graduate attrition.", DeliveryModel = "Implementing partner",
                Status = BusinessCaseStatus.Approved, SubmittedAtUtc = _clock.UtcNow.AddMonths(-9), DecidedAtUtc = _clock.UtcNow.AddMonths(-8)
            },
            new BusinessCase
            {
                ProjectId = project5.Id, Problem = "Regional offices run on end-of-life network and server infrastructure, risking service continuity.",
                Objectives = "Upgrade ICT infrastructure at 6 regional offices.", Options = "Phased upgrade vs. single turnkey contract.",
                Scope = "Network, server and end-user device refresh at 6 regional offices.", Benefits = "Reduced downtime; improved service delivery.", EstimatedCost = 15_000_000m,
                Risks = "Single point of supplier dependency; site access delays.", DeliveryModel = "Single turnkey contract", Status = BusinessCaseStatus.Approved,
                SubmittedAtUtc = _clock.UtcNow.AddMonths(-11), DecidedAtUtc = _clock.UtcNow.AddMonths(-10)
            },
            new BusinessCase
            {
                ProjectId = project6.Id, Problem = "Youth unemployment and a shortage of qualified artisans in the Ehlanzeni district.", Objectives = "Place 90 unemployed youth into accredited artisan trades.",
                Options = "Accredited training provider partnership.", Scope = "Recruitment, trade theory, workplace-based learning and trade testing for 90 learners.",
                Benefits = "90 qualified artisans.", EstimatedCost = 9_500_000m, Risks = "Provider capacity; learner attrition.", DeliveryModel = "Accredited training provider",
                Status = BusinessCaseStatus.Approved, SubmittedAtUtc = _clock.UtcNow.AddMonths(-12), DecidedAtUtc = _clock.UtcNow.AddMonths(-11)
            },
            new BusinessCase
            {
                ProjectId = project7.Id, Problem = "Warehouse operators lack formal skills recognised by the logistics sector.", Objectives = "Train 60 warehouse operators in a recognised skills programme.",
                Options = "Accredited training provider partnership.", Scope = "Classroom and practical training for 60 learners over 6 months.", Benefits = "60 skilled warehouse operators.",
                EstimatedCost = 4_200_000m, Risks = "Employer release time for learners.", DeliveryModel = "Accredited training provider", Status = BusinessCaseStatus.Approved,
                SubmittedAtUtc = _clock.UtcNow.AddMonths(-14), DecidedAtUtc = _clock.UtcNow.AddMonths(-13)
            },
            new BusinessCase
            {
                ProjectId = project8.Id, Problem = "Rail operators sector faced a shortage of certified rail operations staff.", Objectives = "Fund 80 learnerships in rail operations.",
                Options = "Accredited training provider partnership.", Scope = "Recruitment, theory and workplace-based learning for 80 learners over 12 months.",
                Benefits = "80 qualified rail operations learners; improved sector safety record.", EstimatedCost = 7_300_000m, Risks = "Provider capacity.", DeliveryModel = "Accredited training provider",
                ExpectedBenefitMeasure = "Learners placed in permanent rail-sector employment", ExpectedBenefitValue = 60, Status = BusinessCaseStatus.Approved,
                SubmittedAtUtc = new DateTime(2024, 6, 10), DecidedAtUtc = new DateTime(2024, 7, 1)
            },
            new BusinessCase
            {
                ProjectId = project9.Id, Problem = "Limited green-economy skills pipeline.", Objectives = "Pilot a green-economy skills programme for 30 learners.",
                Options = "New provider partnership.", Scope = "Pilot classroom and practical training for 30 learners.", Benefits = "30 learners exposed to green-economy skills.",
                EstimatedCost = 2_100_000m, Risks = "Unproven curriculum; no accredited provider yet identified.", DeliveryModel = "To be determined", Status = BusinessCaseStatus.Rejected,
                SubmittedAtUtc = _clock.UtcNow.AddMonths(-2), DecidedAtUtc = _clock.UtcNow.AddMonths(-1), DecisionComment = "Returned: no accredited provider identified and curriculum not yet approved by QCTO. Resubmit once a provider and curriculum are confirmed."
            });

        // Approve the business cases and assign immutable Project IDs (BR-001/FR-PPM-004/005).
        project2.ActivateWithProjectId(Num(NumberPrefixes.Project, 1), 8_500_000m, _clock.UtcNow.AddMonths(-1));
        project3.ActivateWithProjectId(Num(NumberPrefixes.Project, 2), 12_000_000m, _clock.UtcNow.AddMonths(-7));
        project4.ActivateWithProjectId(Num(NumberPrefixes.Project, 3), 6_000_000m, _clock.UtcNow.AddMonths(-8));
        project5.ActivateWithProjectId(Num(NumberPrefixes.Project, 4), 15_000_000m, _clock.UtcNow.AddMonths(-10));
        project6.ActivateWithProjectId(Num(NumberPrefixes.Project, 5), 9_500_000m, _clock.UtcNow.AddMonths(-11));
        project7.ActivateWithProjectId(Num(NumberPrefixes.Project, 6), 4_200_000m, _clock.UtcNow.AddMonths(-13));
        project8.ActivateWithProjectId("PRJ-2024-0014", 7_300_000m, new DateTime(2024, 7, 1));

        project3.Stage = ProjectStage.Execution; project3.Status = ProjectStatus.InExecution; project3.ActualStart = today.AddMonths(-6);
        project3.Health = HealthStatus.Green; project3.HealthExplanation = "Schedule, cost and delivery are all within tolerance; no open critical risks."; project3.HealthCalculatedAtUtc = _clock.UtcNow.AddDays(-1);
        project4.Stage = ProjectStage.Execution; project4.Status = ProjectStatus.InExecution; project4.ActualStart = today.AddMonths(-8);
        project4.Health = HealthStatus.Amber; project4.HealthExplanation = "One milestone is overdue and the workplace-placement schedule has slipped by 3 weeks."; project4.HealthCalculatedAtUtc = _clock.UtcNow.AddDays(-1);
        project5.Stage = ProjectStage.Execution; project5.Status = ProjectStatus.InExecution; project5.ActualStart = today.AddMonths(-10);
        project5.Health = HealthStatus.Red; project5.HealthExplanation = "Cost forecast exceeds approved budget, a critical supplier risk is open, and a contract breach is unresolved.";
        project5.HealthCalculatedAtUtc = _clock.UtcNow.AddDays(-1);
        project6.Stage = ProjectStage.Execution; project6.Status = ProjectStatus.InExecution; project6.ActualStart = today.AddMonths(-11);
        project6.Health = HealthStatus.Green; project6.HealthExplanation = "Nearing close-out; all milestones on track and a minor schedule-extension change request is pending approval.";
        project6.HealthCalculatedAtUtc = _clock.UtcNow.AddDays(-1);
        project7.Stage = ProjectStage.CloseOut; project7.Status = ProjectStatus.Closing; project7.ActualStart = today.AddMonths(-13); project7.ActualEnd = today.AddDays(-5);
        project7.Health = HealthStatus.Green; project7.HealthExplanation = "Delivery complete; close-out checklist in progress."; project7.HealthCalculatedAtUtc = _clock.UtcNow.AddDays(-1);
        project8.Stage = ProjectStage.Completed; project8.Status = ProjectStatus.Closed; project8.ActualStart = new DateOnly(2024, 6, 15); project8.ActualEnd = new DateOnly(2025, 5, 20);
        project8.Health = HealthStatus.Green; project8.HealthExplanation = "Closed on time and within budget; benefit review completed.";

        // ---- Budget lines (FR-BUD-001/002), baselined for every approved project ----
        var budgetLine2 = new ProjectBudgetLine { ProjectId = project2.Id, FinancialYear = "2026/27", CostCategory = "Training delivery", FundingSource = "Discretionary grant" };
        budgetLine2.SetOriginal(8_500_000m); budgetLine2.IsBaselined = true;
        var budgetLine3 = new ProjectBudgetLine { ProjectId = project3.Id, FinancialYear = "2026/27", CostCategory = "Training delivery", FundingSource = "Discretionary grant" };
        budgetLine3.SetOriginal(12_000_000m); budgetLine3.IsBaselined = true;
        var budgetLine4 = new ProjectBudgetLine { ProjectId = project4.Id, FinancialYear = "2026/27", CostCategory = "Learner stipends", FundingSource = "Discretionary grant" };
        budgetLine4.SetOriginal(6_000_000m); budgetLine4.IsBaselined = true;
        var budgetLine5 = new ProjectBudgetLine { ProjectId = project5.Id, FinancialYear = "2026/27", CostCategory = "Infrastructure", FundingSource = "Administration budget" };
        budgetLine5.SetOriginal(15_000_000m); budgetLine5.IsBaselined = true; budgetLine5.ForecastAmount = 16_450_000m;
        var budgetLine6 = new ProjectBudgetLine { ProjectId = project6.Id, FinancialYear = "2026/27", CostCategory = "Training delivery", FundingSource = "Discretionary grant" };
        budgetLine6.SetOriginal(9_500_000m); budgetLine6.IsBaselined = true;
        var budgetLine7 = new ProjectBudgetLine { ProjectId = project7.Id, FinancialYear = "2025/26", CostCategory = "Training delivery", FundingSource = "Discretionary grant" };
        budgetLine7.SetOriginal(4_200_000m); budgetLine7.IsBaselined = true;
        var budgetLine8 = new ProjectBudgetLine { ProjectId = project8.Id, FinancialYear = "2024/25", CostCategory = "Training delivery", FundingSource = "Discretionary grant" };
        budgetLine8.SetOriginal(7_300_000m); budgetLine8.IsBaselined = true;
        _db.BudgetLines.AddRange(budgetLine2, budgetLine3, budgetLine4, budgetLine5, budgetLine6, budgetLine7, budgetLine8);

        // ---- Strategy alignment: link a few projects to APP indicators (FR-STR-004/005) ----
        _db.ProjectIndicatorLinks.AddRange(
            new ProjectIndicatorLink { ProjectId = project3.Id, IndicatorId = indicator.Id, Method = ContributionMethod.Direct, PlannedContribution = 200 },
            new ProjectIndicatorLink { ProjectId = project6.Id, IndicatorId = indicator.Id, Method = ContributionMethod.Direct, PlannedContribution = 90 },
            new ProjectIndicatorLink { ProjectId = project8.Id, IndicatorId = indicator.Id, Method = ContributionMethod.Direct, PlannedContribution = 80 },
            new ProjectIndicatorLink { ProjectId = project5.Id, IndicatorId = indicator2.Id, Method = ContributionMethod.Direct, PlannedContribution = 6 });
        _db.PerformanceResults.AddRange(
            new PerformanceResult { IndicatorId = indicator.Id, ProjectId = project3.Id, FinancialYear = "2026/27", Quarter = 1, Value = 45, Status = ResultStatus.Verified, VerifiedAtUtc = _clock.UtcNow.AddDays(-20), VerifiedBy = "seed" },
            new PerformanceResult { IndicatorId = indicator.Id, ProjectId = project3.Id, FinancialYear = "2026/27", Quarter = 2, Value = 55, Status = ResultStatus.Submitted },
            new PerformanceResult { IndicatorId = indicator.Id, ProjectId = project8.Id, FinancialYear = "2024/25", Quarter = 4, Value = 80, Status = ResultStatus.Verified, VerifiedAtUtc = new DateTime(2025, 5, 25), VerifiedBy = "seed" },
            new PerformanceResult { IndicatorId = indicator2.Id, ProjectId = project5.Id, FinancialYear = "2026/27", Quarter = 1, Value = 1, Status = ResultStatus.Captured, Narrative = "Head office site upgrade complete; regional sites in progress." });

        // ---- Project charters and stakeholders for every approved project ----
        foreach (var (p, purpose) in new[]
                 {
                     (project2, "Deliver 100 logistics learnerships against approved APP targets."), (project3, "Deliver RPL assessment and certification for 200 heavy-vehicle drivers."),
                     (project4, "Deliver bursaries and workplace placement for 40 engineering graduates."), (project5, "Upgrade ICT infrastructure at 6 regional offices."),
                     (project6, "Deliver 90 artisan placements in Ehlanzeni."), (project7, "Deliver a warehouse management skills programme for 60 learners."),
                     (project8, "Deliver 80 rail operations learnerships (closed).")
                 })
        {
            _db.ProjectCharters.Add(new ProjectCharter
            {
                ProjectId = p.Id, Purpose = purpose, Scope = "As approved in the business case.", GovernanceStructure = "Project Steering Committee chaired by the sponsor; monthly reporting to Head PMO.",
                Status = CharterStatus.Approved, ApprovedAtUtc = p.ApprovedAtUtc, ApprovedBy = "seed"
            });
            _db.ProjectStakeholders.Add(new ProjectStakeholder { ProjectId = p.Id, Name = p.SponsorName!, Role = StakeholderRole.Sponsor, Responsibility = "Executive sponsor", EffectiveFrom = today.AddYears(-1) });
            _db.ProjectStakeholders.Add(new ProjectStakeholder { ProjectId = p.Id, UserId = p.ManagerUserId, Name = p.ManagerName!, Role = StakeholderRole.ProjectManager, Responsibility = "Day-to-day delivery", EffectiveFrom = today.AddYears(-1) });
        }

        // ================================================================================
        // Procurement pipeline: one fully awarded contract, one adjudication pending CFO
        // decision, one mid-evaluation, one published with no bids yet, a second awarded
        // contract, and a cancelled-and-re-advertised pair.
        // ================================================================================
        var committeeA = new Committee { ProcurementId = null, Type = CommitteeType.BidEvaluation, Name = "Evaluation Committee - Skills Programmes", Quorum = 3 };
        _db.Committees.Add(committeeA);
        if (existingUsers.TryGetValue("teta.evaluator", out var evaluatorUser) && existingUsers.TryGetValue("teta.headscm", out var headScmUser))
        {
            _db.CommitteeMembers.AddRange(
                new CommitteeMember { CommitteeId = committeeA.Id, UserId = headScmUser.Id, Name = "Ayesha Patel (Head SCM)", Role = CommitteeRole.Chairperson, AccessFrom = today.AddYears(-1), AccessTo = today.AddYears(1) },
                new CommitteeMember { CommitteeId = committeeA.Id, UserId = evaluatorUser.Id, Name = "Committee Evaluator", Role = CommitteeRole.Member, AccessFrom = today.AddYears(-1), AccessTo = today.AddYears(1) });
        }

        // -- Procurement A: awarded, becomes Contract 1 (project3, healthy) --
        var reqA = new Requisition { Number = Num(NumberPrefixes.Requisition, 1), ProjectId = project3.Id, BudgetLineId = budgetLine3.Id, Title = "RPL assessment services - heavy vehicle drivers", Description = "Panel-based RPL assessment and gap training for 200 drivers.", EstimatedValue = 4_500_000m, RequiredByDate = today.AddMonths(-5), RecommendedMethod = "RFQ", SelectedMethod = "RFQ", BudgetAvailable = true, BudgetCheckResult = "Sufficient budget available.", Status = RequisitionStatus.Converted };
        var procA = new Procurement { Number = Num(NumberPrefixes.Procurement, 1), ProjectId = project3.Id, RequisitionId = reqA.Id, Title = reqA.Title, Method = "RFQ", EstimatedValue = 4_500_000m, Status = ProcurementStatus.Awarded, ClosingDateUtc = _clock.UtcNow.AddMonths(-5), BidOpeningAtUtc = _clock.UtcNow.AddMonths(-5).AddHours(1), TechnicalThreshold = 60, PriceSystemCode = "80/20", ActualAwardDate = today.AddMonths(-4) };
        reqA.ProcurementId = procA.Id;
        var specA = new Specification { ProcurementId = procA.Id, Title = "RPL assessment terms of reference", Content = "Scope, methodology and deliverables for RPL assessment services.", Status = SpecificationStatus.Approved, ApprovedAtUtc = _clock.UtcNow.AddMonths(-5).AddDays(-10), ApprovedBy = "Head SCM" };
        var pubA = new Publication { ProcurementId = procA.Id, Channel = "TETA website and national newspaper", Reference = "TETA/RFQ/2026/012", PublishedOn = today.AddMonths(-5).AddDays(-14), ClosingDateUtc = _clock.UtcNow.AddMonths(-5) };
        var bidA1 = new Bid { ProcurementId = procA.Id, SupplierId = supplier3.Id, BidReference = "BID-A-01", ReceivedAtUtc = _clock.UtcNow.AddMonths(-5).AddDays(-1), BidAmount = 4_350_000m, Status = BidStatus.Awarded, OpenedAtUtc = _clock.UtcNow.AddMonths(-5), ComplianceMet = true, TechnicalScore = 82, PricePoints = 76, PreferencePoints = 20, TotalPoints = 96, Rank = 1 };
        var bidA2 = new Bid { ProcurementId = procA.Id, SupplierId = supplier.Id, BidReference = "BID-A-02", ReceivedAtUtc = _clock.UtcNow.AddMonths(-5).AddDays(-1), BidAmount = 4_600_000m, Status = BidStatus.Unsuccessful, OpenedAtUtc = _clock.UtcNow.AddMonths(-5), ComplianceMet = true, TechnicalScore = 70, PricePoints = 72, PreferencePoints = 20, TotalPoints = 92, Rank = 2 };
        var ddA = new DueDiligenceCheck { ProcurementId = procA.Id, BidId = bidA1.Id, SupplierId = supplier3.Id, CsdVerified = true, TaxCompliant = true, NotRestricted = true, NotOnDefaultersList = true, ReferencesChecked = true, CapacityConfirmed = true, Outcome = DueDiligenceOutcome.Passed, CompletedAtUtc = _clock.UtcNow.AddMonths(-4).AddDays(-2), CompletedBy = "Head SCM" };
        var adjA = new Adjudication { ProcurementId = procA.Id, RecommendedBidId = bidA1.Id, DueDiligenceCheckId = ddA.Id, Recommendation = "Award to Sizanani Skills Academy as the highest-scoring compliant, responsive bid.", Decision = AdjudicationDecision.Approved, DecidedAtUtc = _clock.UtcNow.AddMonths(-4), DecidedBy = "Pieter Botha (CFO)" };
        var awardA = new Award { Number = Num(NumberPrefixes.Award, 1), ProcurementId = procA.Id, BidId = bidA1.Id, SupplierId = supplier3.Id, ProjectId = project3.Id, Amount = 4_350_000m, AwardDate = today.AddMonths(-4), Status = AwardStatus.Final, ConditionsSatisfied = true };
        var commA = new BidderCommunication { ProcurementId = procA.Id, BidId = bidA2.Id, SupplierId = supplier.Id, Type = CommunicationType.RegretLetter, Date = today.AddMonths(-4), Subject = "Outcome of RFQ TETA/RFQ/2026/012", Details = "Regret to advise your bid was not successful.", Outcome = "Unsuccessful" };
        _db.Requisitions.Add(reqA); _db.Procurements.Add(procA); _db.Specifications.Add(specA); _db.Publications.Add(pubA);
        _db.Bids.AddRange(bidA1, bidA2); _db.DueDiligenceChecks.Add(ddA); _db.Adjudications.Add(adjA); _db.Awards.Add(awardA); _db.BidderCommunications.Add(commA);

        // -- Procurement B: adjudication pending CFO decision (project4) --
        var reqB = new Requisition { Number = Num(NumberPrefixes.Requisition, 2), ProjectId = project4.Id, BudgetLineId = budgetLine4.Id, Title = "Workplace placement and mentorship services", Description = "Workplace placement and mentorship for 40 engineering graduates.", EstimatedValue = 2_500_000m, RequiredByDate = today.AddDays(21), RecommendedMethod = "RFQ", SelectedMethod = "RFQ", BudgetAvailable = true, BudgetCheckResult = "Sufficient budget available.", Status = RequisitionStatus.Converted };
        var procB = new Procurement { Number = Num(NumberPrefixes.Procurement, 2), ProjectId = project4.Id, RequisitionId = reqB.Id, Title = reqB.Title, Method = "RFQ", EstimatedValue = 2_500_000m, Status = ProcurementStatus.Adjudication, ClosingDateUtc = _clock.UtcNow.AddDays(-10), BidOpeningAtUtc = _clock.UtcNow.AddDays(-10).AddHours(1), TechnicalThreshold = 60, PriceSystemCode = "80/20" };
        reqB.ProcurementId = procB.Id;
        var specB = new Specification { ProcurementId = procB.Id, Title = "Workplace placement terms of reference", Content = "Scope and deliverables for graduate workplace placement services.", Status = SpecificationStatus.Approved, ApprovedAtUtc = _clock.UtcNow.AddDays(-24), ApprovedBy = "Head SCM" };
        var pubB = new Publication { ProcurementId = procB.Id, Channel = "TETA website", Reference = "TETA/RFQ/2026/031", PublishedOn = today.AddDays(-28), ClosingDateUtc = _clock.UtcNow.AddDays(-10) };
        var bidB1 = new Bid { ProcurementId = procB.Id, SupplierId = supplier6.Id, BidReference = "BID-B-01", ReceivedAtUtc = _clock.UtcNow.AddDays(-11), BidAmount = 2_420_000m, Status = BidStatus.Recommended, OpenedAtUtc = _clock.UtcNow.AddDays(-10), ComplianceMet = true, TechnicalScore = 78, PricePoints = 74, PreferencePoints = 20, TotalPoints = 94, Rank = 1 };
        var bidB2 = new Bid { ProcurementId = procB.Id, SupplierId = supplier4.Id, BidReference = "BID-B-02", ReceivedAtUtc = _clock.UtcNow.AddDays(-11), BidAmount = 2_600_000m, Status = BidStatus.Compliant, OpenedAtUtc = _clock.UtcNow.AddDays(-10), ComplianceMet = true, TechnicalScore = 65, PricePoints = 68, PreferencePoints = 20, TotalPoints = 88, Rank = 2 };
        var ddB = new DueDiligenceCheck { ProcurementId = procB.Id, BidId = bidB1.Id, SupplierId = supplier6.Id, CsdVerified = true, TaxCompliant = true, NotRestricted = true, NotOnDefaultersList = true, ReferencesChecked = true, CapacityConfirmed = true, Outcome = DueDiligenceOutcome.Passed, CompletedAtUtc = _clock.UtcNow.AddDays(-6), CompletedBy = "Head SCM" };
        var adjB = new Adjudication { ProcurementId = procB.Id, RecommendedBidId = bidB1.Id, DueDiligenceCheckId = ddB.Id, Recommendation = "Award to Ubuntu Consulting Engineers as the highest-scoring compliant, responsive bid.", Decision = AdjudicationDecision.Pending };
        _db.Requisitions.Add(reqB); _db.Procurements.Add(procB); _db.Specifications.Add(specB); _db.Publications.Add(pubB);
        _db.Bids.AddRange(bidB1, bidB2); _db.DueDiligenceChecks.Add(ddB); _db.Adjudications.Add(adjB);

        // -- Procurement C: mid-evaluation, not yet adjudicated (project5, the red project) --
        var reqC = new Requisition { Number = Num(NumberPrefixes.Requisition, 3), ProjectId = project5.Id, BudgetLineId = budgetLine5.Id, Title = "Regional office network and server refresh", Description = "Network and server hardware refresh at 6 regional offices.", EstimatedValue = 9_000_000m, RequiredByDate = today.AddMonths(-9), RecommendedMethod = "OPEN_TENDER", SelectedMethod = "OPEN_TENDER", BudgetAvailable = true, BudgetCheckResult = "Sufficient budget available.", Status = RequisitionStatus.Converted };
        var procC = new Procurement { Number = Num(NumberPrefixes.Procurement, 3), ProjectId = project5.Id, RequisitionId = reqC.Id, Title = reqC.Title, Method = "OPEN_TENDER", EstimatedValue = 9_000_000m, Status = ProcurementStatus.Evaluation, ClosingDateUtc = _clock.UtcNow.AddMonths(-8), BidOpeningAtUtc = _clock.UtcNow.AddMonths(-8).AddHours(1), TechnicalThreshold = 60, PriceSystemCode = "80/20" };
        reqC.ProcurementId = procC.Id;
        var specC = new Specification { ProcurementId = procC.Id, Title = "ICT infrastructure refresh terms of reference", Content = "Scope, technical specification and SLAs for the ICT infrastructure refresh.", Status = SpecificationStatus.Approved, ApprovedAtUtc = _clock.UtcNow.AddMonths(-8).AddDays(-21), ApprovedBy = "Head SCM" };
        var pubC = new Publication { ProcurementId = procC.Id, Channel = "TETA website and national newspaper", Reference = "TETA/OT/2026/004", PublishedOn = today.AddMonths(-8).AddDays(-25), ClosingDateUtc = _clock.UtcNow.AddMonths(-8) };
        var bidC1 = new Bid { ProcurementId = procC.Id, SupplierId = supplier4.Id, BidReference = "BID-C-01", ReceivedAtUtc = _clock.UtcNow.AddMonths(-8).AddDays(-1), BidAmount = 8_950_000m, Status = BidStatus.Compliant, OpenedAtUtc = _clock.UtcNow.AddMonths(-8), ComplianceMet = true, TechnicalScore = 71 };
        var bidC2 = new Bid { ProcurementId = procC.Id, SupplierId = supplier6.Id, BidReference = "BID-C-02", ReceivedAtUtc = _clock.UtcNow.AddMonths(-8).AddDays(-1), BidAmount = 9_400_000m, Status = BidStatus.Compliant, OpenedAtUtc = _clock.UtcNow.AddMonths(-8), ComplianceMet = true, TechnicalScore = 68 };
        var evalCritC1 = new EvaluationCriterion { ProcurementId = procC.Id, Stage = EvaluationStage.Compliance, Name = "Valid tax clearance and CSD registration", Weight = 0, MaxScore = 1, IsMandatory = true, SortOrder = 1 };
        var evalCritC2 = new EvaluationCriterion { ProcurementId = procC.Id, Stage = EvaluationStage.Technical, Name = "Technical approach and methodology", Weight = 60, MaxScore = 5, IsMandatory = false, SortOrder = 2 };
        var evalCritC3 = new EvaluationCriterion { ProcurementId = procC.Id, Stage = EvaluationStage.Technical, Name = "Relevant experience and capacity", Weight = 40, MaxScore = 5, IsMandatory = false, SortOrder = 3 };
        _db.Requisitions.Add(reqC); _db.Procurements.Add(procC); _db.Specifications.Add(specC); _db.Publications.Add(pubC); _db.Bids.AddRange(bidC1, bidC2);
        _db.EvaluationCriteria.AddRange(evalCritC1, evalCritC2, evalCritC3);
        if (evaluatorUser is not null)
        {
            _db.EvaluationScores.AddRange(
                new EvaluationScore { BidId = bidC1.Id, CriterionId = evalCritC2.Id, EvaluatorUserId = evaluatorUser.Id, EvaluatorName = "Committee Evaluator", Score = 4, Comment = "Sound methodology, minor gaps in rollout sequencing.", ScoredAtUtc = _clock.UtcNow.AddMonths(-7) },
                new EvaluationScore { BidId = bidC1.Id, CriterionId = evalCritC3.Id, EvaluatorUserId = evaluatorUser.Id, EvaluatorName = "Committee Evaluator", Score = 4, Comment = "Strong relevant experience.", ScoredAtUtc = _clock.UtcNow.AddMonths(-7) },
                new EvaluationScore { BidId = bidC2.Id, CriterionId = evalCritC2.Id, EvaluatorUserId = evaluatorUser.Id, EvaluatorName = "Committee Evaluator", Score = 3, Comment = "Adequate but generic methodology.", ScoredAtUtc = _clock.UtcNow.AddMonths(-7) },
                new EvaluationScore { BidId = bidC2.Id, CriterionId = evalCritC3.Id, EvaluatorUserId = evaluatorUser.Id, EvaluatorName = "Committee Evaluator", Score = 3, Comment = "Limited regional office experience.", ScoredAtUtc = _clock.UtcNow.AddMonths(-7) });
        }

        // -- Procurement D: published, no bids received yet (second requirement for project4) --
        var reqD = new Requisition { Number = Num(NumberPrefixes.Requisition, 4), ProjectId = project4.Id, BudgetLineId = budgetLine4.Id, Title = "Bursary learner welfare support services", Description = "Counselling and welfare support services for bursary learners.", EstimatedValue = 450_000m, RequiredByDate = today.AddDays(30), RecommendedMethod = "QUOTATIONS", SelectedMethod = "QUOTATIONS", BudgetAvailable = true, BudgetCheckResult = "Sufficient budget available.", Status = RequisitionStatus.Converted };
        var procD = new Procurement { Number = Num(NumberPrefixes.Procurement, 4), ProjectId = project4.Id, RequisitionId = reqD.Id, Title = reqD.Title, Method = "QUOTATIONS", EstimatedValue = 450_000m, Status = ProcurementStatus.Published, ClosingDateUtc = _clock.UtcNow.AddDays(7) };
        reqD.ProcurementId = procD.Id;
        var specD = new Specification { ProcurementId = procD.Id, Title = "Learner welfare support terms of reference", Content = "Scope of counselling and welfare support services.", Status = SpecificationStatus.Approved, ApprovedAtUtc = _clock.UtcNow.AddDays(-3), ApprovedBy = "Head SCM" };
        var pubD = new Publication { ProcurementId = procD.Id, Channel = "TETA website", Reference = "TETA/QUOT/2026/058", PublishedOn = today.AddDays(-2), ClosingDateUtc = _clock.UtcNow.AddDays(7) };
        _db.Requisitions.Add(reqD); _db.Procurements.Add(procD); _db.Specifications.Add(specD); _db.Publications.Add(pubD);

        // -- Procurement E: awarded, becomes Contract 2 (project6, has a breach and expires soon) --
        var reqE = new Requisition { Number = Num(NumberPrefixes.Requisition, 5), ProjectId = project6.Id, BudgetLineId = budgetLine6.Id, Title = "Artisan trade testing and workplace placement - Nelspruit", Description = "Trade testing and workplace placement for 90 artisan learners.", EstimatedValue = 6_800_000m, RequiredByDate = today.AddMonths(-10), RecommendedMethod = "RFQ", SelectedMethod = "RFQ", BudgetAvailable = true, BudgetCheckResult = "Sufficient budget available.", Status = RequisitionStatus.Converted };
        var procE = new Procurement { Number = Num(NumberPrefixes.Procurement, 5), ProjectId = project6.Id, RequisitionId = reqE.Id, Title = reqE.Title, Method = "RFQ", EstimatedValue = 6_800_000m, Status = ProcurementStatus.Awarded, ClosingDateUtc = _clock.UtcNow.AddMonths(-10), BidOpeningAtUtc = _clock.UtcNow.AddMonths(-10).AddHours(1), TechnicalThreshold = 60, PriceSystemCode = "80/20", ActualAwardDate = today.AddMonths(-9) };
        reqE.ProcurementId = procE.Id;
        var specE = new Specification { ProcurementId = procE.Id, Title = "Artisan trade testing terms of reference", Content = "Scope, methodology and deliverables for trade testing and placement.", Status = SpecificationStatus.Approved, ApprovedAtUtc = _clock.UtcNow.AddMonths(-10).AddDays(-10), ApprovedBy = "Head SCM" };
        var pubE = new Publication { ProcurementId = procE.Id, Channel = "TETA website and regional newspaper", Reference = "TETA/RFQ/2025/098", PublishedOn = today.AddMonths(-10).AddDays(-14), ClosingDateUtc = _clock.UtcNow.AddMonths(-10) };
        var bidE1 = new Bid { ProcurementId = procE.Id, SupplierId = supplier2.Id, BidReference = "BID-E-01", ReceivedAtUtc = _clock.UtcNow.AddMonths(-10).AddDays(-1), BidAmount = 6_700_000m, Status = BidStatus.Awarded, OpenedAtUtc = _clock.UtcNow.AddMonths(-10), ComplianceMet = true, TechnicalScore = 75, PricePoints = 74, PreferencePoints = 18, TotalPoints = 92, Rank = 1 };
        var ddE = new DueDiligenceCheck { ProcurementId = procE.Id, BidId = bidE1.Id, SupplierId = supplier2.Id, CsdVerified = true, TaxCompliant = true, NotRestricted = true, NotOnDefaultersList = true, ReferencesChecked = true, CapacityConfirmed = true, Outcome = DueDiligenceOutcome.Passed, CompletedAtUtc = _clock.UtcNow.AddMonths(-9).AddDays(-2), CompletedBy = "Head SCM" };
        var adjE = new Adjudication { ProcurementId = procE.Id, RecommendedBidId = bidE1.Id, DueDiligenceCheckId = ddE.Id, Recommendation = "Award to Vhutali Construction and Projects as the sole compliant, responsive bid.", Decision = AdjudicationDecision.Approved, DecidedAtUtc = _clock.UtcNow.AddMonths(-9), DecidedBy = "Pieter Botha (CFO)" };
        var awardE = new Award { Number = Num(NumberPrefixes.Award, 2), ProcurementId = procE.Id, BidId = bidE1.Id, SupplierId = supplier2.Id, ProjectId = project6.Id, Amount = 6_700_000m, AwardDate = today.AddMonths(-9), Status = AwardStatus.Final, ConditionsSatisfied = true };
        _db.Requisitions.Add(reqE); _db.Procurements.Add(procE); _db.Specifications.Add(specE); _db.Publications.Add(pubE); _db.Bids.Add(bidE1); _db.DueDiligenceChecks.Add(ddE); _db.Adjudications.Add(adjE); _db.Awards.Add(awardE);

        // -- Procurement F/G: cancelled and re-advertised (project2, still in planning) --
        var reqF = new Requisition { Number = Num(NumberPrefixes.Requisition, 6), ProjectId = project2.Id, BudgetLineId = budgetLine2.Id, Title = "Learner recruitment and screening services", Description = "Recruitment, screening and induction of 100 logistics learners.", EstimatedValue = 350_000m, RequiredByDate = today.AddDays(45), RecommendedMethod = "QUOTATIONS", SelectedMethod = "QUOTATIONS", BudgetAvailable = true, BudgetCheckResult = "Sufficient budget available.", Status = RequisitionStatus.Converted };
        var procF = new Procurement { Number = Num(NumberPrefixes.Procurement, 6), ProjectId = project2.Id, RequisitionId = reqF.Id, Title = reqF.Title, Method = "QUOTATIONS", EstimatedValue = 350_000m, Status = ProcurementStatus.Cancelled, ClosingDateUtc = _clock.UtcNow.AddDays(-15), CancellationReason = "Fewer than the minimum 3 quotations were received; re-advertised for a wider response.", CancelledAtUtc = _clock.UtcNow.AddDays(-10) };
        reqF.ProcurementId = procF.Id;
        var pubF = new Publication { ProcurementId = procF.Id, Channel = "TETA website", Reference = "TETA/QUOT/2026/041", PublishedOn = today.AddDays(-22), ClosingDateUtc = _clock.UtcNow.AddDays(-15) };
        var procG = new Procurement { Number = Num(NumberPrefixes.Procurement, 7), ProjectId = project2.Id, Title = reqF.Title, Method = "QUOTATIONS", EstimatedValue = 350_000m, Status = ProcurementStatus.Published, ClosingDateUtc = _clock.UtcNow.AddDays(5), ReAdvertisedFromId = procF.Id };
        procF.ReAdvertisedAsId = procG.Id;
        var pubG = new Publication { ProcurementId = procG.Id, Channel = "TETA website and community radio", Reference = "TETA/QUOT/2026/047", PublishedOn = today.AddDays(-8), ClosingDateUtc = _clock.UtcNow.AddDays(5) };
        _db.Requisitions.Add(reqF); _db.Procurements.AddRange(procF, procG); _db.Publications.AddRange(pubF, pubG);

        // ================================================================================
        // Contracts: one healthy active contract with an approved variation, one active
        // contract with an open breach and near-term expiry, one closed non-bid contract,
        // and one closed historical contract.
        // ================================================================================
        var contract1 = new Contract
        {
            ContractNumber = Num(NumberPrefixes.Contract, 1), Title = "RPL assessment services - heavy vehicle drivers", ProjectId = project3.Id, ProcurementId = procA.Id, AwardId = awardA.Id,
            SupplierId = supplier3.Id, Source = ContractSource.Award, OriginalValue = 4_350_000m, StartDate = today.AddMonths(-4), OriginalEndDate = today.AddMonths(8), CurrentEndDate = today.AddMonths(8),
            Status = ContractStatus.Active, ContractManagerName = "Naledi Khumalo", ContractManagerUserId = existingUsers.TryGetValue("teta.contracts", out var contractsUser) ? contractsUser.Id : null,
            SignatureStatus = SignatureStatus.Signed, SignedDate = today.AddMonths(-4).AddDays(3), PoReference = "PO-2026-3301", PerformanceRating = 4.3m
        };
        awardA.ContractId = contract1.Id;
        contract1.ApplyApprovedVariation(150_000m, today.AddMonths(9));
        var variation1 = new ContractVariation
        {
            Number = Num(NumberPrefixes.Variation, 1), ContractId = contract1.Id, Type = VariationType.Combined, IsExtension = true, Description = "Extend scope to cover an additional intake of 20 drivers and extend the end date by one month.",
            Reason = "Additional demand identified after the original cohort was oversubscribed.", Amount = 150_000m, Days = 30, RevisedEndDate = today.AddMonths(9), ValueBefore = 4_350_000m, ValueAfter = 4_500_000m,
            EndDateBefore = today.AddMonths(8), EndDateAfter = today.AddMonths(9), Status = ApprovalState.Approved, DecidedAtUtc = _clock.UtcNow.AddDays(-30), DecidedBy = "Pieter Botha (CFO)"
        };
        _db.Contracts.Add(contract1); _db.ContractVariations.Add(variation1);
        _db.ContractObligations.AddRange(
            new ContractObligation { ContractId = contract1.Id, Type = ObligationType.Deliverable, Description = "Assess and certify cohort 1 (100 drivers)", OwnerName = "Naledi Khumalo", DueDate = today.AddMonths(-1), Status = ObligationStatus.Met },
            new ContractObligation { ContractId = contract1.Id, Type = ObligationType.Deliverable, Description = "Assess and certify cohort 2 (100 drivers)", OwnerName = "Naledi Khumalo", DueDate = today.AddMonths(3), Status = ObligationStatus.Open },
            new ContractObligation { ContractId = contract1.Id, Type = ObligationType.Reporting, Description = "Monthly progress report", OwnerName = "Sizanani Skills Academy", DueDate = today.AddDays(5), Status = ObligationStatus.Open });
        var deliverable1a = new Deliverable { Number = Num(NumberPrefixes.Deliverable, 1), ProjectId = project3.Id, ContractId = contract1.Id, Name = "Cohort 1 assessment and certification report", DueDate = today.AddMonths(-1), PayableAmount = 2_200_000m, AcceptanceStatus = AcceptanceStatus.Accepted, SubmittedAtUtc = _clock.UtcNow.AddMonths(-1).AddDays(-3), AcceptedAtUtc = _clock.UtcNow.AddMonths(-1), AcceptedBy = "Naledi Khumalo" };
        var deliverable1b = new Deliverable { Number = Num(NumberPrefixes.Deliverable, 2), ProjectId = project3.Id, ContractId = contract1.Id, Name = "Cohort 2 assessment and certification report", DueDate = today.AddMonths(3), PayableAmount = 2_300_000m, AcceptanceStatus = AcceptanceStatus.Pending };
        _db.Deliverables.AddRange(deliverable1a, deliverable1b);
        _db.PaymentScheduleItems.AddRange(
            new PaymentScheduleItem { ContractId = contract1.Id, DeliverableId = deliverable1a.Id, Description = "Payment on acceptance of cohort 1", Amount = 2_200_000m, PlannedDate = today.AddMonths(-1) },
            new PaymentScheduleItem { ContractId = contract1.Id, DeliverableId = deliverable1b.Id, Description = "Payment on acceptance of cohort 2", Amount = 2_300_000m, PlannedDate = today.AddMonths(3) });
        _db.ContractPerformanceReviews.Add(new ContractPerformanceReview { ContractId = contract1.Id, SupplierId = supplier3.Id, Period = "2026 Q2", ReviewDate = today.AddDays(-20), QualityScore = 4.5m, TimelinessScore = 4, ComplianceScore = 4.5m, OverallScore = 4.33m, Rating = "Excellent", ReviewedBy = "Naledi Khumalo" });

        var contract2 = new Contract
        {
            ContractNumber = Num(NumberPrefixes.Contract, 2), Title = "Artisan trade testing and workplace placement - Nelspruit", ProjectId = project6.Id, ProcurementId = procE.Id, AwardId = awardE.Id,
            SupplierId = supplier2.Id, Source = ContractSource.Award, OriginalValue = 6_700_000m, StartDate = today.AddMonths(-9), OriginalEndDate = today.AddDays(45), CurrentEndDate = today.AddDays(45),
            Status = ContractStatus.Active, ContractManagerName = "Naledi Khumalo", ContractManagerUserId = contractsUser?.Id, SignatureStatus = SignatureStatus.Signed, SignedDate = today.AddMonths(-9).AddDays(2),
            PoReference = "PO-2025-2987", PerformanceRating = 2.8m
        };
        awardE.ContractId = contract2.Id;
        _db.Contracts.Add(contract2);
        _db.ContractObligations.Add(new ContractObligation { ContractId = contract2.Id, Type = ObligationType.Deliverable, Description = "Complete trade testing for all 90 learners", OwnerName = "Naledi Khumalo", DueDate = today.AddDays(30), Status = ObligationStatus.Open });
        var deliverable2 = new Deliverable { Number = Num(NumberPrefixes.Deliverable, 3), ProjectId = project6.Id, ContractId = contract2.Id, Name = "Trade testing and placement completion report", DueDate = today.AddDays(30), PayableAmount = 6_700_000m, AcceptanceStatus = AcceptanceStatus.Submitted, SubmittedAtUtc = _clock.UtcNow.AddDays(-2) };
        _db.Deliverables.Add(deliverable2);
        _db.PaymentScheduleItems.Add(new PaymentScheduleItem { ContractId = contract2.Id, DeliverableId = deliverable2.Id, Description = "Final payment on completion", Amount = 6_700_000m, PlannedDate = today.AddDays(30) });
        _db.ContractBreaches.Add(new ContractBreach { ContractId = contract2.Id, Description = "Trade-testing site visits fell behind the agreed schedule by 6 weeks with no advance notice given.", Severity = Severity.Medium, IdentifiedOn = today.AddDays(-25), NoticeDate = today.AddDays(-18), NoticeReference = "CM-NOTICE-2026-014", Remedy = "Supplier to submit a recovery plan and complete outstanding site visits within 30 days.", RemedyDueDate = today.AddDays(10), Status = BreachStatus.NoticeIssued });
        _db.ContractPerformanceReviews.Add(new ContractPerformanceReview { ContractId = contract2.Id, SupplierId = supplier2.Id, Period = "2026 Q2", ReviewDate = today.AddDays(-20), QualityScore = 3, TimelinessScore = 2, ComplianceScore = 3, OverallScore = 2.67m, Rating = "Satisfactory", ReviewedBy = "Naledi Khumalo" });

        // Non-bid contract, closed together with project7's close-out.
        var exception1 = new ProcurementException { Number = Num(NumberPrefixes.ProcurementException, 1), ProjectId = project7.Id, Type = ExceptionType.SingleSource, Motivation = "Only one accredited provider offers the specialised warehouse-management curriculum in the region.", Authority = "Head SCM", Value = 4_100_000m, Status = ApprovalState.Approved, DecidedAtUtc = _clock.UtcNow.AddMonths(-13), DecidedBy = "Ayesha Patel (Head SCM)" };
        _db.ProcurementExceptions.Add(exception1);
        var contract3 = new Contract
        {
            ContractNumber = Num(NumberPrefixes.Contract, 3), Title = "Warehouse management skills programme delivery", ProjectId = project7.Id, SupplierId = supplier5.Id, Source = ContractSource.NonBid,
            NonBidAuthority = $"Approved single-source exception {exception1.Number}", OriginalValue = 4_100_000m, StartDate = today.AddMonths(-13), OriginalEndDate = today.AddDays(-5), CurrentEndDate = today.AddDays(-5),
            Status = ContractStatus.Closed, ContractManagerName = "Naledi Khumalo", ContractManagerUserId = contractsUser?.Id, SignatureStatus = SignatureStatus.Signed, SignedDate = today.AddMonths(-13).AddDays(5),
            PerformanceRating = 4.0m, ClosedAtUtc = _clock.UtcNow.AddDays(-4), ClosureNotes = "All deliverables accepted, final invoice paid, no open obligations."
        };
        _db.Contracts.Add(contract3);
        var deliverable3 = new Deliverable { Number = Num(NumberPrefixes.Deliverable, 4), ProjectId = project7.Id, ContractId = contract3.Id, Name = "Warehouse management training completion report", DueDate = today.AddDays(-10), PayableAmount = 4_100_000m, AcceptanceStatus = AcceptanceStatus.Accepted, SubmittedAtUtc = _clock.UtcNow.AddDays(-12), AcceptedAtUtc = _clock.UtcNow.AddDays(-9), AcceptedBy = "Naledi Khumalo" };
        _db.Deliverables.Add(deliverable3);
        _db.ContractPerformanceReviews.Add(new ContractPerformanceReview { ContractId = contract3.Id, SupplierId = supplier5.Id, Period = "2026 Q2", ReviewDate = today.AddDays(-8), QualityScore = 4, TimelinessScore = 4, ComplianceScore = 4, OverallScore = 4, Rating = "Good", ReviewedBy = "Naledi Khumalo" });

        // Closed historical contract for project8 (2024/25).
        var contract4 = new Contract
        {
            ContractNumber = Num(NumberPrefixes.Contract, 4), Title = "Rail operations learnership delivery (2024/25)", ProjectId = project8.Id, SupplierId = supplier.Id, Source = ContractSource.Award,
            OriginalValue = 7_300_000m, StartDate = new DateOnly(2024, 7, 1), OriginalEndDate = new DateOnly(2025, 5, 31), CurrentEndDate = new DateOnly(2025, 5, 31), Status = ContractStatus.Closed,
            ContractManagerName = "Naledi Khumalo", SignatureStatus = SignatureStatus.Signed, SignedDate = new DateOnly(2024, 7, 5), PerformanceRating = 4.2m,
            ClosedAtUtc = new DateTime(2025, 6, 10), ClosureNotes = "Closed on time; 78 of 80 learners completed successfully."
        };
        _db.Contracts.Add(contract4);
        var deliverable4 = new Deliverable { Number = Num(NumberPrefixes.Deliverable, 5), ProjectId = project8.Id, ContractId = contract4.Id, Name = "Rail operations learnership completion report", DueDate = new DateOnly(2025, 5, 15), PayableAmount = 7_300_000m, AcceptanceStatus = AcceptanceStatus.Accepted, SubmittedAtUtc = new DateTime(2025, 5, 18), AcceptedAtUtc = new DateTime(2025, 5, 22), AcceptedBy = "Naledi Khumalo" };
        _db.Deliverables.Add(deliverable4);
        _db.ContractPerformanceReviews.Add(new ContractPerformanceReview { ContractId = contract4.Id, SupplierId = supplier.Id, Period = "2025 Q1", ReviewDate = new DateOnly(2025, 4, 20), QualityScore = 4.5m, TimelinessScore = 4, ComplianceScore = 4, OverallScore = 4.17m, Rating = "Good", ReviewedBy = "Naledi Khumalo" });

        // ================================================================================
        // Finance: commitments, invoices in every status, one paid, one certified, one
        // pending certification, one failed validation (over-billed), plus an ERP batch.
        // ================================================================================
        _db.Commitments.AddRange(
            new Commitment { ProjectId = project3.Id, ContractId = contract1.Id, PoReference = contract1.PoReference, FinancialYear = "2026/27", Amount = 4_500_000m, CommitmentDate = today.AddMonths(-4), Source = CommitmentSource.Contract },
            new Commitment { ProjectId = project6.Id, ContractId = contract2.Id, PoReference = contract2.PoReference, FinancialYear = "2026/27", Amount = 6_700_000m, CommitmentDate = today.AddMonths(-9), Source = CommitmentSource.Contract },
            new Commitment { ProjectId = project7.Id, ContractId = contract3.Id, FinancialYear = "2025/26", Amount = 4_100_000m, CommitmentDate = today.AddMonths(-13), Source = CommitmentSource.Contract, IsReleased = true },
            new Commitment { ProjectId = project8.Id, ContractId = contract4.Id, FinancialYear = "2024/25", Amount = 7_300_000m, CommitmentDate = new DateOnly(2024, 7, 5), Source = CommitmentSource.Contract, IsReleased = true });

        var invoice1 = new Invoice { Number = Num(NumberPrefixes.Invoice, 1), SupplierInvoiceNumber = "SSA-INV-2201", ContractId = contract1.Id, DeliverableId = deliverable1a.Id, SupplierId = supplier3.Id, ProjectId = project3.Id, PoReference = contract1.PoReference, InvoiceDate = today.AddMonths(-1).AddDays(-4), ReceivedDate = today.AddMonths(-1).AddDays(-3), Amount = 2_200_000m, VatAmount = 330_000m, Status = InvoiceStatus.Paid, CertifiedAtUtc = _clock.UtcNow.AddMonths(-1).AddDays(2), CertifiedBy = "Kagiso Molefe (Finance Officer)" };
        var invoice2 = new Invoice { Number = Num(NumberPrefixes.Invoice, 2), SupplierInvoiceNumber = "SSA-INV-2244", ContractId = contract1.Id, SupplierId = supplier3.Id, ProjectId = project3.Id, PoReference = contract1.PoReference, InvoiceDate = today.AddDays(-10), ReceivedDate = today.AddDays(-9), Amount = 500_000m, VatAmount = 75_000m, Status = InvoiceStatus.PendingCertification, WorkflowInstanceId = null };
        var invoice3 = new Invoice { Number = Num(NumberPrefixes.Invoice, 3), SupplierInvoiceNumber = "VCP-INV-0871", ContractId = contract2.Id, SupplierId = supplier2.Id, ProjectId = project6.Id, PoReference = contract2.PoReference, InvoiceDate = today.AddDays(-15), ReceivedDate = today.AddDays(-14), Amount = 7_100_000m, VatAmount = 1_065_000m, Status = InvoiceStatus.ValidationFailed, ValidationMessages = "Invoice amount (R7,100,000) exceeds the contract's remaining payable ceiling for a deliverable that has not yet been accepted (FR-CON-009/FR-FIN-005)." };
        var invoice4 = new Invoice { Number = Num(NumberPrefixes.Invoice, 4), SupplierInvoiceNumber = "VCP-INV-0844", ContractId = contract2.Id, SupplierId = supplier2.Id, ProjectId = project6.Id, PoReference = contract2.PoReference, InvoiceDate = today.AddMonths(-3), ReceivedDate = today.AddMonths(-3).AddDays(1), Amount = 1_500_000m, VatAmount = 225_000m, Status = InvoiceStatus.Certified, CertifiedAtUtc = _clock.UtcNow.AddMonths(-3).AddDays(3), CertifiedBy = "Kagiso Molefe (Finance Officer)" };
        var invoice5 = new Invoice { Number = Num(NumberPrefixes.Invoice, 5), SupplierInvoiceNumber = "KLF-INV-3390", ContractId = contract3.Id, SupplierId = supplier5.Id, ProjectId = project7.Id, InvoiceDate = today.AddDays(-9), ReceivedDate = today.AddDays(-8), Amount = 4_100_000m, VatAmount = 615_000m, Status = InvoiceStatus.Paid, CertifiedAtUtc = _clock.UtcNow.AddDays(-6), CertifiedBy = "Kagiso Molefe (Finance Officer)" };
        var invoice6 = new Invoice { Number = Num(NumberPrefixes.Invoice, 6), SupplierInvoiceNumber = "DTP-INV-1102", ContractId = contract4.Id, SupplierId = supplier.Id, ProjectId = project8.Id, InvoiceDate = new DateOnly(2025, 5, 20), ReceivedDate = new DateOnly(2025, 5, 21), Amount = 7_300_000m, VatAmount = 1_095_000m, Status = InvoiceStatus.Paid, CertifiedAtUtc = new DateTime(2025, 5, 25), CertifiedBy = "Kagiso Molefe (Finance Officer)" };
        _db.Invoices.AddRange(invoice1, invoice2, invoice3, invoice4, invoice5, invoice6);
        _db.Payments.AddRange(
            new Payment { InvoiceId = invoice1.Id, ProjectId = project3.Id, ErpReference = "ERP-PAY-88231", Amount = 2_200_000m, PaymentDate = today.AddMonths(-1).AddDays(5), Status = PaymentStatus.Paid, BankReference = "EFT-88231" },
            new Payment { InvoiceId = invoice5.Id, ProjectId = project7.Id, ErpReference = "ERP-PAY-88390", Amount = 4_100_000m, PaymentDate = today.AddDays(-4), Status = PaymentStatus.Paid, BankReference = "EFT-88390" },
            new Payment { InvoiceId = invoice6.Id, ProjectId = project8.Id, ErpReference = "ERP-PAY-79102", Amount = 7_300_000m, PaymentDate = new DateOnly(2025, 5, 30), Status = PaymentStatus.Paid, BankReference = "EFT-79102" });

        _db.CostForecasts.AddRange(
            new CostForecast { ProjectId = project5.Id, RecordedAtUtc = _clock.UtcNow.AddMonths(-3), BudgetAtCompletion = 15_000_000m, EstimateAtCompletion = 15_600_000m, Variance = 600_000m, VariancePercent = 4.0m, Commentary = "Early sign of cost pressure on the network hardware line item.", RecordedBy = "Kagiso Molefe (Finance Officer)" },
            new CostForecast { ProjectId = project5.Id, RecordedAtUtc = _clock.UtcNow.AddDays(-15), BudgetAtCompletion = 15_000_000m, EstimateAtCompletion = 16_450_000m, Variance = 1_450_000m, VariancePercent = 9.7m, Commentary = "Confirmed overrun following the site-access delay and supplier remediation costs.", RecordedBy = "Kagiso Molefe (Finance Officer)" });

        var erpBatchId = Guid.NewGuid();
        _db.ErpInterfaceMessages.AddRange(
            new ErpInterfaceMessage { Direction = InterfaceDirection.Inbound, MessageType = "Payment", ExternalReference = "ERP-PAY-88231", BatchId = erpBatchId, PayloadJson = "{\"invoiceNumber\":\"" + invoice1.Number + "\",\"amount\":2200000}", ControlAmount = 2_200_000m, Status = InterfaceStatus.Reconciled, ProcessedAtUtc = _clock.UtcNow.AddMonths(-1).AddDays(5) },
            new ErpInterfaceMessage { Direction = InterfaceDirection.Inbound, MessageType = "Payment", ExternalReference = "ERP-PAY-88390", BatchId = erpBatchId, PayloadJson = "{\"invoiceNumber\":\"" + invoice5.Number + "\",\"amount\":4100000}", ControlAmount = 4_100_000m, Status = InterfaceStatus.Reconciled, ProcessedAtUtc = _clock.UtcNow.AddDays(-4) });
        _db.ErpReconciliations.Add(new ErpReconciliation { BatchId = erpBatchId, RunAtUtc = _clock.UtcNow.AddDays(-4), MessageType = "Payment", ExpectedCount = 2, ProcessedCount = 2, ErrorCount = 0, ExpectedTotal = 6_300_000m, ProcessedTotal = 6_300_000m, Balanced = true });

        // ================================================================================
        // Execution: WBS, progress, resources, issues, dependencies, a change request,
        // health snapshots, closure and a completed benefit review.
        // ================================================================================
        var ws3 = new WbsElement { ProjectId = project3.Id, Type = WbsType.Workstream, Code = "WS1", Name = "RPL assessment delivery", PlannedStart = project3.PlannedStart, PlannedEnd = project3.PlannedEnd, Status = WorkStatus.InProgress, Weight = 1 };
        var ms3a = new WbsElement { ProjectId = project3.Id, ParentId = ws3.Id, Type = WbsType.Milestone, Code = "WS1-M1", Name = "Cohort 1 assessed and certified", PlannedStart = today.AddMonths(-4), PlannedEnd = today.AddMonths(-1), BaselineStart = today.AddMonths(-4), BaselineEnd = today.AddMonths(-1), ActualStart = today.AddMonths(-4), ActualEnd = today.AddMonths(-1), PercentComplete = 100, Status = WorkStatus.Completed, EvidenceRequired = true, AcceptanceCriteria = "Signed acceptance of cohort 1 completion report." };
        var ms3b = new WbsElement { ProjectId = project3.Id, ParentId = ws3.Id, Type = WbsType.Milestone, Code = "WS1-M2", Name = "Cohort 2 assessed and certified", PlannedStart = today.AddMonths(-1), PlannedEnd = today.AddMonths(3), ForecastEnd = today.AddMonths(3), PercentComplete = 40, Status = WorkStatus.InProgress, EvidenceRequired = true, AcceptanceCriteria = "Signed acceptance of cohort 2 completion report." };
        var task3a = new WbsElement { ProjectId = project3.Id, ParentId = ms3b.Id, Type = WbsType.Task, Code = "WS1-M2-T1", Name = "Assess remaining 60 drivers", PlannedStart = today.AddDays(-10), PlannedEnd = today.AddDays(20), PercentComplete = 40, Status = WorkStatus.InProgress };
        _db.WbsElements.AddRange(ws3, ms3a, ms3b, task3a);
        _db.WbsDependencies.Add(new WbsDependency { ProjectId = project3.Id, PredecessorId = ms3a.Id, SuccessorId = ms3b.Id, Type = DependencyType.FS });
        _db.ScheduleBaselines.Add(new ScheduleBaseline { ProjectId = project3.Id, BaselineNumber = 0, ApprovedAtUtc = _clock.UtcNow.AddMonths(-6), ApprovedBy = "Sipho Nkosi", SnapshotJson = "[]", PlannedEnd = project3.PlannedEnd, BudgetAtBaseline = project3.ApprovedBudget });
        _db.ProgressUpdates.Add(new ProgressUpdate { WbsElementId = ms3b.Id, ProjectId = project3.Id, RecordedAtUtc = _clock.UtcNow.AddDays(-10), PercentComplete = 40, Comment = "60 of 100 remaining drivers assessed to date.", RecordedBy = "Sipho Nkosi" });
        _db.ResourceAssignments.Add(new ResourceAssignment { WbsElementId = ms3b.Id, ProjectId = project3.Id, UserId = pmUser?.Id, ResourceName = "Sipho Nkosi", Role = "Project Manager", AllocationPercent = 50 });
        _db.Issues.Add(new Issue { Number = Num(NumberPrefixes.Issue, 1), ProjectId = project3.Id, Title = "Assessor availability in outlying areas", Description = "Two assessors have limited availability for site visits to outlying depots.", Severity = Severity.Low, OwnerName = "Sipho Nkosi", Action = "Engage a third assessor on a part-time basis.", DueDate = today.AddDays(14), Status = IssueStatus.Open });

        var ws4 = new WbsElement { ProjectId = project4.Id, Type = WbsType.Workstream, Code = "WS1", Name = "Bursary and placement delivery", PlannedStart = project4.PlannedStart, PlannedEnd = project4.PlannedEnd, Status = WorkStatus.InProgress };
        var ms4a = new WbsElement { ProjectId = project4.Id, ParentId = ws4.Id, Type = WbsType.Milestone, Code = "WS1-M1", Name = "Workplace placement contract signed", PlannedStart = today.AddMonths(-8), PlannedEnd = today.AddMonths(-6), BaselineEnd = today.AddMonths(-6), ForecastEnd = today.AddMonths(-5).AddDays(-9), PercentComplete = 90, Status = WorkStatus.InProgress };
        _db.WbsElements.AddRange(ws4, ms4a);
        _db.Issues.AddRange(
            new Issue { Number = Num(NumberPrefixes.Issue, 2), ProjectId = project4.Id, Title = "Placement contract award delayed", Description = "The workplace placement procurement is still awaiting CFO award decision.", Severity = Severity.High, OwnerName = "Naledi Khumalo", Action = "Escalate to CFO for a decision this week.", DueDate = today.AddDays(-5), Status = IssueStatus.Escalated, EscalationLevel = 1, LastEscalatedAtUtc = _clock.UtcNow.AddDays(-4) },
            new Issue { Number = Num(NumberPrefixes.Issue, 3), ProjectId = project4.Id, Title = "Two graduates at risk of dropping out", Description = "Two bursary graduates have indicated they may withdraw due to relocation costs.", Severity = Severity.Medium, OwnerName = "Sipho Nkosi", Action = "Assess hardship fund eligibility.", DueDate = today.AddDays(10), Status = IssueStatus.InProgress });
        _db.ProjectDependencies.Add(new ProjectDependency { ProjectId = project4.Id, Description = "Depends on Procurement B award decision before placements can start.", Direction = DependencyDirection.Internal, DependsOn = "Procurement B (workplace placement services)", Impact = "Placement start date slips day-for-day with the award decision.", NeededBy = today.AddDays(7), OwnerName = "Naledi Khumalo", Status = DependencyStatus.AtRisk });
        var changeRequest4 = new ChangeRequest { Number = Num(NumberPrefixes.ChangeRequest, 1), ProjectId = project4.Id, Type = ChangeType.Cost, Title = "Additional welfare support budget", Description = "Increase budget to fund a hardship allowance for at-risk bursary graduates.", Justification = "Retention risk identified for 2 graduates; a small hardship allowance is expected to prevent attrition.", CostImpact = 1_500_000m, Status = ApprovalState.Pending };
        _db.ChangeRequests.Add(changeRequest4);
        _db.ProjectHealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = project4.Id, CalculatedAtUtc = _clock.UtcNow.AddDays(-1), Overall = "Amber", ScheduleScore = 2, CostScore = 1, RiskScore = 1, DeliveryScore = 1, ScheduleVarianceDays = 21, CostVariancePercent = 0, CriticalOpenRisks = 0, HighOpenRisks = 1, OverdueMilestones = 1, OverdueCriticalMilestones = 0, Explanation = "Placement milestone overdue pending procurement award; one high-severity issue open." });

        var ws5 = new WbsElement { ProjectId = project5.Id, Type = WbsType.Workstream, Code = "WS1", Name = "Regional office ICT refresh", PlannedStart = project5.PlannedStart, PlannedEnd = project5.PlannedEnd, Status = WorkStatus.InProgress };
        var ms5a = new WbsElement { ProjectId = project5.Id, ParentId = ws5.Id, Type = WbsType.Milestone, Code = "WS1-M1", Name = "Head office site upgraded", PlannedStart = today.AddMonths(-10), PlannedEnd = today.AddMonths(-7), BaselineEnd = today.AddMonths(-7), ActualStart = today.AddMonths(-10), ActualEnd = today.AddMonths(-6), PercentComplete = 100, Status = WorkStatus.Completed };
        var ms5b = new WbsElement { ProjectId = project5.Id, ParentId = ws5.Id, Type = WbsType.Milestone, Code = "WS1-M2", Name = "5 regional sites upgraded", PlannedStart = today.AddMonths(-7), PlannedEnd = today.AddMonths(-1), BaselineEnd = today.AddMonths(-1), ForecastEnd = today.AddMonths(2), PercentComplete = 35, Status = WorkStatus.InProgress };
        _db.WbsElements.AddRange(ws5, ms5a, ms5b);
        _db.Issues.AddRange(
            new Issue { Number = Num(NumberPrefixes.Issue, 4), ProjectId = project5.Id, Title = "Site access delays at 3 regional offices", Description = "Facilities approvals for after-hours site access have taken longer than planned at 3 sites.", Severity = Severity.High, OwnerName = "Metro ICT Programme Lead", Action = "Escalate to regional facilities managers.", DueDate = today.AddDays(-12), Status = IssueStatus.Escalated, EscalationLevel = 2, LastEscalatedAtUtc = _clock.UtcNow.AddDays(-2) },
            new Issue { Number = Num(NumberPrefixes.Issue, 5), ProjectId = project5.Id, Title = "Hardware lead times longer than planned", Description = "Network switch lead times have extended from 6 to 14 weeks.", Severity = Severity.Medium, OwnerName = "Metro ICT Programme Lead", Action = "Source an alternate hardware vendor for the remaining sites.", DueDate = today.AddDays(21), Status = IssueStatus.Open });
        _db.ProjectHealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = project5.Id, CalculatedAtUtc = _clock.UtcNow.AddDays(-1), Overall = "Red", ScheduleScore = 1, CostScore = 1, RiskScore = 0, DeliveryScore = 1, ScheduleVarianceDays = 60, CostVariancePercent = 9.7m, CriticalOpenRisks = 1, HighOpenRisks = 1, OverdueMilestones = 1, OverdueCriticalMilestones = 1, Explanation = "Regional rollout is 2 months behind baseline, cost forecast is 9.7% over budget, and a critical supplier risk is open." });
        _db.Comments.Add(new Comment { ParentType = ParentTypes.Project, ParentId = project5.Id, ProjectId = project5.Id, Text = "Escalated to the CEO at this week's steering committee; recovery plan due from the supplier by Friday.", AuthorName = "Thandi Mokoena (CEO)", CreatedAtUtc = _clock.UtcNow.AddDays(-2) });

        var ws6 = new WbsElement { ProjectId = project6.Id, Type = WbsType.Workstream, Code = "WS1", Name = "Artisan placement delivery - Nelspruit", PlannedStart = project6.PlannedStart, PlannedEnd = project6.PlannedEnd, Status = WorkStatus.InProgress };
        var ms6a = new WbsElement { ProjectId = project6.Id, ParentId = ws6.Id, Type = WbsType.Milestone, Code = "WS1-M1", Name = "All 90 learners trade-tested", PlannedStart = today.AddMonths(-9), PlannedEnd = today.AddDays(30), BaselineEnd = today.AddDays(30), ForecastEnd = today.AddDays(45), PercentComplete = 80, Status = WorkStatus.InProgress };
        _db.WbsElements.AddRange(ws6, ms6a);
        var changeRequest6 = new ChangeRequest { Number = Num(NumberPrefixes.ChangeRequest, 2), ProjectId = project6.Id, ContractId = contract2.Id, Type = ChangeType.Schedule, Title = "Extend delivery by 6 weeks", Description = "Extend the project end date to absorb the contractor's recovery plan for the site-visit backlog.", Justification = "Aligns the project schedule with the contract breach remedy timeline agreed with the supplier.", CostImpact = 0, ScheduleImpactDays = 42, ProposedEndDate = today.AddDays(60), Status = ApprovalState.Pending };
        _db.ChangeRequests.Add(changeRequest6);
        _db.ProjectHealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = project6.Id, CalculatedAtUtc = _clock.UtcNow.AddDays(-1), Overall = "Green", ScheduleScore = 2, CostScore = 2, RiskScore = 2, DeliveryScore = 2, ScheduleVarianceDays = 15, CostVariancePercent = 0, CriticalOpenRisks = 0, HighOpenRisks = 0, OverdueMilestones = 0, OverdueCriticalMilestones = 0, Explanation = "On track overall; a short schedule extension is pending approval to accommodate the contractor's recovery plan." });

        _db.ProjectClosures.Add(new ProjectClosure { ProjectId = project7.Id, LessonsLearned = "Earlier engagement with the sole accredited provider would have shortened the procurement lead time.", HandoverNotes = "Operational handover to Skills Planning complete; training records filed.", FinancialReconciliationConfirmed = true, DocumentationComplete = true, Status = ClosureStatus.Submitted });
        _db.BenefitReviews.Add(new BenefitReview { ProjectId = project8.Id, ScheduledDate = new DateOnly(2025, 11, 20), ExpectedBenefit = "Learners placed in permanent rail-sector employment", ExpectedValue = 60, ActualBenefit = "62 of 78 graduates placed in permanent rail-sector employment within 6 months of completion.", ActualValue = 62, Findings = "Exceeded the benefit target; strong employer relationships in the rail sector were the key success factor.", Status = BenefitReviewStatus.Completed, CompletedDate = new DateOnly(2025, 11, 18) });

        // ================================================================================
        // Risk, compliance and assurance: a spread of ratings across the heat map, one
        // escalated critical risk, overdue treatments, compliance attestations and audit
        // findings with corrective actions.
        // ================================================================================
        (int Score, string Rating) Rate(int likelihood, int impact) => RiskRating.Rate(likelihood, impact, RiskRating.DefaultBands);
        var risks = new List<Risk>();
        void AddRisk(string parentType, Guid parentId, Guid? projectId, string title, string cause, string ev, string consequence, string category, int likelihood, int impact, string owner, DateOnly reviewDate, RiskStatus status = RiskStatus.Open, DateTime? escalatedAtUtc = null)
        {
            var (score, rating) = Rate(likelihood, impact);
            risks.Add(new Risk
            {
                Number = Num(NumberPrefixes.Risk, risks.Count + 1), ParentType = parentType, ParentId = parentId, ProjectId = projectId, Title = title, Cause = cause, Event = ev, Consequence = consequence,
                Category = category, InherentLikelihood = likelihood, InherentImpact = impact, InherentScore = score, InherentRating = rating, ResidualLikelihood = Math.Max(1, likelihood - 1),
                ResidualImpact = impact, ResidualScore = Rate(Math.Max(1, likelihood - 1), impact).Score, ResidualRating = Rate(Math.Max(1, likelihood - 1), impact).Rating, OwnerName = owner,
                ReviewDate = reviewDate, Status = status, EscalatedAtUtc = escalatedAtUtc
            });
        }
        AddRisk(ParentTypes.Project, project3.Id, project3.Id, "Assessor capacity shortfall", "Limited pool of qualified RPL assessors regionally.", "Assessor unavailability delays cohort 2 assessment.", "Schedule slip on cohort 2 certification.", "Operational", 2, 3, "Sipho Nkosi", today.AddDays(60));
        AddRisk(ParentTypes.Project, project4.Id, project4.Id, "Graduate attrition", "Relocation and cost-of-living pressure on bursary graduates.", "Graduates withdraw before completing placement.", "Reduced benefit realisation; unspent bursary funds.", "Delivery", 3, 3, "Naledi Khumalo", today.AddDays(45), RiskStatus.Treating);
        AddRisk(ParentTypes.Project, project4.Id, project4.Id, "Placement procurement delay", "Adjudication decision outstanding.", "Workplace placements start later than planned.", "Compressed delivery timeline for remaining milestones.", "Procurement", 3, 3, "Naledi Khumalo", today.AddDays(14), RiskStatus.Treating);
        AddRisk(ParentTypes.Project, project5.Id, project5.Id, "Single supplier dependency", "Only one supplier was compliant and responsive for the ICT refresh.", "Supplier underperformance has no ready alternative.", "Programme-wide delay and cost overrun with no fallback delivery route.", "Strategic", 4, 5, "Metro ICT Programme Lead", today.AddDays(-5), RiskStatus.Open, _clock.UtcNow.AddDays(-5));
        AddRisk(ParentTypes.Project, project5.Id, project5.Id, "Hardware cost escalation", "Global semiconductor supply pressure on network hardware.", "Unit costs increase beyond the approved budget.", "Cost overrun on remaining regional sites.", "Financial", 3, 4, "Kagiso Molefe (Finance Officer)", today.AddDays(30), RiskStatus.Treating);
        AddRisk(ParentTypes.Contract, contract2.Id, project6.Id, "Contractor schedule recovery risk", "Site-visit backlog identified in the current breach notice.", "Recovery plan is not delivered within the agreed remedy period.", "Repeat breach and possible further schedule slip.", "Operational", 3, 3, "Naledi Khumalo", today.AddDays(20), RiskStatus.Treating);
        AddRisk(ParentTypes.Programme, programme1.Id, null, "Regional provider capacity constraint", "A small pool of accredited artisan-trade providers services all TETA regions.", "Concurrent projects compete for the same limited provider capacity.", "Programme-wide delivery risk across artisan development projects.", "Strategic", 2, 4, "Lindiwe Dube (Head PMO)", today.AddDays(75));
        AddRisk(ParentTypes.Project, project3.Id, project3.Id, "Learner attendance in outlying depots", "Long travel distances to assessment venues.", "Learners miss scheduled assessment sessions.", "Delayed certification for affected learners.", "Delivery", 2, 2, "Sipho Nkosi", today.AddDays(90));
        AddRisk(ParentTypes.Project, project2.Id, project2.Id, "Provider onboarding delay", "Learner recruitment procurement was re-advertised.", "Provider onboarding for logistics learners slips.", "Delayed learner intake for the 2026/27 cohort.", "Procurement", 2, 3, "Sipho Nkosi", today.AddDays(40));
        AddRisk(ParentTypes.Project, project6.Id, project6.Id, "POPIA compliance on beneficiary records", "Regional field staff capture beneficiary identity data on mobile devices.", "Personal information is inadequately protected in the field.", "Regulatory exposure under POPIA.", "Compliance", 2, 3, "Fatima Adams (Risk & Compliance)", today.AddDays(50));
        _db.Risks.AddRange(risks);
        var criticalRisk = risks.First(r => r.Title == "Single supplier dependency");
        var treatingRisk = risks.First(r => r.Title == "Contractor schedule recovery risk");
        _db.RiskTreatments.AddRange(
            new RiskTreatment { RiskId = criticalRisk.Id, ProjectId = project5.Id, Description = "Identify and qualify a second ICT infrastructure supplier for the remaining regional sites.", OwnerName = "Metro ICT Programme Lead", DueDate = today.AddDays(-3), Status = ActionStatus.InProgress },
            new RiskTreatment { RiskId = treatingRisk.Id, ProjectId = project6.Id, Description = "Obtain and approve the supplier's site-visit recovery plan.", OwnerName = "Naledi Khumalo", DueDate = today.AddDays(20), Status = ActionStatus.Open });
        var riskControl1 = new RiskControl { RiskId = criticalRisk.Id, Name = "Multi-supplier procurement policy for infrastructure contracts above R5m", OwnerName = "Ayesha Patel (Head SCM)", Effectiveness = "PartiallyEffective", LastAssessedOn = today.AddDays(-30) };
        _db.RiskControls.Add(riskControl1);
        _db.ControlAssessments.Add(new ControlAssessment { ControlId = riskControl1.Id, AssessedOn = today.AddDays(-30), Effectiveness = "PartiallyEffective", Comment = "Policy exists but was not applied before this procurement was initiated; being reinforced for future rounds.", AssessedBy = "Fatima Adams (Risk & Compliance)" });

        var obligation1 = new ComplianceObligation { Code = "COMP-001", Title = "Quarterly SCM compliance return", Source = "National Treasury SCM Instruction", Frequency = "Quarterly", OwnerName = "Ayesha Patel (Head SCM)" };
        var obligation2 = new ComplianceObligation { Code = "COMP-002", Title = "Annual POPIA data protection review", Source = "POPIA", Frequency = "Annually", OwnerName = "Fatima Adams (Risk & Compliance)" };
        var obligation3 = new ComplianceObligation { Code = "COMP-003", Title = "B-BBEE supplier verification refresh", Source = "TETA SCM policy", Frequency = "Annually", OwnerName = "Ayesha Patel (Head SCM)" };
        _db.ComplianceObligations.AddRange(obligation1, obligation2, obligation3);
        _db.ComplianceAttestations.AddRange(
            new ComplianceAttestation { ObligationId = obligation1.Id, Period = "2026 Q2", Result = AttestationResult.Compliant, AttestedAtUtc = _clock.UtcNow.AddDays(-40), AttestedBy = "Ayesha Patel (Head SCM)" },
            new ComplianceAttestation { ObligationId = obligation2.Id, Period = "2026/27", Result = AttestationResult.PartiallyCompliant, Comment = "Field data-capture devices for M&E have not yet completed the annual POPIA control review.", AttestedAtUtc = _clock.UtcNow.AddDays(-10), AttestedBy = "Fatima Adams (Risk & Compliance)" },
            new ComplianceAttestation { ObligationId = obligation3.Id, Period = "2026/27", Result = AttestationResult.NonCompliant, Comment = "One active supplier's B-BBEE certificate expired and has not yet been refreshed.", AttestedAtUtc = _clock.UtcNow.AddDays(-5), AttestedBy = "Ayesha Patel (Head SCM)" });

        var auditFinding1 = new AuditFinding { Number = Num(NumberPrefixes.AuditFinding, 1), ProjectId = project5.Id, Process = "ICT infrastructure procurement", Source = AuditSource.InternalAudit, AuditReference = "IA-2026-011", Title = "Single-bid procurement without documented market-sounding", Description = "The ICT infrastructure tender received only one compliant bid; no evidence of prior market sounding was found on file.", Recommendation = "Conduct and document market sounding before advertising infrastructure tenders above R5m.", Rating = Severity.High, ActionOwnerName = "Ayesha Patel (Head SCM)", DueDate = today.AddDays(-10), Status = FindingStatus.Open };
        var auditFinding2 = new AuditFinding { Number = Num(NumberPrefixes.AuditFinding, 2), ProjectId = project8.Id, Process = "Learnership contract close-out", Source = AuditSource.ExternalAudit, AuditReference = "EXT-2025-004", Title = "Close-out documentation filed 2 weeks after policy deadline", Description = "Close-out documentation for the rail operations contract was filed 2 weeks after the policy deadline, with no impact on learner outcomes.", Recommendation = "Reinforce close-out documentation deadlines with contract managers.", Rating = Severity.Low, ActionOwnerName = "Naledi Khumalo", DueDate = new DateOnly(2025, 7, 1), Status = FindingStatus.Closed, ClosedAtUtc = new DateTime(2025, 6, 28) };
        _db.AuditFindings.AddRange(auditFinding1, auditFinding2);
        _db.CorrectiveActions.Add(new CorrectiveAction { Number = Num(NumberPrefixes.CorrectiveAction, 1), ParentType = ParentTypes.AuditFinding, ParentId = auditFinding1.Id, ProjectId = project5.Id, Description = "Document a market-sounding procedure and apply it retrospectively to the current infrastructure programme.", OwnerName = "Ayesha Patel (Head SCM)", DueDate = today.AddDays(-10), Status = ActionStatus.Open, EscalationLevel = 1, LastEscalatedAtUtc = _clock.UtcNow.AddDays(-3) });

        var period = "2026/27";
        _db.AssuranceCoverage.AddRange(
            new AssuranceCoverage { Area = "Procurement", Line = AssuranceLine.Management, Period = period, Covered = true, Provider = "Head SCM", Rating = "Adequate" },
            new AssuranceCoverage { Area = "Procurement", Line = AssuranceLine.RiskAndCompliance, Period = period, Covered = true, Provider = "Risk & Compliance", Rating = "Adequate" },
            new AssuranceCoverage { Area = "Procurement", Line = AssuranceLine.InternalAudit, Period = period, Covered = true, Provider = "Internal Audit", Rating = "Needs improvement", RiskId = criticalRisk.Id },
            new AssuranceCoverage { Area = "Contract management", Line = AssuranceLine.Management, Period = period, Covered = true, Provider = "Head PMO", Rating = "Adequate" },
            new AssuranceCoverage { Area = "Contract management", Line = AssuranceLine.RiskAndCompliance, Period = period, Covered = true, Provider = "Risk & Compliance", Rating = "Adequate" },
            new AssuranceCoverage { Area = "Contract management", Line = AssuranceLine.InternalAudit, Period = period, Covered = false, Comments = "Not yet included in this year's internal audit plan." },
            new AssuranceCoverage { Area = "Contract management", Line = AssuranceLine.ExternalAssurance, Period = period, Covered = false, Comments = "No external assurance scheduled for this area this year." },
            new AssuranceCoverage { Area = "Finance and ERP interface", Line = AssuranceLine.Management, Period = period, Covered = true, Provider = "Finance", Rating = "Adequate" },
            new AssuranceCoverage { Area = "Finance and ERP interface", Line = AssuranceLine.InternalAudit, Period = period, Covered = true, Provider = "Internal Audit", Rating = "Adequate" },
            new AssuranceCoverage { Area = "Finance and ERP interface", Line = AssuranceLine.ExternalAssurance, Period = period, Covered = true, Provider = "External auditor (AGSA)", Rating = "Adequate" });

        // ================================================================================
        // Monitoring, evaluation and beneficiaries: an M&E plan, a published template, site
        // visits with findings and overdue corrective actions, and a beneficiary register
        // spanning the full lifecycle with one flagged potential duplicate.
        // ================================================================================
        _db.MePlans.AddRange(
            new MePlan { ProjectId = project3.Id, Description = "Quarterly desktop review with a site visit each quarter.", Frequency = "Quarterly", Methods = "Desktop review, site visits, learner interviews", Indicators = "Cohort completion rate; assessor throughput", ResponsibleOfficerName = "Bongani Zulu (M&E Officer)", NextVisitDue = today.AddDays(25) },
            new MePlan { ProjectId = project5.Id, Description = "Monthly site visits given elevated delivery risk.", Frequency = "Monthly", Methods = "Site visits, supplier progress review", Indicators = "Sites upgraded; downtime incidents", ResponsibleOfficerName = "Bongani Zulu (M&E Officer)", NextVisitDue = today.AddDays(10) });
        var template = new MonitoringTemplate { Code = "SITE-STD", Name = "Standard site monitoring form", ProjectType = "Learnership", TemplateVersion = 1, Status = TemplateStatus.Published, FieldsJson = "[{\"key\":\"attendance\",\"label\":\"Attendance confirmed\",\"type\":\"bool\",\"required\":true},{\"key\":\"findings\",\"label\":\"Findings\",\"type\":\"text\",\"required\":false}]", PublishedAtUtc = _clock.UtcNow.AddMonths(-10) };
        _db.MonitoringTemplates.Add(template);

        var visit1 = new MonitoringVisit { Number = Num(NumberPrefixes.Visit, 1), ProjectId = project3.Id, TemplateId = template.Id, TemplateVersion = 1, TemplateFieldsSnapshotJson = template.FieldsJson, Type = VisitType.Site, ScheduledDate = today.AddMonths(-3), VisitDate = today.AddMonths(-3), Officials = "Bongani Zulu; Site coordinator", Location = "Johannesburg assessment centre", Latitude = -26.2041m, Longitude = 28.0473m, Summary = "Assessment sessions running to schedule; attendance strong.", Outcome = "Satisfactory", Status = VisitStatus.Completed, ProviderSupplierId = supplier3.Id };
        var visit2 = new MonitoringVisit { Number = Num(NumberPrefixes.Visit, 2), ProjectId = project3.Id, TemplateId = template.Id, TemplateVersion = 1, TemplateFieldsSnapshotJson = template.FieldsJson, Type = VisitType.Site, ScheduledDate = today.AddDays(-20), VisitDate = today.AddDays(-20), Officials = "Bongani Zulu", Location = "Polokwane assessment centre", Summary = "Minor venue access issue resolved on the day.", Outcome = "Satisfactory", Status = VisitStatus.Completed, ProviderSupplierId = supplier3.Id };
        var visit3 = new MonitoringVisit { Number = Num(NumberPrefixes.Visit, 3), ProjectId = project5.Id, TemplateId = template.Id, TemplateVersion = 1, TemplateFieldsSnapshotJson = template.FieldsJson, Type = VisitType.Site, ScheduledDate = today.AddDays(-25), VisitDate = today.AddDays(-25), Officials = "Bongani Zulu; Metro ICT Programme Lead", Location = "Mbombela regional office", Summary = "Site visit confirmed the reported schedule delay and identified a further access constraint.", Outcome = "Findings raised", Status = VisitStatus.Completed, ProviderSupplierId = supplier4.Id };
        var visit4 = new MonitoringVisit { Number = Num(NumberPrefixes.Visit, 4), ProjectId = project6.Id, TemplateId = template.Id, TemplateVersion = 1, Type = VisitType.Site, ScheduledDate = today.AddDays(15), Officials = "Bongani Zulu", Location = "Nelspruit assessment centre", Status = VisitStatus.Scheduled };
        _db.MonitoringVisits.AddRange(visit1, visit2, visit3, visit4);

        var finding1 = new Finding { Number = Num(NumberPrefixes.Finding, 1), ProjectId = project5.Id, VisitId = visit3.Id, Source = "Monitoring", Description = "Generator backup power at the Mbombela regional office is non-functional, adding further risk to the upgrade timeline.", Severity = Severity.High, RootCause = "Deferred facilities maintenance predating this project.", Status = FindingStatus.Open };
        var finding2 = new Finding { Number = Num(NumberPrefixes.Finding, 2), ProjectId = project3.Id, VisitId = visit1.Id, Source = "Monitoring", Description = "Attendance register for one session was completed manually instead of on the digital system.", Severity = Severity.Low, RootCause = "Temporary network outage on the day.", Status = FindingStatus.Closed, ClosedAtUtc = _clock.UtcNow.AddMonths(-2) };
        _db.Findings.AddRange(finding1, finding2);
        _db.CorrectiveActions.AddRange(
            new CorrectiveAction { Number = Num(NumberPrefixes.CorrectiveAction, 2), ParentType = ParentTypes.Finding, ParentId = finding1.Id, ProjectId = project5.Id, Description = "Facilities team to repair or replace backup power at the Mbombela regional office.", OwnerName = "Metro ICT Programme Lead", DueDate = today.AddDays(-8), Status = ActionStatus.Open, EscalationLevel = 1, LastEscalatedAtUtc = _clock.UtcNow.AddDays(-1) },
            new CorrectiveAction { Number = Num(NumberPrefixes.CorrectiveAction, 3), ParentType = ParentTypes.Finding, ParentId = finding2.Id, ProjectId = project3.Id, Description = "Re-capture the affected attendance register on the digital system.", OwnerName = "Sipho Nkosi", DueDate = DateOnly.FromDateTime(_clock.UtcNow.AddMonths(-2).AddDays(3)), Status = ActionStatus.Completed, CompletedAtUtc = _clock.UtcNow.AddMonths(-2).AddDays(2) });

        string[] firstNames = { "Thabo", "Nomvula", "Sipho", "Palesa", "Andile", "Refilwe", "Kagiso", "Zinhle", "Bongani", "Lerato", "Mpho", "Katlego", "Nokuthula", "Tumelo", "Ayanda", "Boitumelo", "Sthembiso", "Nonhlanhla", "Thulani", "Precious" };
        string[] lastNames = { "Mokoena", "Dlamini", "Khumalo", "Nkosi", "Molefe", "Zulu", "Mahlangu", "Sithole", "Radebe", "Mabaso", "Ndlovu", "Tshabalala", "Mofokeng", "Baloyi", "Maake", "Chauke", "Mnguni", "Sibiya", "Mokgadi", "Letsoalo" };
        var beneficiaryProjects = new[] { project3, project4, project6, project8 };
        var beneficiaryInterventions = new[] { "RPL assessment", "Bursary and workplace placement", "Artisan trade testing", "Learnership" };
        var beneficiaries = new List<Beneficiary>();
        for (var i = 0; i < 20; i++)
        {
            var project = beneficiaryProjects[i % beneficiaryProjects.Length];
            var occurrence = i / beneficiaryProjects.Length; // 0-4: which beneficiary this is within its project
            var said = $"{8001 + i:D4}01{5000 + i:D4}08";
            var status = i switch { < 3 => BeneficiaryStatus.Registered, < 8 => BeneficiaryStatus.Enrolled, < 13 => BeneficiaryStatus.Participating, < 17 => BeneficiaryStatus.Completed, < 19 => BeneficiaryStatus.Placed, _ => BeneficiaryStatus.DroppedOut };
            var beneficiary = new Beneficiary
            {
                Number = Num(NumberPrefixes.Beneficiary, i + 1), ProjectId = project.Id, IdentifierEncrypted = _fieldProtector.Protect(said), IdentifierHash = _fieldProtector.KeyedHash(said),
                IdentifierMasked = Masking.Mask(said, 4), IdentifierType = "SAID", FirstName = firstNames[i % firstNames.Length], LastName = lastNames[(i * 3) % lastNames.Length],
                Gender = i % 2 == 0 ? "Female" : "Male", BirthYear = 1998 + i % 8, Province = new[] { "Gauteng", "Limpopo", "Mpumalanga", "KwaZulu-Natal" }[i % 4],
                Intervention = beneficiaryInterventions[Array.IndexOf(beneficiaryProjects, project)], ProviderSupplierId = project == project3 ? supplier3.Id : project == project6 ? supplier2.Id : null,
                FundingSource = "Discretionary grant", Status = status, ConsentObtained = true, Cohort = occurrence < 3 ? "Cohort 1" : "Cohort 2"
            };
            beneficiaries.Add(beneficiary);
        }
        // One deliberately flagged as a potential duplicate across two projects, to demo FR-ME-008.
        beneficiaries[5].PotentialDuplicate = true;
        beneficiaries[5].DuplicateNote = $"Possible match on name/identifier with beneficiary {beneficiaries[11].Number} registered under a different project.";
        _db.Beneficiaries.AddRange(beneficiaries);
        foreach (var b in beneficiaries.Where(b => b.Status is BeneficiaryStatus.Completed or BeneficiaryStatus.Placed or BeneficiaryStatus.DroppedOut))
        {
            _db.BeneficiaryStatusHistory.Add(new BeneficiaryStatusHistory { BeneficiaryId = b.Id, FromStatus = BeneficiaryStatus.Participating, ToStatus = b.Status, ChangedAtUtc = _clock.UtcNow.AddDays(-30), ChangedBy = "Bongani Zulu (M&E Officer)", Note = b.Status == BeneficiaryStatus.DroppedOut ? "Withdrew due to relocation." : null });
        }

        // TETA-approved learner delivery targets for the Learner Delivery & Monitoring Report (FR-REP learner
        // delivery) - drives the Executive Summary KPIs for the four projects that carry a beneficiary register.
        _db.LearnerDeliveryTargets.AddRange(
            new LearnerDeliveryTarget { ProjectId = project3.Id, ContractedLearners = 200, LearnersDueForCompletion = 100, MonitoringVisitsPlanned = 8, WithdrawalTolerancePercent = 10 },
            new LearnerDeliveryTarget { ProjectId = project4.Id, ContractedLearners = 50, LearnersDueForCompletion = 25, MonitoringVisitsPlanned = 4, WithdrawalTolerancePercent = 10 },
            new LearnerDeliveryTarget { ProjectId = project6.Id, ContractedLearners = 80, LearnersDueForCompletion = 40, MonitoringVisitsPlanned = 6, WithdrawalTolerancePercent = 15 },
            new LearnerDeliveryTarget { ProjectId = project8.Id, ContractedLearners = 120, LearnersDueForCompletion = 60, MonitoringVisitsPlanned = 8, WithdrawalTolerancePercent = 10 });

        // ================================================================================
        // Workflow inbox and notifications: a handful of in-flight approvals for demo users
        // to act on, and a few notifications so inboxes aren't empty on first login.
        // ================================================================================
        var definitions = await _db.WorkflowDefinitions.Include(d => d.Steps).ToDictionaryAsync(d => d.Code, ct);
        void AddPendingApproval(string workflowCode, string entityType, Guid entityId, string title, decimal? value, Guid? projectId, int stepOrder, string assignedRole, DateTime startedAtUtc)
        {
            if (!definitions.TryGetValue(workflowCode, out var def)) return;
            var step = def.Steps.FirstOrDefault(s => s.StepOrder == stepOrder);
            if (step is null) return;
            var instance = new WorkflowInstance
            {
                DefinitionId = def.Id, DefinitionCode = def.Code, DefinitionVersion = def.DefinitionVersion, EntityType = entityType, EntityId = entityId, EntityReference = title,
                TransactionValue = value, ProjectId = projectId, CurrentStepOrder = stepOrder, State = WorkflowState.InProgress, StartedAtUtc = startedAtUtc, StartedBy = "seed"
            };
            _db.WorkflowInstances.Add(instance);
            _db.WorkflowTasks.Add(new WorkflowTask
            {
                InstanceId = instance.Id, StepOrder = step.StepOrder, StepCode = step.Code, StepName = step.Name, AssignedRole = assignedRole, AuthorityType = step.AuthorityType,
                CreatedAtUtc = startedAtUtc, DueAtUtc = startedAtUtc.AddHours(step.SlaHours), Decision = TaskDecision.Pending
            });
        }
        AddPendingApproval("BUSINESS_CASE_APPROVAL", "BusinessCase", project1.Id, project1.Name, 9_800_000m, project1.Id, 10, Roles.HeadPmo, _clock.UtcNow.AddDays(-3));
        AddPendingApproval("PROCUREMENT_ADJUDICATION", "Procurement", procB.Id, procB.Title, procB.EstimatedValue, procB.ProjectId, 20, Roles.Cfo, _clock.UtcNow.AddDays(-2));
        AddPendingApproval("CHANGE_REQUEST", "ChangeRequest", changeRequest4.Id, changeRequest4.Title, changeRequest4.CostImpact, changeRequest4.ProjectId, 20, Roles.Cfo, _clock.UtcNow.AddDays(-1));
        AddPendingApproval("INVOICE_CERTIFICATION", "Invoice", invoice2.Id, invoice2.Number, invoice2.Amount, invoice2.ProjectId, 10, Roles.ProjectManager, _clock.UtcNow.AddDays(-1));

        if (existingUsers.TryGetValue("teta.pmo", out var pmoUser))
            _db.Notifications.Add(new Notification { UserId = pmoUser.Id, Title = "Business case awaiting your review", Message = $"{project1.Name} is awaiting PMO review.", Category = "Approval", CreatedAtUtc = _clock.UtcNow.AddDays(-3), Link = "/projects" });
        if (existingUsers.TryGetValue("teta.cfo", out var cfoUser))
        {
            _db.Notifications.Add(new Notification { UserId = cfoUser.Id, Title = "Award decision awaiting your approval", Message = $"{procB.Title} is awaiting your award decision.", Category = "Approval", CreatedAtUtc = _clock.UtcNow.AddDays(-2), Link = "/procurement" });
            _db.Notifications.Add(new Notification { UserId = cfoUser.Id, Title = "Change request awaiting your approval", Message = $"{changeRequest4.Title} is awaiting your approval.", Category = "Approval", CreatedAtUtc = _clock.UtcNow.AddDays(-1), Link = "/change-requests" });
        }
        if (contractsUser is not null)
            _db.Notifications.Add(new Notification { UserId = contractsUser.Id, Title = "Contract expiring within 90 days", Message = $"Contract {contract2.ContractNumber} expires on {contract2.CurrentEndDate:yyyy-MM-dd}.", Category = "Contract", CreatedAtUtc = _clock.UtcNow.AddDays(-1), Link = "/contracts" });
        if (existingUsers.TryGetValue("teta.finance", out var financeUser))
            _db.Notifications.Add(new Notification { UserId = financeUser.Id, Title = "Invoice failed validation", Message = $"Invoice {invoice3.Number} failed the three-way match and needs review.", Category = "Finance", CreatedAtUtc = _clock.UtcNow.AddDays(-14), Link = "/finance/invoices" });
        if (existingUsers.TryGetValue("teta.risk", out var riskUser))
            _db.Notifications.Add(new Notification { UserId = riskUser.Id, Title = "Critical risk escalated", Message = $"{criticalRisk.Title} on {project5.Name} has been escalated.", Category = "Risk", CreatedAtUtc = _clock.UtcNow.AddDays(-5), Link = "/assurance/risks" });

        // Continue live business-number sequences after the ones seeded here, so the running
        // application never collides with a demo-data number.
        var sequenceStarts = new (string Prefix, int LastUsed)[]
        {
            (NumberPrefixes.Project, 6), (NumberPrefixes.ProjectDraft, 9), (NumberPrefixes.Requisition, 6), (NumberPrefixes.Procurement, 7), (NumberPrefixes.Award, 2),
            (NumberPrefixes.Contract, 4), (NumberPrefixes.Variation, 1), (NumberPrefixes.Deliverable, 5), (NumberPrefixes.Invoice, 6), (NumberPrefixes.ChangeRequest, 2),
            (NumberPrefixes.Issue, 5), (NumberPrefixes.Risk, risks.Count), (NumberPrefixes.Visit, 4), (NumberPrefixes.Finding, 2), (NumberPrefixes.CorrectiveAction, 3),
            (NumberPrefixes.AuditFinding, 2), (NumberPrefixes.Beneficiary, beneficiaries.Count), (NumberPrefixes.ProcurementException, 1)
        };
        foreach (var (prefix, lastUsed) in sequenceStarts)
        {
            if (!await _db.NumberSequences.AnyAsync(s => s.Prefix == prefix && s.Year == year, ct))
                _db.NumberSequences.Add(new NumberSequence { Prefix = prefix, Year = year, NextValue = lastUsed + 1 });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("TETA demo data seeded: {Projects} projects, {Procurements} procurements, {Contracts} contracts, {Invoices} invoices, {Risks} risks, {Beneficiaries} beneficiaries",
            9, 7, 4, 6, risks.Count, beneficiaries.Count);
    }
}
