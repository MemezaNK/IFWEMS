# TETA-IPPCMS Requirements Traceability Matrix

**Date:** 27 September 2026
**System:** TETA-IPPCMS (Integrated Project, Procurement and Contract Management System)
**Source:** *TETA-IPPCMS Technical Design and Developer Specification* v1.0, 25 September 2026, Section 20 ("Requirements Traceability Matrix — Initial Baseline")
**Scope:** Every requirement in that baseline (126 functional requirements across 11 modules, 15 security requirements, 16 non-functional requirements — 157 total) checked against the actual code in `src/Teta.Ippcms.*`, `src/Teta.Ippcms.Client` and `src/Shared/Platform.*`, and against the automated test suites in `tests/Teta.Ippcms.UnitTests` and `tests/Teta.Ippcms.IntegrationTests`.

## How to read this

Each row states a status based on reading the actual implementation, not on what the requirement's name suggests should exist:

- **Implemented** — the behaviour described exists in code and was traced to a specific class/method.
- **Partial** — some of the requirement exists, but a specific piece is missing or incomplete (stated explicitly).
- **Not Implemented** — no corresponding code was found.
- **Not Verifiable In Code** — the requirement describes an infrastructure/operations commitment (an SLA, a backup schedule, a penetration test) rather than application behaviour, so no amount of reading the repository proves or disproves it.

"Test coverage" names the specific test method where one exists, or says `none found` — a gap in automated regression coverage, not necessarily a gap in the underlying feature.

## Executive summary

| Status | Count | % of 157 |
|---|---|---|
| Implemented | 136 | 87% |
| Partial | 10 | 6% |
| Not Implemented | 4 | 3% |
| Not Verifiable In Code (infra/ops) | 7 | 4% |

**The four genuine gaps** (no corresponding code at all):

- **SEC-001 (SSO)** — authentication is local username/password + JWT; there is no OIDC/SAML integration with an external identity provider.
- **SEC-013 (Vulnerability management)** — CI runs build+test only; no SAST, dependency/vulnerability scanning, or penetration-testing evidence in the repo.
- **SEC-014 (Logging/SIEM)** — Serilog logs to console only; no forwarding to a SIEM/central security monitoring platform.
- **NFR-008 (Accessibility)** — no systematic ARIA labelling or accessibility tooling (axe-core, pa11y) across the ~107 Angular components; only 2 files use `aria-label`/`role` at all.

