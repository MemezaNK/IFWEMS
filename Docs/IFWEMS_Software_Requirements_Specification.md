# IFWEMS — Software Requirements Specification (SRS)
**Irregular, Fruitless & Wasteful Expenditure Management and Prevention System**
Government Department of Health

| | |
|---|---|
| Derived From | System Narrative Description v1.0 (13 Sep 2026); Technical System Design v1.0 (Sep 2026) |
| Document Type | Refined Software Requirements Specification + Production-Readiness Gap Analysis |
| Prepared As | Senior Technical Business Analyst review |
| Status | Draft for review |

---

## 1. Purpose and Scope

This SRS consolidates and formalizes the requirements expressed in the two source documents into testable, numbered functional (FR) and non-functional (NFR) requirements, and identifies **requirements gaps that must be closed before the system can be safely operated in a live production environment**. Source documents describe a strong conceptual/business design and a solid technical blueprint, but — as is typical of narrative/design documents — they omit a number of operational, governance and engineering requirements that are mandatory for production go-live. These are captured in Section 8.

---

## 1A. Platform Directive (Mandatory)

The following architectural decisions are **fixed requirements**, not options, and override any technology-neutral language elsewhere in the source documents:

- **PLAT-01**: IFWEMS shall be delivered as a **website** — a browser-based, responsive web application. No native desktop client is in scope; mobile is supported via responsive web, not a native app, unless separately approved.
- **PLAT-02**: The system shall use a **backend application server** (API layer) backed by a **SQL relational database** (e.g., Microsoft SQL Server or PostgreSQL) as the system of record. NoSQL/document stores may only be used for secondary concerns (e.g., document blob storage, search indexing), never as the primary transactional store.
- **PLAT-03**: All enterprise cross-cutting functionality — **user management, role-based access control, authentication, notification emails, audit logging, system configuration, and reporting** — shall be **built natively into the application** (first-party modules owned by IFWEMS), not assumed to be provided solely by external/enterprise infrastructure. Integration with the Department's enterprise directory (SSO) is additive/optional, not a substitute for the application's own user/role administration screens.
- **PLAT-04**: The application shall ship with a complete, self-contained administration console for managing users, roles, permissions, organisational units, notification templates, and system configuration — usable even if enterprise SSO/identity integration is unavailable or delayed.

---

## 2. Functional Requirements (derived and numbered)

### 2.1 Identity, Access and Organisation
- **FR-001**: The system shall provide a **built-in user management module** (create/edit/deactivate/reset-password, bulk import, self-service profile) as a first-party feature, independent of any external directory. Where available, the system shall additionally support SSO against the Department's enterprise identity provider, with MFA enforced for all privileged and financially sensitive roles.
- **FR-002**: The system shall implement **native RBAC administration** — an in-app UI for creating roles, assigning granular permissions, and scoping access by organisational unit (Department → District → Facility → Cost Centre) — with no dependency on external tooling to manage roles.
- **FR-003**: The system shall enforce maker-reviewer-approver segregation of duties for: case capture/approval, rule creation/approval, investigation performance/approval, recovery capture/write-off approval.
- **FR-004**: The system shall support an unlimited-depth organisational hierarchy with effective-dated changes, managed through the built-in administration console.
- **FR-005**: The system shall provide a **built-in notification engine** that sends transactional and workflow emails (SLA warnings, escalations, approvals, assignment, overdue tasks, override alerts) using configurable, in-app-editable email templates and a pluggable SMTP/mail-provider connection — no external notification system dependency required.
- **FR-006**: The system shall provide in-app notification centre (bell/inbox) mirroring email notifications, with read/unread state and per-user notification preferences.

### 2.2 Transaction Screening and Prevention
- **FR-010**: The system shall expose a real-time compliance-check API accepting transaction data and returning a risk score, GREEN/AMBER/RED rating, failed rule codes, and a recommended action (PASS/REVIEW/BLOCK).
- **FR-011**: Compliance rules shall be stored as versioned, effective-dated, configurable records (no hard-coded thresholds/legislative values) and shall require maker-checker approval before activation.
- **FR-012**: The system shall support the rule catalogue described (retrospective PO, expired contract, ceiling exceeded, splitting, duplicate invoice, repeat emergency procurement) as a configurable minimum set, extensible without code change.
- **FR-013**: The system shall provide a controlled emergency-override workflow requiring reason, emergency category, approving official/delegation, date/time, supplier, amount and evidence, and shall automatically create a post-transaction compliance review.
- **FR-014**: The system shall detect and flag repeated emergency procurement of the same commodity/supplier/facility within a configurable window.

