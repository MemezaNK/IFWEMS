-- IFWEMS demo/UAT data seed: users, roles, controls, SLA policies, and full case-lifecycle
-- detail (assessments, investigations, recoveries, corrective actions, evidence documents)
-- layered on top of 02_test_data.sql, so a convincing end-to-end demo can be run immediately
-- after a fresh database build. For SIT/UAT/dev environments only - never run in production.
--
-- Run order: 01_lookup_data.sql -> 02_test_data.sql -> 03_demo_data.sql

SET NOCOUNT ON;

------------------------------------------------------------
-- Demo users, one per role (FR-001/FR-002)
-- All demo accounts use the password Demo@12345, except "admin" which uses Admin@12345.
-- Hashes below were generated with ASP.NET Core Identity's PasswordHasher<T> (PBKDF2/HMAC-SHA256,
-- format marker 0x01) so they verify correctly against AuthService.LoginAsync.
------------------------------------------------------------
DECLARE @RootOrgUnitId UNIQUEIDENTIFIER = (SELECT Id FROM OrgUnits WHERE Code = 'DOH');
DECLARE @FirstFacilityId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM OrgUnits WHERE Level = 'Facility' ORDER BY Code);

DECLARE @DemoUsers TABLE (Username NVARCHAR(100), Email NVARCHAR(200), DisplayName NVARCHAR(200), PasswordHash NVARCHAR(500), RoleName NVARCHAR(100));
INSERT INTO @DemoUsers (Username, Email, DisplayName, PasswordHash, RoleName) VALUES
    ('admin', 'admin@ifwems.demo', 'System Administrator', 'AQAAAAIAAYagAAAAELB4DfiRNFwUjOHRD7fgzE/oXiMtmvgv8ih6AEZrYTt9ms68V6YH6FsictbXrHb6gg==', 'SystemAdministrator'),
    ('caseofficer1', 'caseofficer1@ifwems.demo', 'Thandiwe Nkosi (Case Officer)', 'AQAAAAIAAYagAAAAEG1lI7iuscx0z5/xALOVSrmOuJRCGOtWS8lgD5S2+7tZcqqNlUXRZgZN5LpyJRXmdg==', 'CaseOfficer'),
    ('caseofficer2', 'caseofficer2@ifwems.demo', 'Sipho Dlamini (Case Officer)', 'AQAAAAIAAYagAAAAEH7YMs9OlDI100u0pTFkE/Jo2qXKFm2dpgRxY/hxFZHz3LP7oInAV6bTJvo8GUmo8g==', 'CaseOfficer'),
    ('casereviewer1', 'casereviewer1@ifwems.demo', 'Nomvula Khumalo (Case Reviewer)', 'AQAAAAIAAYagAAAAEHJy2n17lFx8PICAWaa9FoLjcQ1AAWUe4Kd0OiZMPzIvMOW2o1J2kHtdVMgGyrMiEw==', 'CaseReviewer'),
    ('approver1', 'approver1@ifwems.demo', 'Johan van der Merwe (Approving Official)', 'AQAAAAIAAYagAAAAEJ5+c8WBJAxlf26SUIMCSY0dWbVaik2qKhwKtveYp3C3xR+mZW/QSaDUYXNvM+4XnQ==', 'ApprovingOfficial'),
    ('investigator1', 'investigator1@ifwems.demo', 'Palesa Mokoena (Investigator)', 'AQAAAAIAAYagAAAAEBQL9SNZT1fTOlu7uONkWZVauUYvKVogFanGvNvN2GqW34MV3AOazZuIuPlFtkHRWA==', 'Investigator'),
    ('compliance1', 'compliance1@ifwems.demo', 'Amanda Botha (Compliance Officer)', 'AQAAAAIAAYagAAAAEJAfNSKiaWJJCcvZDCfKALwq3RctFaz5cx0gascI29ICfcuWZ5x9aIjcCAoVaVE1Tw==', 'ComplianceOfficer'),
    ('finance1', 'finance1@ifwems.demo', 'Bongani Zulu (Finance Officer)', 'AQAAAAIAAYagAAAAEDxSurZmxNsHOpdySSQczEGqZZduAJ0JCMi9GaY+Qvl6j9kghohZC/6nvM/HC3rWtw==', 'FinanceOfficer'),
    ('contractofficer1', 'contractofficer1@ifwems.demo', 'Karabo Mahlangu (Contract Officer)', 'AQAAAAIAAYagAAAAEBVQQTxnmIPnJuqG0y4YHItA30VpVObqekn17kC0ApQ9HKyWLTij+TQB3fQ8Qgsmfg==', 'ContractOfficer'),
    ('auditor1', 'auditor1@ifwems.demo', 'Elize Pretorius (Read-Only Auditor)', 'AQAAAAIAAYagAAAAEONRotbnqM4RpfhDnvnA2kHYbt7EKPu/4qeDN3JWsxHeppP/DUaSN1T0gYpJhe9lSw==', 'ReadOnlyAuditor');

