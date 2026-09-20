# IFWEMS Gap Analysis & UI Modernisation Report

**Date:** 19 September 2026
**Reviewed against:** `IFWEMS_Technical_System_Design_Department_of_Health.docx` (Version 1.0, Design Baseline, September 2026)
**Scope of this pass:** (1) verify the build against the technical design/requirements document; (2) fix what was safely fixable in this session; (3) modernise the Angular UI.

This report supersedes the "PRODUCTION READY" conclusion in `REQUIREMENTS_VALIDATION_REPORT.md`. That report is not wrong about what it tested — the Playwright suite genuinely does pass — but it only exercised a handful of happy-path flows (login, case creation, page loads) and did not check the build against the full 43-section design document. This review does that, module by module, and treats the design document's own code blocks (they read like ready-made schemas) as the acceptance bar.

## 1. How to read the module table

Each of the 20 core modules (Section 3 of the design document) is marked:

- **Implemented** — the entity, API and a usable UI all exist and match the spec closely enough to use as designed.
- **Partial** — some of entity/API/UI exists, but a meaningful piece named in the spec is missing.
- **Missing** — nothing exists in the codebase for this module beyond, at most, a status label or placeholder screen.

## 2. Module-by-module status

| # | Module | Status | Notes |
|---|---|---|---|
| M01 | Authentication & User Management | Implemented | JWT auth, `Admin/UsersController`, `Admin/RolesController`, seeded roles/permissions. See §4 for a caveat on how permissions are actually enforced. |
| M02 | Organisation Management | Implemented | `OrgUnit` hierarchy, `Admin/OrgUnitsController`, admin UI. |
| M03 | Delegation Management | **Missing** | No `Delegation` entity, controller, or UI anywhere in the codebase. Financial/SCM delegation limits are not modelled at all — the compliance engine's `CEILING_EXCEEDED` rule uses a single hard-coded ceiling in a rule's `ParametersJson`, not a per-role/per-org-unit delegation table. See §5.1. |
| M04 | Legislative / Compliance Rules Engine | Implemented (UI was missing, now fixed) | `ComplianceRule` entity + `ComplianceRulesController` already supported versioning, effective-dating and a maker/checker approve step. There was no admin screen to use any of it — `admin-rules.component` was a static placeholder card with no data binding. Rebuilt as a full list/create/approve page (`/admin/rules`). |
| M05 | Transaction Screening | Partial (one defect fixed) | `Transaction` entity and `POST /api/compliance/check` existed, but the engine never saved a `Transaction` row for a routine check — only emergency overrides were persisted. This silently disabled the `DUPLICATE_INVOICE`, `REPEAT_EMERGENCY_PROCUREMENT` and `SPLITTING` rule checks for all normal traffic, since those rules look up transaction history that was never being written. **Fixed** in `ComplianceRuleEngine.cs` (§5.2). The register was also invisible — no controller or screen ever read the `Transactions` table. Added `TransactionsController` + `/compliance/transactions` screen. The `Transaction` entity itself is still narrower than the design's `financial_transaction` table (no PO/invoice/contract linkage columns) — a schema follow-up, not fixed here (needs an EF migration; see §6). |
| M06 | Contract Management | Partial | `Contract` entity, CRUD, utilisation/expiry alerting all exist and work. No `ContractVariation` entity — variation history isn't tracked, only the current rolled-up value. See §5.3. |
| M07 | Procurement Compliance | Partial | Rule catalogue covers ceiling/expiry/duplicate/retrospective-PO/splitting/repeat-emergency (Section 7's table). No explicit tender/quotation/deviation workflow entities — deviations are implicit in rule outcomes, not tracked as their own record. |
| M08 | Incident / Case Management | Implemented | Full case lifecycle, numbering (`[DEPT]-[TYPE]-[FY]-[SEQUENCE]`), status history. UI rebuilt (§7). |
| M09 | Assessment & Determination | Partial | Assessment (`CaseAssessment` + `CaseAssessmentsController`) is implemented. **Determination is not** — there is no `case_determination` entity anywhere; "Determined" exists only as a `CaseStatus` enum value with no structured record of root cause, responsible official, or fraud/misconduct flags. See §5.4. |
| M10 | Investigation Management | Implemented | `Investigation` entity with maker/checker approval; UI rebuilt. Evidence is attached via the generic `Document` entity rather than a dedicated `investigation_evidence` table, but it does carry an integrity hash and version, which covers the intent of Section 12. |
| M11 | Loss & Recovery | Implemented | `Recovery` entity, write-off approval; UI rebuilt. |
| M12 | Consequence Management | **Missing** | No `consequence_action` entity, controller or UI. "ConsequenceManagement" exists only as a `CaseStatus` value. Disciplinary/civil/criminal actions, referrals, hearing dates, outcomes and sanctions (Section 14) are not tracked anywhere. See §5.5. |
| M13 | Corrective Action | Implemented | `CorrectiveAction` entity, verification workflow, linked to `Control` library; UI rebuilt. |
| M14 | Document Repository | Implemented | Hash, version, confidentiality classification all present; UI rebuilt. |
| M15 | Notifications & Escalations | Partial | In-app `Notification` delivery and `SlaController` (breach/approaching detection) both work. `NotificationTemplate` entity exists but has **no controller at all** — templates can't be created or edited via the API, so `/admin/notifications` is necessarily still a placeholder (not fixed in this pass; needs a new controller). SLA policies had no admin UI — added (`/admin/sla`). |
| M16 | Dashboards & Analytics | Partial | A single role-agnostic dashboard exists with metric cards, status/type distribution and quick links. The design's separate Accounting Officer and CFO dashboards (Section 24.1/24.2, e.g. case-age analysis, SLA performance, repeat-supplier findings) are not built as distinct views. |
| M17 | Reporting | Partial | Three CSV registers (cases, recoveries, contracts) work and were restyled. The design lists ~14 registers/reports (Section 25) — most are not implemented, and PDF/Word export isn't implemented (CSV only). |
| M18 | Audit Trail | Partial (UI was missing, now fixed) | Write side was solid — an append-only interceptor logs material changes and the API blocks any delete/update of audit rows. Nothing ever read it: no controller, no screen. A `ReadOnlyAuditor` role exists in the seed data but had nothing to actually read. Added `AuditController` + `/audit` screen with filters and a before/after diff view. |
| M19 | Integration Services | Partial | `BatchImportController` supports validated CSV import for suppliers/contracts with row-level accept/reject reporting, matching Section 23's "manual controlled import" mode. No real-time/near-real-time API integration with BAS/LOGIS/PERSAL/CSD exists (expected — those depend on external departmental systems this project can't reach). |
| M20 | System Administration | Partial | Org units, users, roles are all administrable. Most of Section 30's configuration list (financial years, procurement thresholds, delegation limits, expenditure types, root causes, escalation levels, business-day calendar) has no dedicated configuration screen — `admin-system-config.component` is still a placeholder. |