### 2.3 Case Management Lifecycle
- **FR-020**: The system shall generate unique, centrally issued, non-reusable case numbers in the format `[DEPT]-[TYPE]-[FY]-[SEQUENCE]`.
- **FR-021**: The system shall support case types IE, FWE, UE, PNC and associated status lifecycle per Appendix A of the technical design.
- **FR-022**: The system shall provide structured, configurable assessment questionnaires and capture assessor conclusions, reasons and evidence.
- **FR-023**: The system shall support determination, investigation, recovery, consequence-management and corrective-action modules linked to a single case record, with full status and history tracking.
- **FR-024**: The system shall prevent deletion of case history through any application function.
- **FR-025**: The system shall track recovery transactions individually and reconcile recovered amounts against recoverable balances.
- **FR-026**: The system shall link confirmed incidents to a control library and require verified evidence of corrective-action implementation before closure.

### 2.4 Contracts, Suppliers, Documents
- **FR-030**: The system shall track contract value, variations, utilisation and expiry, and generate alerts at configurable thresholds (e.g., 120/90/60/30/14 days; 70–100% utilisation).
- **FR-031**: The system shall maintain a supplier risk profile aggregating deviation counts, case counts and concentration indicators.
- **FR-032**: The system shall store evidentiary documents with metadata (type, uploader, timestamp, version, confidentiality classification) and an integrity hash captured at upload.

### 2.5 Workflow, Notification, Reporting
- **FR-040**: Workflow stages, roles, SLAs, approval and escalation rules shall be stored as configuration and be effective-dated/versioned.
- **FR-041**: The system shall generate dashboard and email notifications on SLA breach/approach, with tiered escalation.
- **FR-042**: The system shall provide the registers and reports listed in Narrative §23 / Technical §25, exportable to PDF/Excel/CSV, with permission-scoped drill-down.

### 2.6 Integration and Audit
- **FR-050**: The system shall integrate with BAS, LOGIS, PERSAL, CSD and other departmental systems via secured APIs where available, and via validated batch/CSV/XML/JSON import with batch reconciliation (rows received/accepted/rejected) otherwise.
- **FR-051**: The system shall record every material user/system action to an immutable audit log capturing user, session, action, entity, before/after values, timestamp and technical context, with no application-level delete capability.

### 2.7 Platform, Backend and Data Store
- **FR-060**: The system shall be a browser-based website; all functional modules (cases, assessments, investigations, recoveries, dashboards, admin) shall be accessible through the web UI without requiring installed client software.
- **FR-061**: The backend shall be a service/API layer backed by a **SQL relational database** as the authoritative transactional data store, with a normalized schema aligned to the entities defined in Section 6 of the Technical Design (case, transaction, contract, supplier, document, audit_log, etc.).
- **FR-062**: Database schema changes shall be managed through versioned migration scripts executed as part of the deployment pipeline (no manual/ad-hoc production schema edits).

### 2.8 User Interface / User Experience
- **FR-070**: The UI shall follow a **modern, clean, easy-to-navigate design** using a consistent design system/component library (e.g., Material Design, Ant Design, or equivalent), with a persistent left/side navigation, breadcrumbs, and global search.
- **FR-071**: The UI shall be **responsive** (desktop, laptop, tablet) and support both light and dark modes where feasible.
- **FR-072**: Role-based landing dashboards shall present the most relevant information/actions for that role within one click of login (e.g., pending tasks, overdue items, quick-create case).
- **FR-073**: Forms shall provide inline validation, clear error messaging, and progressive disclosure (multi-step wizards) for complex processes (case assessment, investigation, emergency override) to reduce user error and training burden.
- **FR-074**: The UI shall meet WCAG 2.1 AA accessibility conformance as a testable acceptance gate (not aspirational).

---

## 3. Non-Functional Requirements (as specified)

| ID | Requirement | Target |
|---|---|---|
| NFR-01 | Availability | ≥99.5% |
| NFR-02 | Standard response time | <3s |
| NFR-03 | Dashboard response | <5s |
| NFR-04 | Audit coverage | 100% of material actions |
| NFR-05 | Encryption | In transit and at rest |
| NFR-06 | Backup | Daily full/incremental, tested restores |
| NFR-07 | DR | Approved RPO/RTO |
| NFR-08 | Browser support | Current Edge/Chrome |
| NFR-09 | API | REST, versioned, OpenAPI documented |
| NFR-10 | Auth | SSO/MFA where available |

