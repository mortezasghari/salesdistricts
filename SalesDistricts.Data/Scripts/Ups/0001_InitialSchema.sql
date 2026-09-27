-- Task 1 schema. Business rules enforced here:
--   * A district ALWAYS has exactly ONE primary salesperson: District.PrimarySalespersonId is NOT NULL
--     (at least one) and a single column (at most one). ON DELETE NO ACTION means a primary cannot be
--     deleted until a replacement is assigned.
--   * The same salesperson may be primary for several districts (transition periods): deliberately no
--     unique index on PrimarySalespersonId.
--   * Zero or more secondaries per district: DistrictSecondarySalesperson, composite key stops duplicates.
--   * A store belongs to exactly one district; a district with stores cannot be deleted.
-- Not enforceable in SQL: "a salesperson belongs to one or more districts" (no deferred constraints in SQL Server).

CREATE SCHEMA sales;
GO

CREATE TABLE sales.Salesperson
(
    Id    uniqueidentifier NOT NULL CONSTRAINT DF_Salesperson_Id DEFAULT NEWSEQUENTIALID(),
    Name  nvarchar(200)    NOT NULL,
    Email nvarchar(320)    NULL,
    CONSTRAINT PK_Salesperson PRIMARY KEY (Id),
    CONSTRAINT CK_Salesperson_Name_NotBlank  CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
    CONSTRAINT CK_Salesperson_Email_NotBlank CHECK (Email IS NULL OR LEN(LTRIM(RTRIM(Email))) > 0)
);
-- Optional email, unique when present.
CREATE UNIQUE INDEX UX_Salesperson_Email ON sales.Salesperson (Email) WHERE Email IS NOT NULL;

CREATE TABLE sales.District
(
    Id                   uniqueidentifier NOT NULL CONSTRAINT DF_District_Id DEFAULT NEWSEQUENTIALID(),
    Name                 nvarchar(100)    NOT NULL,
    PrimarySalespersonId uniqueidentifier NOT NULL,
    CONSTRAINT PK_District PRIMARY KEY (Id),
    CONSTRAINT UQ_District_Name UNIQUE (Name),
    CONSTRAINT CK_District_Name_NotBlank CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
    CONSTRAINT FK_District_PrimarySalesperson FOREIGN KEY (PrimarySalespersonId)
        REFERENCES sales.Salesperson (Id) ON DELETE NO ACTION
);
CREATE INDEX IX_District_PrimarySalespersonId ON sales.District (PrimarySalespersonId);

CREATE TABLE sales.Store
(
    Id         uniqueidentifier NOT NULL CONSTRAINT DF_Store_Id DEFAULT NEWSEQUENTIALID(),
    Name       nvarchar(200)    NOT NULL,
    Address    nvarchar(500)    NULL,
    DistrictId uniqueidentifier NOT NULL,
    CONSTRAINT PK_Store PRIMARY KEY (Id),
    CONSTRAINT CK_Store_Name_NotBlank CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
    CONSTRAINT FK_Store_District FOREIGN KEY (DistrictId) REFERENCES sales.District (Id) ON DELETE NO ACTION,
    -- Unique per district; leading DistrictId also serves the FK lookup.
    CONSTRAINT UQ_Store_District_Name UNIQUE (DistrictId, Name)
);

CREATE TABLE sales.DistrictSecondarySalesperson
(
    DistrictId    uniqueidentifier NOT NULL,
    SalespersonId uniqueidentifier NOT NULL,
    CONSTRAINT PK_DistrictSecondarySalesperson PRIMARY KEY (DistrictId, SalespersonId),
    CONSTRAINT FK_DistrictSecondarySalesperson_District    FOREIGN KEY (DistrictId)    REFERENCES sales.District (Id)    ON DELETE CASCADE,
    CONSTRAINT FK_DistrictSecondarySalesperson_Salesperson FOREIGN KEY (SalespersonId) REFERENCES sales.Salesperson (Id) ON DELETE CASCADE
);
CREATE INDEX IX_DistrictSecondarySalesperson_SalespersonId ON sales.DistrictSecondarySalesperson (SalespersonId);
GO

-- One row per (district, salesperson) regardless of role, so readers need not know the primary is stored apart.
CREATE VIEW sales.vDistrictSalesperson AS
SELECT Id AS DistrictId, PrimarySalespersonId AS SalespersonId, CAST(1 AS bit) AS IsPrimary FROM sales.District
UNION ALL
SELECT DistrictId, SalespersonId, CAST(0 AS bit) FROM sales.DistrictSecondarySalesperson;