INSERT INTO Users (Id, Username, Email, DisplayName, PasswordHash, IsActive, MfaEnabled, FailedLoginAttempts, LockedOutUntilUtc, OrgUnitId, CreatedAtUtc)
SELECT NEWID(), d.Username, d.Email, d.DisplayName, d.PasswordHash, 1, 0, 0, NULL,
       CASE WHEN d.RoleName IN ('CaseOfficer', 'CaseReviewer', 'Investigator') THEN @FirstFacilityId ELSE @RootOrgUnitId END,
       GETUTCDATE()
FROM @DemoUsers d
WHERE NOT EXISTS (SELECT 1 FROM Users WHERE Username = d.Username);

INSERT INTO UserRoles (UserId, RoleId, OrgUnitId)
SELECT u.Id, r.Id, NULL
FROM @DemoUsers d
JOIN Users u ON u.Username = d.Username
JOIN Roles r ON r.Name = d.RoleName
WHERE NOT EXISTS (SELECT 1 FROM UserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);

------------------------------------------------------------
-- Control library (FR-026)
------------------------------------------------------------
DECLARE @Controls TABLE (Code NVARCHAR(50), Name NVARCHAR(200), Description NVARCHAR(500));
INSERT INTO @Controls (Code, Name, Description) VALUES
    ('CTRL-PROC-01', 'Segregation of duties in procurement', 'Requisition, approval and payment must be performed by different officials.'),
    ('CTRL-PROC-02', 'Three-quotation verification', 'Verify at least three comparative quotations exist before award, except approved deviations.'),
    ('CTRL-CONTRACT-01', 'Contract expiry monitoring', 'Monthly review of contracts approaching expiry to prevent lapses/retrospective extensions.'),
    ('CTRL-BUDGET-01', 'Budget/commitment control', 'Pre-commitment check against available budget before purchase order issue.'),
    ('CTRL-SUPPLIER-01', 'Supplier database validation', 'Validate supplier tax/CSD status before onboarding and periodically thereafter.'),
    ('CTRL-INVOICE-01', 'Duplicate invoice detection', 'System-enforced check for duplicate invoice references per supplier.'),
    ('CTRL-DELEGATION-01', 'Delegation of authority enforcement', 'System-enforced delegation ceilings per role/org unit.'),
    ('CTRL-TRAINING-01', 'SCM policy refresher training', 'Mandatory annual supply chain management policy refresher training for requisitioning staff.');

INSERT INTO Controls (Id, Code, Name, Description, IsActive, CreatedAtUtc)
SELECT NEWID(), c.Code, c.Name, c.Description, 1, GETUTCDATE()
FROM @Controls c
WHERE NOT EXISTS (SELECT 1 FROM Controls WHERE Code = c.Code);

------------------------------------------------------------
-- SLA policies per case type / status (FR-040), tuned so a realistic mix of demo cases
-- will show up as within-SLA vs. overdue on GET /api/sla/overdue-cases.
------------------------------------------------------------
DECLARE @SlaHours TABLE (Status INT, MaxDurationHours INT);
INSERT INTO @SlaHours (Status, MaxDurationHours) VALUES
    (1, 72),   -- UnderAssessment
    (2, 168),  -- UnderInvestigation
    (3, 48),   -- Determined
    (4, 120),  -- RecoveryInProgress
    (5, 96),   -- ConsequenceManagement
    (6, 168);  -- CorrectiveActionPending