## 3. Minimum Acceptance Criteria (Section 39) — spot check

The design document sets its own bar in Section 39. Checking against it directly:

- *"A case can be created, uniquely numbered, assigned, assessed, investigated, actioned and closed with full history"* — **met**.
- *"Authorised users can create/version/test/approve rules without source-code modification"* — the backend met this already; the UI didn't expose it at all, so in practice no one could. **Now met** (`/admin/rules`).
- *"A transaction can be evaluated and returned with score, decision, failed rules and messages"* — met for the single-check response, but see M05: the result wasn't being kept anywhere, which undermines *"Reporting: Core registers reconcile to underlying case and transaction values"* — there was no transaction register to reconcile against. **Fixed.**
- *"Material changes show user, timestamp, old value and new value"* — the data was captured correctly but nothing could display it. **Fixed** (`/audit`).
- *"Role and permission restrictions operate at record, function and document level"* — see §4; this is the one item I'd flag as **not actually met** despite looking met.

## 4. RBAC: a design gap worth flagging even though it "works" today

The domain model has a proper `Permission` / `RolePermission` table, and `Admin/RolesController` lets an administrator assign arbitrary permission codes to a new role. But no controller anywhere checks a permission code — every single `[Authorize]` attribute in the API is written as `[Authorize(Roles = "ComplianceOfficer,SystemAdministrator")]`, a hard-coded list of role *names*.

The practical effect: if the Department creates a new role tomorrow — say, `DistrictComplianceLead` — and assigns it the `cases.approve` permission through the admin UI exactly as designed, that role will not be able to approve anything, because no code path checks `cases.approve`; they all check whether the user's role name is literally the string `"ApprovingOfficial"`. This directly contradicts the design document's own principle (Section 4): *"permissions maintained independently from roles so the Department can alter responsibilities without code changes."* Today, every responsibility change requires a code change.