*(These targets are stated in the source design but, as noted in Section 8 below, lack the measurable detail needed for a signed-off SLA/OLA and test plan.)*

---

## 4. What Is Missing for a Live Production Environment

The source documents are strong on **business process and logical architecture** but are a design baseline, not an operations-ready specification. The items below are commonly omitted at this stage and should be added as explicit requirements before go-live.

### 4.1 Non-Functional Requirements — insufficiently specified
1. **RPO/RTO are not quantified.** "Approved RPO/RTO" must become explicit numbers (e.g., RPO ≤ 15 min, RTO ≤ 4 hrs) tied to a tested DR runbook and DR test cadence (e.g., bi-annual).
2. **Concurrency/load targets are undefined.** No stated number of concurrent users, transaction throughput (transactions/sec at month-end/year-end peaks), or database sizing/growth projections over 5 years.
3. **Capacity planning for peak periods** (financial year-end, AGSA audit season) is not addressed — historically the highest-load period for this exact use case.
4. **Observability/monitoring is absent**: no requirement for APM, structured logging with correlation/trace IDs, centralized log aggregation (e.g., ELK/Azure Monitor), health-check/readiness endpoints, uptime dashboards, or alerting thresholds tied to on-call escalation.
5. **No defined Service Level Objectives (SLOs)/error budgets** separate from the high-level 99.5% availability figure, and no distinction between planned maintenance windows and unplanned downtime.
6. **Performance test acceptance criteria are missing** — "load/performance testing" is listed as an activity but with no pass/fail thresholds.
7. **Accessibility is aspirational ("WCAG-aligned practices where applicable")** rather than a testable conformance target (e.g., WCAG 2.1 AA) with an accessibility audit gate before release.
8. **Data retention/archival and legal-hold requirements are not defined** — retention periods per record type, archival tiering, and a process for placing a case under litigation/legal hold that prevents purge even after retention expiry.
9. **Time zone and business-day calendar governance**: while SLA "business day" flags are mentioned, there's no requirement for a maintained public-holiday calendar per province/facility, or explicit system time zone (SAST) handling for timestamps across integrated systems.

### 4.2 Security — gaps beyond the stated architecture
10. **Secrets management is not addressed** — no requirement for a vault (e.g., Azure Key Vault/HashiCorp Vault) for API keys, DB credentials, integration secrets; credentials must not live in config files or source control.
11. **Dependency/supply-chain security** is missing — no requirement for SCA (software composition analysis), vulnerability scanning of third-party libraries/containers, or an SBOM.
12. **API security detail is shallow** — rate limiting/throttling, input validation/schema enforcement, OWASP API Top-10 controls (BOLA/broken object-level authorization is critical given case-record sensitivity), and API key/token rotation policy are not specified.
13. **Password/session policy specifics are missing** — complexity rules, lockout thresholds, idle/absolute session timeout values, concurrent-session policy.
14. **User lifecycle management** — no requirement for automated de-provisioning on termination/transfer (linked to PERSAL/HR feed), periodic access recertification (e.g., quarterly), or orphaned-account detection.
15. **Data classification enforcement mechanics** are named (PUBLIC…INVESTIGATION RESTRICTED) but no technical control model (e.g., attribute-based access control on top of RBAC, field-level masking for banking/ID details) is specified.
16. **POPIA compliance detail is thin** — no Data Protection Impact Assessment (DPIA) requirement, no data-subject-access-request handling process, no cross-border transfer control if cloud-hosted outside SA, and no breach-notification procedure/timeline.
17. **Penetration testing cadence and remediation SLAs** are not defined (e.g., annual pen-test + before each major release, critical findings remediated within 15 days).
18. **Immutable audit log technology is unspecified** — how immutability is enforced (WORM storage, append-only DB constraints, log signing/hash-chaining) beyond "no delete via application."