INSERT INTO SlaPolicies (Id, CaseType, Status, MaxDurationHours, Version, IsActive, EffectiveFromUtc, EffectiveToUtc, CreatedAtUtc)
SELECT NEWID(), ct.CaseTypeId, s.Status, s.MaxDurationHours, 1, 1, DATEADD(MONTH, -6, GETUTCDATE()), NULL, GETUTCDATE()
FROM @SlaHours s
CROSS JOIN (VALUES (0), (1), (2), (3)) AS ct(CaseTypeId)
WHERE NOT EXISTS (SELECT 1 FROM SlaPolicies p WHERE p.CaseType = ct.CaseTypeId AND p.Status = s.Status AND p.IsActive = 1);

------------------------------------------------------------
-- Case status history: one Draft -> current-status transition per non-Draft case, with
-- ChangedAtUtc spread across the last ~3 weeks so some cases breach SLA and some don't.
------------------------------------------------------------
INSERT INTO CaseStatusHistories (CaseId, FromStatus, ToStatus, ChangedBy, Reason, ChangedAtUtc)
SELECT c.Id, 0, c.Status, 'system-seed', 'Demo data: simulated transition into current status.',
       DATEADD(HOUR, -(24 + (ABS(CHECKSUM(c.Id)) % 500)), GETUTCDATE())
FROM Cases c
WHERE c.Status <> 0
  AND NOT EXISTS (SELECT 1 FROM CaseStatusHistories h WHERE h.CaseId = c.Id);

------------------------------------------------------------
-- Case assessments (FR-022) for every case beyond Draft
------------------------------------------------------------
INSERT INTO CaseAssessments (Id, CaseId, AssessorUserId, AnswersJson, Conclusion, Reasons, CreatedAtUtc)
SELECT NEWID(), c.Id,
       (SELECT TOP 1 Id FROM Users WHERE Username = 'caseofficer1'),
       '{"wasCompetitiveProcessFollowed":false,"wasDelegationExceeded":true,"wasContractValid":true}',
       CASE c.CaseType WHEN 0 THEN 'Irregular expenditure confirmed - non-compliant procurement process.'
                        WHEN 1 THEN 'Fruitless and wasteful expenditure confirmed - no value received.'
                        WHEN 2 THEN 'Unauthorised expenditure confirmed - no budget provision.'
                        ELSE 'Potential non-compliance requires further review.' END,
       'Assessed against SCM policy checklist and delegation of authority framework.',
       GETUTCDATE()
FROM Cases c
WHERE c.Status <> 0
  AND NOT EXISTS (SELECT 1 FROM CaseAssessments a WHERE a.CaseId = c.Id);

------------------------------------------------------------
-- Investigations (FR-023) for cases from UnderInvestigation onward
------------------------------------------------------------
INSERT INTO Investigations (Id, CaseId, InvestigatorUserId, Status, Findings, Recommendation, ApprovedByUserId, ApprovedAtUtc, CreatedAtUtc)
SELECT NEWID(), c.Id,
       (SELECT TOP 1 Id FROM Users WHERE Username = 'investigator1'),
       CASE WHEN c.Status IN (4, 5, 6, 7) THEN 2 ELSE 0 END, -- Approved vs InProgress
       'Investigation identified a breakdown in the ' + CASE c.CaseType WHEN 0 THEN 'competitive bidding' WHEN 1 THEN 'value-for-money assessment' WHEN 2 THEN 'budget authorisation' ELSE 'compliance screening' END + ' control.',
       'Recommend recovery action and referral to consequence management where applicable.',
       CASE WHEN c.Status IN (4, 5, 6, 7) THEN (SELECT TOP 1 Id FROM Users WHERE Username = 'approver1') ELSE NULL END,
       CASE WHEN c.Status IN (4, 5, 6, 7) THEN GETUTCDATE() ELSE NULL END,
       GETUTCDATE()
FROM Cases c
WHERE c.Status IN (2, 3, 4, 5, 6, 7)
  AND NOT EXISTS (SELECT 1 FROM Investigations i WHERE i.CaseId = c.Id);

------------------------------------------------------------
-- Recoveries (FR-025): set a recoverable balance and reconcile recovered amounts
------------------------------------------------------------
UPDATE c
SET RecoverableAmount = CAST(c.AmountInvolved * 0.8 AS DECIMAL(18,2))
FROM Cases c
WHERE c.Status IN (4, 5, 6, 7) AND c.AmountInvolved IS NOT NULL AND c.RecoverableAmount = 0;