This wasn't fixed in this session — it's a cross-cutting change (a custom `IAuthorizationHandler` + policy provider that resolves `[Authorize(Policy = "cases.approve")]` against the user's actual `RolePermission` rows, then updating every controller to use it) that touches the whole API surface, and this environment has no .NET compiler to verify a change of that size against (see §8). It's flagged here as the top architectural priority for the next development pass, precisely because it's the kind of gap that a demo won't surface — it only shows up the day someone actually tries to reorganise roles without redeploying code.

## 5. Missing-module details and suggested schemas

For the four modules below, the design document's own code blocks are close enough to a working schema that a developer could implement each in roughly a day using the existing modules as a template (`CorrectiveAction`/`CorrectiveActionsController` is a good structural model for all four: a child entity keyed by `CaseId`, a controller under `api/cases/{caseId}/...`, an Angular service + model, and a section in `case-detail.component`).

### 5.1 Delegation Management (M03)

Add a `Delegation` entity (role or org-unit scoped financial/SCM authority limit, with an effective-date range) plus an `Admin/DelegationsController`. Then change `ComplianceRuleEngine.IsCeilingExceededAsync` to look up the delegation ceiling for the transaction's org unit/role instead of the rule's own static `maxAmount` parameter — that's the piece that actually makes delegation limits mean something operationally rather than just being a configuration screen nobody reads from.

### 5.2 Financial Transaction persistence (fixed this session)

`ComplianceRuleEngine.EvaluateAsync` (in `src/IFWEMS.Infrastructure/Compliance/ComplianceRuleEngine.cs`) now persists a `Transaction` row for every check, not only overrides. This one-line-of-consequence fix has an outsized effect: it's what makes the `DUPLICATE_INVOICE`, `REPEAT_EMERGENCY_PROCUREMENT` and `SPLITTING` rules actually able to see prior transactions at all. **This change has not been compiled** — there is no .NET SDK available in this session's environment (see §8). Please run `dotnet build` before relying on it.

### 5.3 Contract Variation tracking (part of M06)

Add a `ContractVariation` entity (`variation_type`, `variation_amount`, `motivation`, `approval_authority`, `approval_date`, `effective_date` — Section 17 gives this almost verbatim) linked to `Contract`. Right now a contract variation just overwrites `Contract.CurrentValue` with no record of what changed, who approved it, or why — which breaks the "no silent changes" principle the rest of the system is built around.

### 5.4 Determination Module (M09)

Add `case_determination` as its own entity (the design document's Section 11 block: `determination_officer`, `responsible_official`, `responsible_unit`, `control_failure`, `root_cause`, `fraud_suspected`, `misconduct_suspected`, `determination_conclusion`, `approved_by`), a controller under `api/cases/{caseId}/determinations`, and a section in the case detail page next to Assessment. Right now "a determination happened" is indistinguishable from "the case's status field says Determined" — there's no record of who decided what, or why.

### 5.5 Consequence Management (M12)

Add `consequence_action` (Section 14: `action_type` — disciplinary/civil/criminal/administrative — `referral_date`, `referred_to`, `hearing_date`, `outcome`, `sanction`, `status`), a controller, and a case-detail section. This is the module furthest behind relative to how much the design document specifies for it (a full permission group, `CONSEQUENCE_VIEW/UPDATE/CLOSE`, already exists in the seed data with nothing behind it).

## 6. What was fixed in this session

**Backend (all additive; no existing endpoint's behaviour changed except the one correctness fix):**

- Fixed `ComplianceRuleEngine.cs` to persist every screened transaction, not only overrides (§5.2).
- Added `AuditController.cs` — filterable, paginated read access to the existing audit log (`GET /api/audit`, `GET /api/audit/entity-names`).
- Added `TransactionsController.cs` — filterable, paginated read access to the transaction screening register (`GET /api/transactions`).

**Frontend — rebuilt from raw unstyled HTML to Angular Material, consistent with the app's existing navy/gold theme:** case list, case detail (all five sub-sections: investigations, recoveries, corrective actions, documents, plus the status-change panel), contracts, suppliers (including the risk-profile panel), compliance check, emergency override, notifications, reports.

**Frontend — new functionality:**

- `/admin/rules` — compliance rules are now creatable, listable and approvable from the UI (backend already supported this; nothing did before).
- `/admin/sla` — SLA policies are now creatable and listable, with a live "cases currently breaching SLA" panel (backend already supported this too).
- `/audit` — the audit trail is now browsable, filterable by entity/user/action, with an expandable before/after diff per row.
- `/compliance/transactions` — the transaction screening register is now browsable, filterable by risk rating, distinguishing routine checks from emergency overrides.
- A global search box in the header (cases, suppliers, contracts by number/name/title) — a lightweight, client-side answer to Section 25's "Global Search" requirement; it reuses the existing list endpoints rather than adding a new search endpoint, so it's a reasonable starting point rather than a full implementation (see §9 for the scaling caveat).
- A shared design-system layer added to `styles.scss`: page header/card conventions, a status-chip and risk-chip system with consistent colour coding across every screen, table/empty-state/loading-state patterns, and a reusable `EnumLabelPipe` that turns the API's PascalCase enum values (e.g. `UnderInvestigation`) into readable labels everywhere instead of each screen inventing its own formatting.

**Not touched, deliberately:** the admin shell (toolbar, sidenav, dark-mode toggle), the dashboard, the login page and the public landing page were already built with Angular Material and a coherent brand identity — they were the parts of the UI actually built well by the prior pass, so this session left them as-is rather than needlessly rewriting working, good code.

## 7. What is recommended but not done

- Build out Delegation Management, Determination, Consequence Management and Contract Variations as described in §5.
- Move authorization from hard-coded role-name checks to permission-code policy checks (§4) — this is the single highest-value backend change available given how much of the permission infrastructure already exists unused.
- Add a `NotificationTemplatesController` so `/admin/notifications` and `/admin/system-config` can become real screens instead of placeholders.
- Widen the `Transaction` entity to match the design's `financial_transaction` table (PO number/date, invoice number, contract linkage, cost centre) so the screening register can support the drill-down and reconciliation the design calls for.
- Build the two named executive dashboards (Accounting Officer, CFO — Section 24) as distinct views rather than one generic dashboard.
- Extend the reporting module toward the ~14 named registers in Section 25, and add PDF/Word export alongside the existing CSV export.

## 8. A note on how this review was carried out, and its limits

This review was done through a bridge to the developer's own Windows machine, working directly on the project at `C:\Thatha\projects\IFWEMS`. That environment has Node.js and the project's own `node_modules`, but no .NET SDK and no network access to install one. Every Angular/TypeScript change in this pass was verified with the Angular AOT compiler (`ngc -p tsconfig.app.json`), which does full template type-checking — the same checking `ng build` does — and it reports zero errors across every file touched. The full `ng build`/`ng serve` bundling step itself could not be run: this project's `node_modules` was installed on Windows and only contains the Windows-specific Rollup native binary (`@rollup/rollup-win32-x64-msvc`), which doesn't run in the Linux environment this session's file-editing bridge uses, and there's no network access here to install the Linux equivalent. **Please run `npm start` or `ng build` on your own machine as a final check** — it should work exactly as it did before this session, since nothing about `package.json` or `node_modules` was changed.

The three new/changed C# files (`ComplianceRuleEngine.cs`, `AuditController.cs`, `TransactionsController.cs`) were written by hand-matching the exact patterns, field names and types already used throughout the rest of the API (verified against every entity and controller they touch), but could not be compiled in this session for the same reason (no .NET SDK, no network). **Please run `dotnet build` before deploying.** If it doesn't build cleanly, the errors will very likely be trivial (a missed `using`, a naming mismatch) given how mechanically these were written — but they should still be checked before this goes anywhere near production.

## 9. Honest caveats on what was added

- The global search (§6) is a client-side filter over data fetched from the existing list endpoints. It will feel instant and work well at the data volumes a pilot or small department would have; it will not scale to a national-level case/supplier/contract volume without a real server-side search endpoint. It's a legitimate way to satisfy the requirement today and a clear seam to swap out later.
- The Audit Trail and Transaction Register screens are read-only by design (matching the "audit records can never be edited or deleted" principle) — there was no risk taken there, only surfacing.
- Nothing in this pass touched the database schema. Every backend change works against tables that already exist.
