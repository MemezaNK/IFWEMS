using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Platform.Core;
using Platform.Security.Identity;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Engines;
using Teta.Ippcms.Application.Projects;
using Teta.Ippcms.Domain.Admin;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Budget;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Projects;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Security;
using Teta.Ippcms.Domain.Strategy;
using Teta.Ippcms.Domain.Suppliers;
using Teta.Ippcms.Domain.Workflow;
using Teta.Ippcms.Infrastructure.Persistence;

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
    private readonly ILogger<TetaSeeder> _logger;

    public TetaSeeder(TetaDbContext db, IConfiguration config, IPlatformPasswordHasher hasher, IClock clock, ILogger<TetaSeeder> logger)
    {
        _db = db;
        _config = config;
        _hasher = hasher;
        _clock = clock;
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

        var supplier = new Supplier
        {
            SupplierNumber = "SUP-DEMO-0001", LegalName = "Demo Training Provider (Pty) Ltd", NormalizedName = Supplier.Normalize("Demo Training Provider (Pty) Ltd"),
            RegistrationNumber = "2019/000001/07", CsdNumber = "MAAA0000001", BbbeeLevel = 1, Status = SupplierStatus.Active, CsdVerified = true,
            IsImplementingPartner = true, Email = "provider@teta.demo", Province = "Gauteng", TaxClearanceExpiry = _clock.Today.AddYears(1)
        };
        _db.Suppliers.Add(supplier);
        if (existingUsers.TryGetValue("teta.supplier", out var supplierUser))
            _db.SupplierUsers.Add(new SupplierUser { SupplierId = supplier.Id, UserId = supplierUser.Id, LinkedAtUtc = _clock.UtcNow });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("TETA demo data seeded");
    }
}