### 4.3 Operations, Deployment, Support
19. **No support/operations model** — Level 1/2/3 support structure, incident severity definitions, response/resolution SLAs, helpdesk tooling, and after-hours/on-call coverage for a system affecting patient-care-linked emergency procurement are all absent.
20. **No defined change-management/release-cadence** beyond "CI/CD pipeline exists" — no requirement for change advisory board (CAB) sign-off for production changes, maintenance-window notification process, or emergency-change (hotfix) procedure.
21. **Rollback/runbook detail is generic** — "rollback plan" is mentioned but there's no requirement for database migration reversibility, blue-green/canary deployment strategy, or feature flagging for risky features (e.g., new compliance rules).
22. **Configuration/secret parity across environments (Dev/SIT/UAT/Pre-Prod/Prod)** is not explicitly required, risking config drift; needs Infrastructure-as-Code with environment-specific parameterization.
23. **Data migration and cutover strategy** for existing/legacy irregular expenditure registers is not addressed — no requirement for historical data migration, reconciliation, dual-run/parallel-run period, or go-live cutover plan.
24. **Post-go-live hypercare period** is not defined (e.g., 4–6 weeks elevated support after go-live with daily defect triage).
25. **Licensing and Total Cost of Ownership** — no requirement to document COTS licensing (BI tool, workflow engine, identity provider, DMS) cost/scaling model, which affects Treasury procurement approval.
26. **Vendor/SLA contract requirements** — since a third-party will likely build/host this, there is no requirement for contractual SLAs, penalty clauses, source-code escrow, or exit/transition-out plan if the vendor relationship ends.

### 4.4 Resilience and Integration Robustness
27. **No resilience pattern requirements for external integrations** — circuit breakers, retry-with-backoff, dead-letter queues for failed integration messages, and idempotency keys to prevent duplicate transaction processing are not specified, despite the system depending heavily on BAS/LOGIS/PERSAL/CSD availability.
28. **Compliance-engine outage behaviour is undefined** — the design blocks transactions on RED but does not specify what happens if the rules engine itself is unavailable (fail-open risk to patient care vs. fail-closed risk to compliance) — this needs an explicit, risk-assessed decision and fallback procedure.
29. **Idempotent case numbering under concurrent/high-availability failover** is asserted ("never reused") but the concurrency-safe generation mechanism (e.g., DB sequence vs. distributed counter under multi-node deployment) is not specified.
30. **No offline/low-connectivity handling** for remote clinics/facilities with poor network connectivity — a real operational risk for a decentralised Department of Health rollout.

### 4.5 Data Quality, Governance, Analytics
31. **Master data governance/stewardship roles are not defined** — who owns supplier, contract, facility and organisational-unit master data quality, and what deduplication/validation rules apply on ingestion.
32. **Data anonymization/pseudonymization requirements for the analytics warehouse** are missing, especially given sensitive consequence-management/personal data feeding dashboards.
33. **Reconciliation/traceability between operational DB and analytics warehouse** (ETL failure handling, late-arriving data, reconciliation reports) is not specified.

### 4.6 UX, Training, Change Adoption
34. **Training, user adoption and change-management plan** is absent — a system spanning hospital finance officials to Accounting Officer needs role-based training, user guides, and a rollout/communication plan (especially given decentralised, non-technical facility users).
35. **Multi-language/localisation requirements** are not addressed if facility-level users require languages beyond English.
36. **Mobile/tablet workflow scope is vague** ("selected mobile workflows") — needs explicit list of which workflows must work on mobile (e.g., emergency override capture at point of care).

### 4.7 Governance and Traceability
37. **No formal requirements-to-legislation traceability matrix deliverable is mandated** (only recommended as a note) — this should be a hard acceptance gate before UAT sign-off, given PFMA/Treasury Regulation dependency.
38. **No defined success metrics/KPIs for the business outcome** (e.g., % reduction in irregular expenditure, average case-resolution days, % expenditure prevented pre-payment) to measure post-implementation value.
39. **No project governance structure specified** — steering committee, business owner, product owner, change-control board, and escalation path for scope/requirement disputes during build.

---

## 5. Recommended Immediate Additions to the SRS Before Development Starts

| Priority | Action |
|---|---|
| Critical | Quantify RPO/RTO, concurrency targets, and peak-load figures; get Treasury/IT sign-off |
| Critical | Define compliance-engine failure/fallback behavior (fail-open vs fail-closed) with clinical risk sign-off |
| Critical | Add secrets management, API security (OWASP API Top 10), and dependency/SCA scanning requirements |
| High | Define support model (L1–L3), incident severities, SLAs, and on-call coverage |
| High | Add data migration/cutover and parallel-run requirements for legacy registers |
| High | Quantify NFR targets (concurrency, response time percentiles e.g. P95, storage growth) |
| Medium | Add training/change-management and multi-language/mobile scope decisions |
| Medium | Add master-data governance and analytics anonymization requirements |
| Medium | Mandate the legislation-traceability matrix as a UAT gate, not just a recommendation |

---

## 6. Traceability Note
This SRS should be maintained under version control alongside the Narrative and Technical Design documents. Each FR/NFR ID above should map to: (a) source document section, (b) design component, (c) test case ID, and (d) UAT sign-off owner — to be built out in the full Requirements Traceability Matrix (RTM) during detailed design.