INSERT INTO Recoveries (Id, CaseId, Amount, Status, Reference, CapturedByUserId, ApprovedByUserId, ApprovedAtUtc, WriteOffReason, CreatedAtUtc)
SELECT NEWID(), c.Id,
       CAST(c.RecoverableAmount * CASE WHEN c.Status = 7 THEN 1.0 WHEN c.Status IN (5, 6) THEN 0.7 ELSE 0.4 END AS DECIMAL(18,2)),
       CASE WHEN c.Status = 7 THEN 2 ELSE 0 END, -- Recovered vs Captured
       'RCPT-' + c.CaseNumber,
       (SELECT TOP 1 Id FROM Users WHERE Username = 'finance1'),
       CASE WHEN c.Status = 7 THEN (SELECT TOP 1 Id FROM Users WHERE Username = 'approver1') ELSE NULL END,
       CASE WHEN c.Status = 7 THEN GETUTCDATE() ELSE NULL END,
       NULL,
       GETUTCDATE()
FROM Cases c
WHERE c.Status IN (4, 5, 6, 7) AND c.RecoverableAmount > 0
  AND NOT EXISTS (SELECT 1 FROM Recoveries r WHERE r.CaseId = c.Id);

UPDATE c
SET RecoveredAmount = ISNULL((SELECT SUM(r.Amount) FROM Recoveries r WHERE r.CaseId = c.Id AND r.Status = 2), 0)
FROM Cases c
WHERE c.Status IN (4, 5, 6, 7);

------------------------------------------------------------
-- Corrective actions (FR-026) for cases at/after CorrectiveActionPending
------------------------------------------------------------
INSERT INTO CorrectiveActions (Id, CaseId, ControlId, Description, Status, EvidenceDocumentId, VerifiedBy, VerifiedAtUtc, CreatedAtUtc)
SELECT NEWID(), c.Id,
       (SELECT TOP 1 Id FROM Controls ORDER BY (ABS(CHECKSUM(c.Id)) % 100)),
       'Remediation of control weakness identified during investigation of ' + c.CaseNumber + '.',
       CASE WHEN c.Status = 7 THEN 3 ELSE 1 END, -- Verified vs InProgress
       NULL,
       CASE WHEN c.Status = 7 THEN 'casereviewer1' ELSE NULL END,
       CASE WHEN c.Status = 7 THEN GETUTCDATE() ELSE NULL END,
       GETUTCDATE()
FROM Cases c
WHERE c.Status IN (6, 7)
  AND NOT EXISTS (SELECT 1 FROM CorrectiveActions ca WHERE ca.CaseId = c.Id);

------------------------------------------------------------
-- Evidence documents (FR-032) - metadata only; StoragePath/IntegrityHash are seed placeholders
-- (no physical file on disk), so use these rows to demo listing/registers, not file download.
------------------------------------------------------------
INSERT INTO Documents (Id, FileName, DocumentType, ConfidentialityClassification, StoragePath, IntegrityHash, Version, UploadedBy, CaseId, CreatedAtUtc)
SELECT NEWID(),
       c.CaseNumber + '-evidence.pdf',
       CASE c.CaseType WHEN 0 THEN 'Procurement Pack' WHEN 1 THEN 'Asset Write-off Memo' WHEN 2 THEN 'Budget Authorisation' ELSE 'Compliance Review Note' END,
       CASE WHEN c.Status IN (5, 6, 7) THEN 'INVESTIGATION RESTRICTED' ELSE 'INTERNAL' END,
       '/seed-placeholder/' + CAST(NEWID() AS NVARCHAR(50)) + '.pdf',
       CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', CAST(NEWID() AS NVARCHAR(50))), 2),
       1,
       'system-seed',
       c.Id,
       GETUTCDATE()
FROM Cases c
WHERE c.Status <> 0
  AND (ABS(CHECKSUM(c.Id)) % 10) < 3 -- roughly 30% of eligible cases get a demo document
  AND NOT EXISTS (SELECT 1 FROM Documents doc WHERE doc.CaseId = c.Id);

PRINT 'IFWEMS demo data seed complete: 10 role-covering users, control library, SLA policies, and full case-lifecycle detail (assessments, investigations, recoveries, corrective actions, documents).';
PRINT 'Demo login: username=admin, password=Admin@12345. All other demo users: password=Demo@12345.';
