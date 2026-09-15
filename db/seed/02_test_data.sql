-- IFWEMS high-volume test data seed (for SIT/UAT/dev environments only — never run in production)
-- Depends on 01_lookup_data.sql having been executed first.

SET NOCOUNT ON;

------------------------------------------------------------
-- Suppliers (200 sample suppliers)
------------------------------------------------------------
DECLARE @i INT = 1;
WHILE @i <= 200
BEGIN
    DECLARE @Code NVARCHAR(20) = 'SUP-' + RIGHT('00000' + CAST(@i AS NVARCHAR(10)), 5);
    IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE SupplierCode = @Code)
    BEGIN
        INSERT INTO Suppliers (Id, SupplierCode, Name, RegistrationNumber, DeviationCount, CaseCount, CreatedAtUtc)
        VALUES (
            NEWID(),
            @Code,
            'Test Supplier ' + CAST(@i AS NVARCHAR(10)),
            'REG-' + CAST(10000 + @i AS NVARCHAR(10)),
            @i % 5,
            @i % 3,
            GETUTCDATE()
        );
    END
    SET @i = @i + 1;
END

------------------------------------------------------------
-- Contracts (one per supplier, varying expiry to test alerting thresholds)
------------------------------------------------------------
INSERT INTO Contracts (Id, ContractNumber, Title, SupplierId, OriginalValue, CurrentValue, UtilisedValue, StartDateUtc, ExpiryDateUtc, CreatedAtUtc)
SELECT
    NEWID(),
    'CON-' + s.SupplierCode,
    'Supply agreement - ' + s.Name,
    s.Id,
    500000.00,
    550000.00,
    CAST(500000.00 * (0.5 + (ABS(CHECKSUM(s.SupplierCode)) % 50) / 100.0) AS DECIMAL(18,2)),
    DATEADD(YEAR, -1, GETUTCDATE()),
    DATEADD(DAY, (ABS(CHECKSUM(s.SupplierCode)) % 400) - 30, GETUTCDATE())
    , GETUTCDATE()
FROM Suppliers s
WHERE NOT EXISTS (SELECT 1 FROM Contracts c WHERE c.ContractNumber = 'CON-' + s.SupplierCode);

------------------------------------------------------------
-- Cases (1,000 sample cases spread across case types/statuses/org units)
------------------------------------------------------------
DECLARE @CaseTypes TABLE (Id INT, Code NVARCHAR(5));
INSERT INTO @CaseTypes (Id, Code) VALUES (0, 'IE'), (1, 'FWE'), (2, 'UE'), (3, 'PNC');

DECLARE @j INT = 1;
DECLARE @FacilityCount INT = (SELECT COUNT(*) FROM OrgUnits WHERE Level = 'Facility');

WHILE @j <= 1000
BEGIN
    DECLARE @TypeIdx INT = @j % 4;
    DECLARE @TypeCode NVARCHAR(5) = (SELECT Code FROM @CaseTypes WHERE Id = @TypeIdx);
    DECLARE @CaseNumber NVARCHAR(50) = 'DOH-' + @TypeCode + '-2026-' + RIGHT('000000' + CAST(@j AS NVARCHAR(10)), 6);
    DECLARE @OrgUnitId UNIQUEIDENTIFIER = (
        SELECT TOP 1 Id FROM OrgUnits WHERE Level = 'Facility'
        ORDER BY (ABS(CHECKSUM(NEWID(), @j)))
    );
    DECLARE @SupplierId UNIQUEIDENTIFIER = (
        SELECT TOP 1 Id FROM Suppliers ORDER BY (ABS(CHECKSUM(NEWID(), @j)))
    );

    IF NOT EXISTS (SELECT 1 FROM Cases WHERE CaseNumber = @CaseNumber)
    BEGIN
        INSERT INTO Cases (Id, CaseNumber, CaseType, Status, OrgUnitId, Title, Description, AmountInvolved, SupplierId, CreatedAtUtc)
        VALUES (
            NEWID(),
            @CaseNumber,
            @TypeIdx,
            @j % 8,
            @OrgUnitId,
            'Sample case ' + CAST(@j AS NVARCHAR(10)),
            'Auto-generated test/UAT case record for load and workflow testing.',
            CAST(1000 + (ABS(CHECKSUM(NEWID())) % 500000) AS DECIMAL(18,2)),
            @SupplierId,
            GETUTCDATE()
        );
    END
    SET @j = @j + 1;
END

PRINT 'IFWEMS high-volume test data seed complete (200 suppliers, 200 contracts, 1000 cases).';