These four, plus the ten "Partial" items below, are exactly the kind of finding this kind of review is supposed to surface before go-live — none of them block day-to-day use of the system, but SEC-001/013/014 in particular should be raised with TETA's infrastructure/security team before a production decision, since they're normally satisfied at the platform level (an enterprise IdP, a SIEM already run by TETA's ICT function) rather than needing new application code.

**"Not Verifiable In Code" is not the same as "not done"** — five of the seven (NFR-001 availability, NFR-002 performance, NFR-003 scalability, NFR-006 RPO, NFR-007 RTO) plus SEC-015 (backup security) describe commitments that depend on the actual VPS, database, and hosting configuration once deployed, which this review cannot see from source code. NFR-009 (browser support) is nominally verifiable but the CI suite only exercises Chrome headless, so Edge compatibility is asserted, not tested.

---

## 5.1 Strategy and APP Management

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-STR-001 | Planning periods | Implemented | `Domain/Strategy/StrategyEntities.cs:StrategicPlan` (PeriodStart/PeriodEnd, VersionNumber, Status) + `StrategyService.CreatePlanAsync`/`RevisePlanAsync` | none found |
| FR-STR-002 | Strategic hierarchy | Implemented | `StrategicOutcome`/`StrategicObjective`/`AppIndicator`/`AppTarget` hierarchy; `StrategyService.SubmitPlanAsync` requires ≥1 indicator per objective | `Scenario.cs.IndicatorAsync()` builds the full chain, used by all strategy tests |
| FR-STR-003 | APP targets | Implemented | `AppTarget` (Quarter, UnitOfMeasure, ResponsibleExecutive); `SaveTargetAsync`/`EnsureEditable` blocks edits once Approved/Locked | none found |
| FR-STR-004 | Project alignment | Implemented | `ProjectIndicatorLink`; `ProjectService.SubmitBusinessCaseAsync` throws `DomainException(...,"FR-STR-004")` if no indicator link exists | `DeliveryAndAssuranceTests.App_results_require_verified_evidence_and_segregated_verification` |
| FR-STR-005 | Contribution rules | Implemented | `ProjectIndicatorLink.ContributionOf()` (Direct/Weighted/Count); `StrategyService.GetPerformanceAsync` aggregates with drill-down | same test asserts `VerifiedActual == 40m` and contributing projects list |
| FR-STR-006 | Performance evidence | Implemented | `AppIndicator.RequiredEvidenceTypeList()`; `VerifyResultAsync` throws `DomainException(...,"BR-009")` without required evidence | same test asserts HTTP 422 without evidence, success once verified |
| FR-STR-007 | Version control | Implemented | `StrategicPlan.EnsureEditable()` (only Draft editable); `StrategicPlanVersion` immutable snapshot on approval | none found |
| FR-STR-008 | Forecasting | Implemented | `AppTarget.ForecastValue`/`ForecastCommentary`; `UpdateForecastAsync`; client `performance.component.ts` shows forecast column | none found |
| FR-STR-009 | Strategic dashboard | Implemented | `GetPerformanceAsync` returns target/actual/pending/forecast/variance/achievement + contributing projects; client drill-down on row click | same test checks contributing-project drill-down data |
| FR-STR-010 | Approval workflow | Implemented | Routes through `IWorkflowService.StartAsync` (versioned steps); `DecideAsync` writes full audit + SoD (BR-008) | `ProjectLifecycleTests` exercises the same engine for business cases; no dedicated STRATEGIC_PLAN_APPROVAL test |

## 5.2 Portfolio, Programme and Project Initiation

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-PPM-001 | Project pipeline | Implemented | `Project.DraftReference` via `NextAsync(NumberPrefixes.ProjectDraft)`; `ProjectNumber` stays null until approval | `ProjectLifecycleTests.Business_case_approval_assigns_project_number_and_budget` |
| FR-PPM-002 | Business case | Implemented | `BusinessCase.MissingMandatoryFields()`; `SubmitBusinessCaseAsync` throws `ValidationException` per missing field | same test: HTTP 400 for incomplete case, success once complete |
| FR-PPM-003 | Prioritisation | Implemented | `PrioritisationCriterion` (Category/Weight/MaxScore) + `ProjectPriorityScore`; `ScoreAsync` requires rationale, computes weighted 0-100 score | none found |
| FR-PPM-004 | Approval | Implemented | `SubmitBusinessCaseAsync` starts `BUSINESS_CASE_APPROVAL` workflow; `BusinessCaseApprovalHandler` only activates project ID on Approved | `ProjectLifecycleTests.Business_case_approval_assigns_project_number_and_budget`, `Rejection_requires_a_reason_and_is_recorded` |
| FR-PPM-005 | Project ID | Implemented | `Project.ProjectNumber` has private set; `ActivateWithProjectId()` throws if already set; format `PRJ-\d{4}-\d{4}` | same test: `Assert.Matches(@"^PRJ-\d{4}-\d{4}$", ...)` |
| FR-PPM-006 | Portfolio hierarchy | Implemented | Portfolio→Programme→Project→Workstream; `GetHierarchyAsync` builds nested tree; client `hierarchy.component.ts` | none found |
| FR-PPM-007 | Project charter | Implemented | `ProjectCharter` (Purpose/Scope/GovernanceStructure); `SaveCharterAsync` requires `HasProjectId`; `ApproveCharterAsync` enforces SoD | none found |
| FR-PPM-008 | Stakeholders | Implemented | `ProjectStakeholder` with `EffectiveFrom`/`EffectiveTo`/`IsEffectiveOn()`; syncs Sponsor/Manager on save | none found |
| FR-PPM-009 | Stage gates | Implemented | `StageGateCriterion`/`Review`/`Check`; `DecideGateAsync` throws `DomainException(...,"FR-PPM-009")` on unmet criteria; `GateChecks.cs` implements 16 automatic checks | none found directly |
| FR-PPM-010 | Project status | Implemented | `ProjectStatusHistory` (append-only); `ChangeStatusAsync` enforces an explicit transition table | none found directly (status asserted indirectly via workflow outcomes) |

## 5.3 Budget and Procurement Planning

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-BUD-001 | Project budget | Implemented | `ProjectBudgetLine`; `BaselineAsync` blocks unless totals reconcile to `ApprovedBudget` (±0.01) | `ProcureToPayTests.Requisition_to_payment_end_to_end`: `summary.Reconciled` |
| FR-BUD-002 | Budget versions | Implemented | `ProjectBudgetLine.SetOriginal()` throws once baselined; `BudgetRevision` records Previous/New/Reason | no test for the revise path |
| FR-BUD-003 | Demand plan | Implemented | `GenerateDemandPlanAsync` selects lines of approved projects, excludes personnel categories | none found |
| FR-BUD-004 | Procurement plan | Implemented | `ProcurementPlanItem` (planned dates, Status); `ListPlanAsync`/`SavePlanItemAsync`; client "Demand plan" tab | none found |
| FR-BUD-005 | Budget availability | Implemented | `CheckAvailabilityAsync`; `SubmitRequisitionAsync` throws `DomainException(...,"FR-BUD-005")` unless an approved budget exception exists | none found (exception path untested) |
| FR-BUD-006 | Commitment forecast | Implemented | `CommitmentForecastAsync` builds a 12-month forecast from payment schedules + planned procurement | none found |
| FR-BUD-007 | Procurement method | Implemented | `ProcurementMethodRule.AppliesTo()` selects by value band + effective dates; requires justification if overridden | `ProcureToPayTests`: `procurement.Summary.Method == "RFQ"` at R500k |
| FR-BUD-008 | Threshold rules | Implemented | `ProcurementMethodRule` effective-dated/versioned; `SubmitRuleAsync` requires a different approver than submitter | none found |
| FR-BUD-009 | Plan variance | Implemented | `ProcurementPlanItem` Planned/Actual date pairs + `VarianceReason`; DTO computes advert/award variance | none found |
| FR-BUD-010 | Carry-over | Implemented | `ProjectBudgetLine.IsCarryOver`/`CarryOverFromYear`; validated on save; distinguishable per financial year | none found |

**Module note:** every FR-BUD requirement is implemented in `BudgetService.cs` with the correct FR-tag in its guard clauses, but only two of the ten (FR-BUD-001, FR-BUD-007) have a dedicated integration test. The other eight are exercised only incidentally, if at all.

## 5.4 Procurement and Bid Management

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-SCM-001 | Requisition | Implemented | `Requisition.MissingMandatoryFields`; `SubmitRequisitionAsync` blocks incomplete submissions | `ProcureToPayTests` |
| FR-SCM-002 | Specification/TOR | Implemented | `Specification.EnsureEditable`; locked once Approved/Superseded; self-approval blocked via SoD | `ProcureToPayTests` (self-approval rejected 422) |
| FR-SCM-003 | Committee setup | Implemented | `Committee`/`CommitteeMember`/`CommitteeMeeting`; document access gated to declared members | `ProcureToPayTests` (BR-003 access asserted) |
| FR-SCM-004 | Declarations | Implemented | `Declaration.GrantsAccess`; `EnsureBidAccessAsync` throws `ForbiddenException` until declared | `ProcureToPayTests` (403 before, 200 after declaring) |
| FR-SCM-005 | Publication | Implemented | `Publication` (channel/reference/closing date/evidence doc); enforces `MinimumAdvertDays` | `ProcureToPayTests` (short advert period rejected 422) |
| FR-SCM-006 | Bid receipt | Implemented | `ReceivedAtUtc` set server-side; `IsLate` computed server-side; opening blocked before closing date | `ProcureToPayTests` |
| FR-SCM-007 | Compliance evaluation | Implemented | `EvaluationCriterion.IsMandatory` + `EvaluationScore` (Passed/Comment/EvidenceReference); `EvaluationEngine.IsCompliant()` | `EngineTests.EvaluationEngineTests`, `ProcureToPayTests` |
| FR-SCM-008 | Technical evaluation | Implemented | Configurable weight/max score/threshold; per-evaluator scores retained alongside consolidated score | `EngineTests`, `ProcureToPayTests` |
| FR-SCM-009 | Price/preference | Implemented | `PricePreferenceSystem.IsEffectiveOn`; formula reproducible via `EvaluationEngine.CalculatePricePreference` | `EngineTests` (74.29 points asserted), `ProcureToPayTests` |
| FR-SCM-010 | Due diligence | Implemented | `DueDiligenceCheck.AllChecksPassed` (6 checks); adjudication blocked without a Passed result | `ProcureToPayTests` |
| FR-SCM-011 | Adjudication | Implemented | `Adjudication` routed through workflow (BAC_RECOMMEND/CFO_AWARD); decision timestamped on completion | `ProcureToPayTests` |
| FR-SCM-012 | Award | Implemented | `Award.CanCreateContract`; notifies Contract Manager role on completion | `ProcureToPayTests` |
| FR-SCM-013 | Unsuccessful bidders | Implemented | `BidderCommunication` (RegretLetter/Debriefing/Objection/ObjectionOutcome); auto regret letters on award | `ProcureToPayTests` (unsuccessful bid outcome asserted); no debrief/objection test |
| FR-SCM-014 | Cancellation/re-advertisement | Implemented | `CancelAsync` requires reason, keeps record; `ReAdvertiseAsync` copies spec into a linked new record | none found |
| FR-SCM-015 | Deviation/exception | Implemented | `ProcurementException` requires Motivation+Authority, routed through workflow; used to bypass budget/method rules without bypassing audit | none found |
| FR-SCM-016 | Publication reporting | Implemented | `TransparencyAsync` exposes tender/bidder/award fields; client "transparency register" export | none found |
| FR-SCM-017 | Procurement dashboard | Implemented | `DashboardAsync` returns status/age/method buckets, delays, exceptions, late bids; filterable | none found |
| FR-SCM-018 | Supplier linkage | Implemented | Hard match on registration/CSD/tax number blocks save (409); fuzzy name match (Levenshtein ≥0.88) requires override | `EngineTests.DuplicateDetectionTests`, `ProcureToPayTests` (409 on duplicate) |

## 5.5 Contract Management

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-CON-001 | Contract creation | Implemented | `CreateFromAwardAsync` copies award data (BR-004 gate); `CreateNonBidAsync` requires an approved exception | `ProcureToPayTests` |
| FR-CON-002 | Contract register | Implemented | Unique `ContractNumber`; `ListAsync` supports search/filters, `IncludeHistorical` toggle | exercised transitively via RPT-005 |
| FR-CON-003 | Obligations | Implemented | `ContractObligation` (Deliverable/Milestone/Kpi/Sla/Reporting/Compliance) with owner+due date; flags overdue | none found |
| FR-CON-004 | Payment schedule | Implemented | Payment milestones blocked against `RevisedValue`; certification requires `AcceptanceStatus == Accepted` | `ProcureToPayTests` |
| FR-CON-005 | Contract documents | Implemented | `SignAsync` requires an uploaded document, sets signature status/date, creates a commitment | `ProcureToPayTests` |
| FR-CON-006 | Expiry alerts | Implemented | `ScheduledJobs.ContractExpiryAlertsAsync`: configurable lead-days list, idempotent notification | `PlatformAndReportingTests` (idempotency asserted) |
| FR-CON-007 | Variations | Implemented | `ContractVariation` stores before/after value+date; routed through workflow; applies only on approval | none found |
| FR-CON-008 | Extensions | Implemented | Extension variations require revised end date/days; `OriginalEndDate` never mutated | none found |
| FR-CON-009 | Ceiling control | Implemented | `Contract.CanCommit`; blocked at both commitment creation and invoice certification | happy path only in `ProcureToPayTests`; breach path untested |
| FR-CON-010 | Performance reviews | Implemented | `ContractPerformanceReview.Calculate`; one review per period (conflict on duplicate); running average rating | none found |
| FR-CON-011 | Breach/remedy | Implemented | `ContractBreach.IsOpen`; auto Open→NoticeIssued transition; surfaced on dashboard | none found |
| FR-CON-012 | Contract risk | Implemented | `Risk.ParentType == "Contract"`; Critical residual rating triggers immediate escalation | none found |
| FR-CON-013 | Close-out | Implemented | 7 mandatory close-out checks; blocked unless all pass or an approved exception is supplied | none found |
| FR-CON-014 | Supplier history | Implemented | `HistoryAsync` aggregates contracts/reviews/bids per supplier, gated by permission | none found |
| FR-CON-015 | Contract dashboard | Implemented | `DashboardAsync` returns active value/expiring/variations/breaches/performance with drill-down IDs | none found |

## 5.6 Project Planning and Execution

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-EXE-001 | WBS | Implemented | `WbsElement` type hierarchy (Workstream→Deliverable→Milestone→Activity→Task); weighted roll-up | `DeliveryAndAssuranceTests`, `EngineTests.Roll_up_is_weighted` |
| FR-EXE-002 | Baseline schedule | Implemented | Baseline 0 is immutable original; later baselines added only on approved change requests | `DeliveryAndAssuranceTests` |
| FR-EXE-003 | Dependencies | Implemented | `WbsDependency` (FS/SS/FF/SF); `ScheduleValidator.FindCycle` blocks loops | `EngineTests.Detects_dependency_cycles`, `DeliveryAndAssuranceTests` |
| FR-EXE-004 | Gantt | **Partial** | `project-schedule.component.ts` renders CSS-positioned bars with the WBS data table's own filter/export, but there is no interactive drag/zoom/resize and no Gantt-specific filter control separate from the table | none found |
| FR-EXE-005 | Progress | Implemented | Every progress update inserts an immutable `ProgressUpdate` row, preserving full history | `DeliveryAndAssuranceTests` |
| FR-EXE-006 | Milestones | Implemented | Completing an element with `EvidenceRequired=true` blocked without verified evidence | none found directly (same pattern tested for corrective actions) |
| FR-EXE-007 | Resource assignment | Implemented | `WorkloadAsync` sums allocation per resource, flags `OverAllocated` >100%; client `workload.component.ts` | none found |
| FR-EXE-008 | Issues | **Partial** | `Issue` register with severity/owner/due date; `EscalateOverdueItemsAsync` escalates overdue issues, but escalation applies uniformly, not specifically gated on "critical" severity as the requirement implies | `PlatformAndReportingTests.Background_jobs_escalate_overdue_items...` (uses High severity) |
| FR-EXE-009 | Dependencies register | Implemented | `ProjectDependency` (Direction/Impact/Status) visible in project-controls tab; not surfaced on the portfolio dashboard | none found |
| FR-EXE-010 | Change control | Implemented | `ChangeRequest` (Scope/Cost/Schedule/Benefit); approval creates a controlled baseline/budget revision per type | `DeliveryAndAssuranceTests` |
| FR-EXE-011 | Project health | Implemented | `HealthCalculator` takes worst of 4 configurable dimensions; snapshot persists each dimension's score | `EngineTests.HealthCalculatorTests`, `DeliveryAndAssuranceTests` |
| FR-EXE-012 | Status report | Implemented | `StatusReportAsync` pulls live status/EAC/milestones/issues/risks/changes from controlled tables | `DeliveryAndAssuranceTests` |
| FR-EXE-013 | Collaboration | Implemented | `Comment` attaches to any parent type with immutable author/timestamp | none found |
| FR-EXE-014 | Project closure | Implemented | `SubmitClosureAsync` enforces reconciliation+documentation, blocks on open items unless an approved exception is cited | related evidence-gating pattern tested elsewhere; no direct closure test |
| FR-EXE-015 | Benefits review | Implemented | `BenefitReview.RealisationPercent`; auto-scheduled 6 months after closure; requires actual benefit + findings to complete | none found |

## 5.7 Financial Control and Payment Interface

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-FIN-001 | Financial view | Implemented | `FinancialViewDto` sums budget/committed/actual/accrued/invoiced/paid/EAC directly from source tables | none found directly |
| FR-FIN-002 | ERP integration | Implemented | `ErpInterfaceMessage`/`ErpReconciliation`; control-total reconciliation; failed items retried (max 10); API-key auth | `ProcureToPayTests` (batch reconciliation, wrong key → 401) |
| FR-FIN-003 | Commitments | Implemented | `Commitment` validated against contract ceiling and available budget before insert | `ProcureToPayTests` |
| FR-FIN-004 | Invoice registration | Implemented | `Invoice.PotentialDuplicate`/idempotency key; duplicate detection on invoice number or amount+date | `ProcureToPayTests` (idempotent replay) |
| FR-FIN-005 | Three-way control | Implemented | `ThreeWayCheckAsync` checks contract status, PO match, deliverable acceptance, ceiling; blocks certification on mismatch | `ProcureToPayTests` (over-billed invoice → ValidationFailed) |
| FR-FIN-006 | Payment certification | Implemented | Routed through workflow (PM_CONFIRM/FIN_CERTIFY); re-runs three-way guard on completion; fully audited | `ProcureToPayTests` |
| FR-FIN-007 | Payment status | Implemented | `Payment` rows created only from inbound ERP batches; no edit path exposed; read-only projection to project team | `ProcureToPayTests` (supplier portal shows Paid status) |
| FR-FIN-008 | Accruals | **Partial** | Manual accrual capture exists (`AddAccrualAsync`), but there is no inbound ERP message type for accruals — only Payment/Expenditure/Commitment — so "receive accrual estimates" from ERP is not supported | none found |
| FR-FIN-009 | Forecast final cost | Implemented | Each forecast call inserts an immutable `CostForecast` row; full EAC history retained | none found |
| FR-FIN-010 | Financial dashboard | Implemented | `DashboardAsync` composes portfolio view + certification/validation/duplicate/ERP-error counts; drill-down UI | none found |

## 5.8 Monitoring, Evaluation and Beneficiaries

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-ME-001 | M&E plan | Implemented | `MePlan` (frequency/methods/indicators/responsible officer), 1:1 with project | `DeliveryAndAssuranceTests.Monitoring_visit_finding_action_and_protected_beneficiaries` |
| FR-ME-002 | Monitoring templates | Implemented | `MonitoringTemplate` versioned; publishing retires prior version and freezes a field snapshot per visit | none found |
| FR-ME-003 | Site visit | Implemented | `MonitoringVisit` (date, officials, geo-coordinates, responses, outcome); responsive client component | same test |
| FR-ME-004 | Evidence | Implemented | `Evidence` links parent+document, carries SHA-256 hash, version, verification status | same test |
| FR-ME-005 | Findings | Implemented | `Finding` (severity/root cause/status); closes only once all corrective actions complete | same test |
| FR-ME-006 | Corrective action | Implemented | Requires closure evidence to complete; overdue actions escalate with incrementing level | same test (no-evidence 422); `PlatformAndReportingTests` (escalation) |
| FR-ME-007 | Beneficiary register | Implemented | Encrypted/masked identifiers with `[SensitiveData]`; PII reveal requires a dedicated permission and is audited | same test (403 without permission, audit assertion) |
| FR-ME-008 | Duplicate checks | Implemented | Same-project match blocks (conflict); cross-project match flags `PotentialDuplicate` for review | same test (asserts `PotentialDuplicate`) |
| FR-ME-009 | Lifecycle | Implemented | `BeneficiaryStatusHistory`; explicit allowed-transition state machine | none found directly |
| FR-ME-010 | M&E dashboard | Implemented | Visits/findings/overdue actions/beneficiaries/outcome trends, filterable by programme/project/provider | same test (dashboard call only, no field assertions) |

## 5.9 Risk, Compliance and Assurance

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-RSK-001 | Risk register | Implemented | `Risk` (Project/Contract/Programme parent); owner+review date enforced | `DeliveryAndAssuranceTests.Risk_rating_heat_map_and_critical_escalation` |
| FR-RSK-002 | Risk matrix | Implemented | Configurable likelihood/impact bands (must cover 1–25 without gaps); consistent rating engine | `EngineTests.RiskRatingTests`, same integration test |
| FR-RSK-003 | Controls | Implemented | `RiskControl` + `ControlAssessment` (appended, not overwritten) per assessment | none found |
| FR-RSK-004 | Mitigation | Implemented | `RiskTreatment` tracked to due date/status; overdue high-risk items escalate | same integration test (overdue flag only, not the scheduled job) |
| FR-RSK-005 | Compliance obligations | Implemented | `ComplianceObligation`/`ComplianceAttestation`; one attestation per obligation per period; comment required on non-compliance | none found |
| FR-RSK-006 | Audit findings | Implemented | `AuditFinding` with owner/due date/source/rating; corrective actions attach and require closure evidence | none found directly |
| FR-RSK-007 | Combined assurance | Implemented | `AssuranceCoverage` maps area × assurance line per period; computes coverage gaps | none found |
| FR-RSK-008 | Assurance dashboard | Implemented | Critical/high risks, overdue treatments/reviews, open findings, coverage gaps, heat map, top-10 drill-down lists | same integration test (critical risks assertion) |

## 5.10 Reporting, Analytics and Executive Dashboards

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-REP-001 | Operational reports | Implemented | `ReportCatalogue` (RPT-001..RPT-015) covering all named modules; export formats csv/xlsx/pdf | `PlatformAndReportingTests.Every_catalogue_report_runs_and_exports` |
| FR-REP-002 | Executive dashboard | Implemented | `ExecutiveDashboardAsync` builds portfolio KPIs each with a drill-down link | same test (drill-link assertion) |
| FR-REP-003 | Board reporting | Implemented | `BoardReportPack` versioned per period; dataset frozen at generation; approval requires a different user | `PlatformAndReportingTests.Board_pack_is_versioned_and_approved_by_someone_else` |
| FR-REP-004 | APP reporting | Implemented | `EvidenceVerificationAsync` (RPT-013) joins result→evidence→document, flags "Traceable" only when fully verified | shares BR-009 test coverage with FR-STR-006 |
| FR-REP-005 | Exception reporting | Implemented | Aggregates at-risk/over-budget/overdue items across every module with configurable thresholds | same "Every_catalogue" test |
| FR-REP-006 | Scheduled reports | **Partial** | Full backend (`ReportSchedule`, owner-only edit, live permission re-check on run), but no Angular component exists to manage schedules — only catalogue/exceptions/run/board-pack/analytics/data-quality UI | `PlatformAndReportingTests.Scheduled_report_runs_as_owner_and_queues_email_with_attachment` (backend only) |
| FR-REP-007 | Ad-hoc analytics | Implemented | `AnalyticsAsync` filterable per-project table, uniformly row-level-scoped | `PlatformAndReportingTests.Row_level_security_applies_to_reports` |
| FR-REP-008 | Data export | Implemented | Every export call logs an audit entry before returning the file; gated by a separate export permission | exercised, audit assertion not explicit |
| FR-REP-009 | Geographic view | Implemented | Groups by province/district/municipality; suppresses counts below a configurable minimum group size | same test (suppression path not directly asserted) |
| FR-REP-010 | Data quality dashboard | Implemented | Detects missing fields, duplicate suppliers/beneficiaries/invoices, unreconciled ERP batches; owner can resolve | `PlatformAndReportingTests.Data_quality_scan_raises_owned_issues_that_can_be_resolved` |

## 5.11 Workflow, Notifications and Administration

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| FR-ADM-001 | Workflow designer | Implemented | Versioned `WorkflowDefinition`, Draft→PendingApproval→Approved, dual control, immutable once approved | `PlatformAndReportingTests.Workflow_definition_changes_are_versioned_and_dual_controlled` |
| FR-ADM-002 | Delegations | Implemented | `Delegation` (authority type/role/amount/effective dates); `EnsureMayApproveAsync` enforces limits (BR-007) | `ProjectLifecycleTests` (executive approval limits) |
| FR-ADM-003 | Substitution | Implemented | Time-bound (max 90 days); revocation retained for history, not deleted | none found |
| FR-ADM-004 | Notifications | Implemented | In-system + email via outbox; editable templates with placeholder rendering | `PlatformAndReportingTests.Home_page_shows_approvals_for_approvers` |
| FR-ADM-005 | Escalations | Implemented | `WorkflowService.EscalateOverdueAsync` escalates to role, then Level 2; audited, 1-day re-escalation cooldown | `PlatformAndReportingTests` (sibling escalation path tested, not this exact method) |
| FR-ADM-006 | Reference data | Implemented | `ReferenceDataItem` unique per category+code; gated by AdminConfig permission | none found |
| FR-ADM-007 | Business calendar | Implemented | `BusinessCalendar.IsWorkingDay` excludes weekends+holidays; drives SLA due-date calculation | `EngineTests.Skips_weekends_and_holidays`, `Sla_hours_count_working_days` |
| FR-ADM-008 | Configuration audit | Implemented | `TetaSaveChangesInterceptor` captures old/new values for every change to every auditable entity | `PlatformAndReportingTests.Setting_changes_keep_old_and_new_values_in_the_audit_trail` |
| FR-ADM-009 | Bulk import | Implemented | CSV import for suppliers/reference data/holidays/compliance obligations; all-or-nothing with error report | `PlatformAndReportingTests.Bulk_import_validates_and_reports_errors` |
| FR-ADM-010 | Search | Implemented | Searches across project/procurement/contract/supplier/risk/invoice/document, each permission-gated and row-scoped | `PlatformAndReportingTests.Global_search_respects_row_level_scope` |

## 6. Security, Privacy and Access Control

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| SEC-001 | SSO | **Not Implemented** | No OIDC/SAML/external-IdP code anywhere in `Platform.Security` or the API; authentication is local username/password + JWT only | none found |
| SEC-002 | MFA | Implemented | RFC 6238 TOTP; enforced for privileged/configured roles; enrolment/challenge flow at login | `PlatformTests.TotpTests`, `PlatformAndReportingTests.Privileged_role_must_enrol_and_then_use_mfa` |
| SEC-003 | RBAC | Implemented | `[HasPermission]` policy attribute; permissions recomputed server-side from current role assignments every request | `PlatformAndReportingTests.Users_without_a_teta_role_are_refused` |
| SEC-004 | Row-level security | Implemented | `AccessScopeService`/`DataScope` restrict by Global/Portfolio/Programme/Project scope; out-of-scope returns 404 | `PlatformAndReportingTests.Row_level_security_applies_to_reports`, `Global_search_respects_row_level_scope` |
| SEC-005 | Segregation of duties | Implemented | Incompatible roles blocked (Supplier vs internal, SysAdmin vs SecAdmin); dual-control role approval; self-approval blocked | `PlatformAndReportingTests.Role_assignment_needs_a_second_security_administrator`, plus SoD checks in `ProcureToPayTests`/`DeliveryAndAssuranceTests` |
| SEC-006 | Encryption in transit | **Partial** | HTTPS redirection is config-flag-controlled (off in the sample Production config); TLS termination delegated entirely to IIS/Let's Encrypt; no HSTS middleware in code | none found |
| SEC-007 | Encryption at rest | **Partial** | Field-level AES-256-GCM encryption exists for specific fields (MFA secrets, sensitive identifiers) via `AesGcmFieldProtector`, but database/backup/document-storage encryption is an infrastructure concern with no code-level enforcement | `PlatformTests.FieldProtectionTests` |
| SEC-008 | Secrets | Implemented | All secrets are `REPLACE_WITH_...` placeholders in committed config; `RequireSecret()` throws at startup on a missing/placeholder/short secret | none found |
| SEC-009 | Session security | Implemented | Server-side `UserSession` with inactivity timeout, revocation on password change, validated on every request via `JwtBearerEvents` | `PlatformAndReportingTests.Logout_revokes_the_server_side_session` |
| SEC-010 | Audit trail | Implemented | Audit rows are append-only (modify/delete throws); every row HMAC-sealed; integrity re-verifiable on demand | `PlatformTests.AuditSealTests` |
| SEC-011 | Privacy | **Partial** | Masking and keyed-hash matching exist, and beneficiary PII has its own gated permission, but there's no evidence of a systematic minimisation/purpose-limitation policy applied across all personal-data fields | none found |
| SEC-012 | Export controls | Implemented | Every export logs an audit entry (code/format/row count); requires a separate export permission from read access | none found |
| SEC-013 | Vulnerability management | **Not Implemented** | CI (`ci.yml`) runs build+test only; no SAST, dependency scanning (Dependabot/CodeQL/`npm audit`), or penetration-test evidence | none found |
| SEC-014 | Logging/SIEM | **Not Implemented** | Serilog configured to console only; no SIEM/central-monitoring sink anywhere in the codebase | none found |
| SEC-015 | Backup security | Not Verifiable In Code | Backup/restore is a SQL Server + IIS operations concern outside this repository | none found |

## 12. Non-Functional Requirements

| ID | Requirement | Status | Implementation | Test coverage |
|---|---|---|---|---|
| NFR-001 | Availability (≥99.5%) | Not Verifiable In Code | Single self-hosted IIS VPS per `deploy.yml`, no redundancy/load-balancing config in repo; an SLA commitment, not a code property | none found |
| NFR-002 | Performance (≤3s p95) | Not Verifiable In Code | No load/performance test harness in the repo | none found |
| NFR-003 | Scalability (2x/3x) | Not Verifiable In Code | Nothing structurally prevents scaling (stateless API, paged queries), but nothing in-repo proves it either | none found |
| NFR-004 | Concurrency | Implemented | Every `AuditableEntity` has an EF concurrency token; `DbUpdateConcurrencyException` mapped to HTTP 409 | none found |
| NFR-005 | Resilience | **Partial** | Retry loop on number generation, outbox pattern for email, non-blocking seed on startup — but no `EnableRetryOnFailure` on the EF SQL provider and no circuit breaker (Polly) for ERP/SMTP calls | none found |
| NFR-006 | RPO ≤4h | Not Verifiable In Code | Ops/backup-schedule commitment; no backup job defined in this repo | none found |
| NFR-007 | RTO ≤8h | Not Verifiable In Code | Ops/DR commitment; no failover automation in this repo | none found |
| NFR-008 | Accessibility (WCAG 2.1 AA) | **Not Implemented** | Only 2 of ~107 component files use `aria-label`/`role` at all; no accessibility test tooling (axe-core, pa11y) in `package.json` | none found |
| NFR-009 | Browser support | Not Verifiable In Code | CI only exercises Chrome headless; no browserslist config or Edge-specific test | none found |
| NFR-010 | Responsive | **Partial** | Responsive viewport meta tag and Material's responsive components give a basic tablet-capable layout, but no `@media` queries found anywhere and no field/M&E-specific mobile view | none found |
| NFR-011 | Maintainability (configurable without redeploy) | Implemented | `SystemSetting` key/value store; notification templates, retention policy, workflow thresholds all DB-editable | `PlatformAndReportingTests.Setting_changes_keep_old_and_new_values_in_the_audit_trail` |
| NFR-012 | Observability | Implemented | `CorrelationIdMiddleware`, Serilog request logging, `/health/live` and `/health/ready` endpoints, correlation ID on every audit row | `PlatformAndReportingTests.Responses_carry_security_headers_and_correlation_id` |
| NFR-013 | Data retention | **Partial** | `RetentionPolicy` is fully configurable per record class, but no purge/archive job actually enforces disposal — configuration exists, enforcement doesn't | none found |
| NFR-014 | Interoperability | **Partial** | REST API is URL-versioned with a generated Swagger/OpenAPI document, but no formal versioning policy or contract-testing beyond the generated spec | none found |
| NFR-015 | Auditability | Implemented | Old/new values captured per changed property (sensitive fields redacted), sealed, queryable, with an integrity-verification endpoint | `PlatformTests.AuditSealTests` |
| NFR-016 | Usability (role landing pages) | Implemented | `HomeService` combines role list, KPIs, pending approvals inbox, and assigned actions into one landing surface | `PlatformAndReportingTests.Home_page_shows_approvals_for_approvers` |

---

## Notes on methodology

This matrix was produced by five parallel code-reading passes (one per module group), each instructed to read the actual source files and test files rather than infer behaviour from names, and to report "Not Implemented"/"Partial" honestly rather than assume a feature exists because the SRS says it should. Findings were consolidated without editing the underlying verdicts. Where a requirement's evidence quotes a class or method name, that name can be grepped directly in `src/Teta.Ippcms.*` to verify it.

**What this document is not:** a substitute for UAT sign-off, a security penetration test, or a performance test. Section 18 of the SRS ("Solution Acceptance and Go-Live Criteria") requires all of those separately before a production go-live decision — this matrix only establishes what the initial build actually contains against the functional/security/non-functional baseline.

**Suggested next steps**, in order of what would most change the go-live picture:
1. Decide whether SEC-001 (SSO) is required for go-live or can be handled by TETA's existing enterprise IdP as a follow-on integration — this is normally a policy/timeline decision, not a large engineering one, since the RBAC/permission layer underneath is already in place.
2. Add SAST/dependency scanning to `ci.yml` (SEC-013) — this is comparatively low effort (a GitHub Actions step) with high signal value before go-live.
3. Decide on a SIEM/log-forwarding target (SEC-014) if TETA's ICT function operates one centrally.
4. Run an accessibility audit pass (NFR-008) against the small set of core user journeys the SRS names as critical (§16.1), rather than all ~107 components at once.
5. Fill the test-coverage gaps flagged "none found" above for Must-priority requirements, prioritising Budget & Procurement Planning (§5.3) and Contract Management (§5.5), which have the thinnest coverage relative to how much domain logic they carry.
