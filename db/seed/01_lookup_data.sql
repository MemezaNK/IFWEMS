-- IFWEMS lookup/reference data seed
-- Run after EF Core migrations have been applied to the target database.
-- This script is idempotent (safe to re-run).

SET NOCOUNT ON;

------------------------------------------------------------
-- Organisational Units (Department -> District -> Facility)
------------------------------------------------------------
DECLARE @DeptId UNIQUEIDENTIFIER = NEWID();

IF NOT EXISTS (SELECT 1 FROM OrgUnits WHERE Code = 'DOH')
BEGIN
    INSERT INTO OrgUnits (Id, Code, Name, Level, ParentOrgUnitId, EffectiveFromUtc, IsActive, CreatedAtUtc)
    VALUES (@DeptId, 'DOH', 'Department of Health', 'Department', NULL, GETUTCDATE(), 1, GETUTCDATE());
END
ELSE
    SELECT @DeptId = Id FROM OrgUnits WHERE Code = 'DOH';

DECLARE @Districts TABLE (Code NVARCHAR(20), Name NVARCHAR(200));
INSERT INTO @Districts (Code, Name) VALUES
    ('DIST-01', 'Metro District'),
    ('DIST-02', 'Northern District'),
    ('DIST-03', 'Southern District'),
    ('DIST-04', 'Eastern District'),
    ('DIST-05', 'Western District');

DECLARE @DistCode NVARCHAR(20), @DistName NVARCHAR(200), @DistId UNIQUEIDENTIFIER;
DECLARE dist_cursor CURSOR FOR SELECT Code, Name FROM @Districts;
OPEN dist_cursor;
FETCH NEXT FROM dist_cursor INTO @DistCode, @DistName;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM OrgUnits WHERE Code = @DistCode)
    BEGIN
        SET @DistId = NEWID();
        INSERT INTO OrgUnits (Id, Code, Name, Level, ParentOrgUnitId, EffectiveFromUtc, IsActive, CreatedAtUtc)
        VALUES (@DistId, @DistCode, @DistName, 'District', @DeptId, GETUTCDATE(), 1, GETUTCDATE());

        -- Two facilities per district
        INSERT INTO OrgUnits (Id, Code, Name, Level, ParentOrgUnitId, EffectiveFromUtc, IsActive, CreatedAtUtc)
        VALUES
            (NEWID(), @DistCode + '-FAC-01', @DistName + ' General Hospital', 'Facility', @DistId, GETUTCDATE(), 1, GETUTCDATE()),
            (NEWID(), @DistCode + '-FAC-02', @DistName + ' Community Health Centre', 'Facility', @DistId, GETUTCDATE(), 1, GETUTCDATE());
    END
    FETCH NEXT FROM dist_cursor INTO @DistCode, @DistName;
END
CLOSE dist_cursor;
DEALLOCATE dist_cursor;

------------------------------------------------------------
-- Permissions
------------------------------------------------------------
DECLARE @Permissions TABLE (Code NVARCHAR(100), Description NVARCHAR(300));
INSERT INTO @Permissions (Code, Description) VALUES
    ('users.manage', 'Create, edit, deactivate users'),
    ('roles.manage', 'Create and assign roles and permissions'),
    ('orgunits.manage', 'Manage organisational hierarchy'),
    ('cases.create', 'Capture a new case'),
    ('cases.review', 'Review a captured case'),
    ('cases.approve', 'Approve a case determination'),
    ('rules.create', 'Create/edit compliance rules'),
    ('rules.approve', 'Approve compliance rules before activation'),
    ('investigations.perform', 'Perform an investigation'),
    ('investigations.approve', 'Approve investigation outcome'),
    ('recoveries.capture', 'Capture recovery transactions'),
    ('recoveries.approve', 'Approve recovery write-off'),
    ('reports.view', 'View reports and registers'),
    ('config.manage', 'Manage system configuration and notification templates');

INSERT INTO Permissions (Id, Code, Description, CreatedAtUtc)
SELECT NEWID(), p.Code, p.Description, GETUTCDATE()
FROM @Permissions p
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = p.Code);

------------------------------------------------------------
-- Roles
------------------------------------------------------------
DECLARE @Roles TABLE (Name NVARCHAR(100), Description NVARCHAR(300));
INSERT INTO @Roles (Name, Description) VALUES
    ('SystemAdministrator', 'Full administrative access to the application'),
    ('CaseOfficer', 'Captures and assesses cases (maker)'),
    ('CaseReviewer', 'Reviews cases (checker)'),
    ('ApprovingOfficial', 'Approves determinations, recoveries and write-offs'),
    ('Investigator', 'Performs investigations'),
    ('ComplianceOfficer', 'Creates and manages compliance rules'),
    ('FinanceOfficer', 'Captures recovery transactions'),
    ('ReadOnlyAuditor', 'Read-only access to cases, reports and audit trail');

INSERT INTO Roles (Id, Name, Description, CreatedAtUtc)
SELECT NEWID(), r.Name, r.Description, GETUTCDATE()
FROM @Roles r
WHERE NOT EXISTS (SELECT 1 FROM Roles WHERE Name = r.Name);

-- SystemAdministrator gets every permission
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN Permissions p
WHERE r.Name = 'SystemAdministrator'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);

PRINT 'IFWEMS lookup data seed complete.';
